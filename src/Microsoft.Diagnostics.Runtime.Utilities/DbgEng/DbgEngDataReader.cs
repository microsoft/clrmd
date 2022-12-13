using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public sealed class DbgEngDataReader : IDataReader, IDisposable, IThreadReader
    {
        private IDebugClient _client;
        private IDebugControl _control;
        private IDebugDataSpaces _spaces;
        private IDebugAdvanced _advanced;
        private IDebugSymbols _symbols;
        private IDisposable _dbgeng;
        private IDebugSystemObjects _systemObjects = null!;

        private bool _disposed;

        private List<ModuleInfo>? _modules;
        private int? _pointerSize;
        private Architecture? _architecture;

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => OSPlatform.Windows;

        public DbgEngDataReader(string displayName, Stream stream, bool leaveOpen)
            : this((stream as FileStream)?.Name ?? throw new NotSupportedException($"{nameof(DbgEngDataReader)} can only be used with real files. Try to use {nameof(FileStream)}."))
        {
            DisplayName = displayName;
            if (!leaveOpen)
                stream?.Dispose();
        }

        public DbgEngDataReader(IntPtr pDebugClient)
        {
            if (pDebugClient == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pDebugClient));

            DisplayName = $"DbgEng, IDebugClient={pDebugClient.ToInt64():x}";

            _dbgeng = IDebugClient.Create();
            _systemObjects = (IDebugSystemObjects)_dbgeng;
            _client = (IDebugClient)_dbgeng;
            _control = (IDebugControl)_dbgeng;
            _spaces = (IDebugDataSpaces)_dbgeng;
            _advanced = (IDebugAdvanced)_dbgeng;
            _symbols = (IDebugSymbols)_dbgeng;
        }

        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            DisplayName = dumpFile;

            _dbgeng = IDebugClient.Create();
            _systemObjects = (IDebugSystemObjects)_dbgeng;
            _client = (IDebugClient)_dbgeng;
            _control = (IDebugControl)_dbgeng;
            _spaces = (IDebugDataSpaces)_dbgeng;
            _advanced = (IDebugAdvanced)_dbgeng;
            _symbols = (IDebugSymbols)_dbgeng;

            HResult hr = _client.OpenDumpFile(dumpFile);
            if (hr != 0)
            {
                const int STATUS_MAPPED_FILE_SIZE_ZERO = unchecked((int)0xC000011E);

                if (hr == HResult.E_INVALIDARG || hr == (STATUS_MAPPED_FILE_SIZE_ZERO | 0x10000000))
                    throw new InvalidDataException($"'{dumpFile}' is not a crash dump.");

                throw CreateExceptionFromDumpFile(dumpFile, hr);
            }

            // This actually "attaches" to the crash dump.
            HResult result = _control.WaitForEvent(TimeSpan.MaxValue);
            if (!result)
                throw CreateExceptionFromDumpFile(dumpFile, hr);
        }

        private static ClrDiagnosticsException CreateExceptionFromDumpFile(string dumpFile, HResult hr)
        {
            ClrDiagnosticsException ex = new ClrDiagnosticsException($"Could not load crash dump, HRESULT: {hr}", hr);
            ex.Data["DumpFile"] = dumpFile;
            return ex;
        }

        public DbgEngDataReader(int processId, bool invasive, TimeSpan timeout)
        {
            DisplayName = $"{processId:x}";

            _dbgeng = (IDisposable)IDebugClient.Create();
            _systemObjects = (IDebugSystemObjects)_dbgeng;
            _client = (IDebugClient)_dbgeng;
            _control = (IDebugControl)_dbgeng;
            _spaces = (IDebugDataSpaces)_dbgeng;
            _advanced = (IDebugAdvanced)_dbgeng;
            _symbols = (IDebugSymbols)_dbgeng;

            HResult hr = _control.AddEngineOptions(DEBUG_ENGOPT.INITIAL_BREAK);

            DEBUG_ATTACH attach = invasive ? DEBUG_ATTACH.DEFAULT : DEBUG_ATTACH.NONINVASIVE;
            hr = _client.AttachProcess(processId, attach);

            if (hr)
                hr = _control.WaitForEvent(timeout);

            if (hr == HResult.S_FALSE)
            {
                throw new TimeoutException("Break in did not occur within the allotted timeout.");
            }

            if (hr != 0)
            {
                if ((uint)hr.Value == 0xd00000bb)
                    throw new InvalidOperationException("Mismatched architecture between this process and the target process.");

                throw new ArgumentException($"Could not attach to process {processId}, HRESULT: 0x{hr:x}");
            }
        }

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                _client.EndSession(DEBUG_END.ACTIVE_DETACH);
                _client.DetachProcesses();
                _dbgeng.Dispose();
            }
        }

        public bool IsThreadSafe => false;

        public int ProcessId => _systemObjects.ProcessSystemId;

        public Architecture Architecture => _architecture ??= _control.CpuType switch
        {
            ImageFileMachine.I386 => Architecture.X86,
            ImageFileMachine.AMD64 => Architecture.X64,
            ImageFileMachine.ARM or
            (ImageFileMachine)0x01c2 or                     // THUMB
            (ImageFileMachine)0x01c4 => Architecture.Arm,   // THUMB2
            (ImageFileMachine)0xAA64 => Architecture.Arm64, // ARM64
            _ => (Architecture)(-1)
        };

        public int PointerSize => _pointerSize ??= _control.PointerSize;

        public void FlushCachedData()
        {
            _modules = null;
        }

        public bool GetThreadContext(uint systemId, uint contextFlags, Span<byte> context)
        {
            int curr = _systemObjects.CurrentThreadId;
            try
            {
                if (_systemObjects.GetThreadIdBySystemId((int)systemId, out int id) < 0)
                    return false;

                _systemObjects.CurrentThreadId = id;
                return _advanced.GetThreadContext(context) >= 0;
            }
            finally
            {
                _systemObjects.CurrentThreadId = curr;
            }
        }

        private ulong[] GetImageBases()
        {
            HResult hr = _symbols.GetNumberModules(out int count, out _);

            if (!hr)
                return Array.Empty<ulong>();

            int index = 0;
            ulong[] bases = new ulong[count];
            for (int i = 0; i < count; ++i)
            {
                hr = _symbols.GetImageBase(i, out ulong baseAddress);
                if (hr)
                    bases[index++] = baseAddress;
            }

            if (index < bases.Length)
                Array.Resize(ref bases, index);

            return bases;
        }

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_modules != null)
                return _modules;

            ulong[] bases = GetImageBases();
            if (bases.Length == 0)
                return Enumerable.Empty<ModuleInfo>();

            DEBUG_MODULE_PARAMETERS[] moduleParams = new DEBUG_MODULE_PARAMETERS[bases.Length];

            List<ModuleInfo> modules = new List<ModuleInfo>();
            HResult hr = _symbols.GetModuleParameters(bases, moduleParams);
            if (hr)
            {
                for (int i = 0; i < bases.Length; ++i)
                {
                    _symbols.GetModuleName(DEBUG_MODNAME.IMAGE, bases[i], out string moduleName);

                    unchecked
                    {
                        var module = ModuleInfo.TryCreate(this, bases[i], moduleName, (int)moduleParams[i].Size, (int)moduleParams[i].TimeDateStamp, GetVersionInfo(bases[i]));
                        if (module is not null)
                            modules.Add(module);
                    }
                }
            }

            _modules = modules;
            return modules;
        }

        public int Read(ulong address, Span<byte> buffer)
        {
            if (!_spaces.ReadVirtual(address, buffer, out int read))
                return 0;

            return read;
        }

        public Version? GetVersionInfo(ulong baseAddress)
        {
            if (!FindModuleIndex(baseAddress, out int index))
                return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            try
            {
                HResult hr = _symbols.GetModuleVersionInformation(index, baseAddress, "\\\\\0", buffer);
                if (!hr)
                    return new Version();

                int minor = Unsafe.As<byte, ushort>(ref buffer[8]);
                int major = Unsafe.As<byte, ushort>(ref buffer[10]);
                int patch = Unsafe.As<byte, ushort>(ref buffer[12]);
                int revision = Unsafe.As<byte, ushort>(ref buffer[14]);

                return new Version(major, minor, revision, patch);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private bool FindModuleIndex(ulong baseAddr, out int index)
        {
            /* GetModuleByOffset returns the first module (from startIndex) which
             * includes baseAddr.
             * However when loading 64-bit dumps of 32-bit processes it seems that
             * the module sizes are sometimes wrong, which may cause a wrong module
             * to be found because it overlaps the beginning of the queried module,
             * so search until we find a module that actually has the correct
             * baseAddr */
            int nextIndex = 0;
            while (true)
            {
                if (_symbols.GetModuleByOffset(baseAddr, nextIndex, out index, out ulong claimedBaseAddr) < 0)
                {
                    index = 0;
                    return false;
                }

                if (claimedBaseAddr == baseAddr)
                    return true;

                nextIndex = index + 1;
            }
        }

        public IEnumerable<uint> EnumerateOSThreadIds()
        {
            _systemObjects.GetNumberThreads(out int count);
            if (count == 0)
                return Array.Empty<uint>();

            uint[] result = new uint[count];
            HResult hr = _systemObjects.GetThreadSystemIDs(result);
            if (hr)
                return result;

            return Array.Empty<uint>();
        }

        public ulong GetThreadTeb(uint osThreadId)
        {
            int curr = _systemObjects.CurrentThreadId;
            try
            {
                HResult hr = _systemObjects.GetThreadIdBySystemId((int)osThreadId, out int id);
                if (hr)
                {
                    hr = _systemObjects.GetCurrentThreadTeb(out ulong teb);
                    if (hr)
                        return teb;
                }
            }
            finally
            {
                _systemObjects.CurrentThreadId = curr;
            }

            return 0;
        }

        public unsafe bool Read<T>(ulong address, out T value)
            where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer) == buffer.Length)
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address)
            where T : unmanaged
        {
            Read(address, out T result);
            return result;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (Read(address, buffer) == IntPtr.Size)
            {
                value = Unsafe.As<byte, nuint>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = 0;
            return false;
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }
    }
}