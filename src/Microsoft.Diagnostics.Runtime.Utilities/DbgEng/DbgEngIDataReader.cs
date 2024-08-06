// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public sealed class DbgEngIDataReader : IDataReader, IDisposable, IThreadReader
    {
        private const string VersionString = "@(#)Version ";
        private readonly IDisposable? _dbgeng;
        public IDebugClient DebugClient { get; }
        public IDebugControl DebugControl { get; }
        public IDebugDataSpaces DebugDataSpaces { get; }
        public IDebugAdvanced DebugAdvanced { get; }
        public IDebugSymbols DebugSymbols { get; }
        public IDebugSystemObjects DebugSystemObjects { get; }

        private bool _disposed;

        private List<ModuleInfo>? _modules;
        private int? _pointerSize;
        private Architecture? _architecture;

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => DebugControl.OSPlatform;

        public static DataTarget CreateDataTarget(nint pDebugClient)
        {
            return new DataTarget(new CustomDataTarget(new DbgEngIDataReader(pDebugClient)));
        }

        public static DataTarget CreateDataTarget(object dbgeng)
        {
            return new DataTarget(new CustomDataTarget(new DbgEngIDataReader(dbgeng)));
        }

        /// <summary>
        /// Creates an instance of DbgEngIDataReader from the given object.  This object
        /// must be castable to these interfaces: IDebugClient, IDebugControl, IDebugDataSpaces,
        /// IDebugAdvanced, IDebugSymbols, IDebugSystemObjects.
        ///
        /// The most common way to obtain a working version of this object is via IDebugClient.Create.
        /// </summary>
        /// <param name="dbgeng">An implementation of DbgEng interfaces.</param>
        public DbgEngIDataReader(object dbgeng)
        {
            DisplayName = $"DbgEng, DbgEng={dbgeng}";

            _dbgeng = dbgeng as IDisposable;
            DebugClient = (IDebugClient)dbgeng;
            DebugControl = (IDebugControl)dbgeng;
            DebugDataSpaces = (IDebugDataSpaces)dbgeng;
            DebugAdvanced = (IDebugAdvanced)dbgeng;
            DebugSymbols = (IDebugSymbols)dbgeng;
            DebugSystemObjects = (IDebugSystemObjects)dbgeng;
        }

        public DbgEngIDataReader(nint pDebugClient)
        {
            if (pDebugClient == 0)
                throw new ArgumentNullException(nameof(pDebugClient));

            DisplayName = $"DbgEng, IDebugClient={pDebugClient:x}";

            _dbgeng = IDebugClient.Create(pDebugClient);
            DebugClient = (IDebugClient)_dbgeng;
            DebugControl = (IDebugControl)_dbgeng;
            DebugDataSpaces = (IDebugDataSpaces)_dbgeng;
            DebugAdvanced = (IDebugAdvanced)_dbgeng;
            DebugSymbols = (IDebugSymbols)_dbgeng;
            DebugSystemObjects = (IDebugSystemObjects)_dbgeng;
        }

        public DbgEngIDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            DisplayName = dumpFile;

            _dbgeng = IDebugClient.Create();
            DebugSystemObjects = (IDebugSystemObjects)_dbgeng;
            DebugClient = (IDebugClient)_dbgeng;
            DebugControl = (IDebugControl)_dbgeng;
            DebugDataSpaces = (IDebugDataSpaces)_dbgeng;
            DebugAdvanced = (IDebugAdvanced)_dbgeng;
            DebugSymbols = (IDebugSymbols)_dbgeng;

            HResult hr = DebugClient.OpenDumpFile(dumpFile);
            if (hr != 0)
            {
                const int STATUS_MAPPED_FILE_SIZE_ZERO = unchecked((int)0xC000011E);

                if (hr == HResult.E_INVALIDARG || hr == (STATUS_MAPPED_FILE_SIZE_ZERO | 0x10000000))
                    throw new InvalidDataException($"'{dumpFile}' is not a crash dump.");

                throw CreateExceptionFromDumpFile(dumpFile, hr);
            }

            // This actually "attaches" to the crash dump.
            HResult result = DebugControl.WaitForEvent(TimeSpan.MaxValue);
            if (!result)
                throw CreateExceptionFromDumpFile(dumpFile, hr);
        }

        private static ClrDiagnosticsException CreateExceptionFromDumpFile(string dumpFile, HResult hr)
        {
            ClrDiagnosticsException ex = new($"Could not load crash dump, HRESULT: {hr}", hr);
            ex.Data["DumpFile"] = dumpFile;
            return ex;
        }

        public DbgEngIDataReader(int processId, DEBUG_ATTACH attach, TimeSpan timeout)
        {
            DisplayName = $"{processId:x}";

            _dbgeng = IDebugClient.Create();
            DebugSystemObjects = (IDebugSystemObjects)_dbgeng;
            DebugClient = (IDebugClient)_dbgeng;
            DebugControl = (IDebugControl)_dbgeng;
            DebugDataSpaces = (IDebugDataSpaces)_dbgeng;
            DebugAdvanced = (IDebugAdvanced)_dbgeng;
            DebugSymbols = (IDebugSymbols)_dbgeng;

            HResult hr = DebugControl.AddEngineOptions(DEBUG_ENGOPT.INITIAL_BREAK);
            if (hr)
                hr = DebugClient.AttachProcess(processId, attach);

            if (hr)
                hr = DebugControl.WaitForEvent(timeout);

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

        ~DbgEngIDataReader()
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
                DebugClient.EndSession(DEBUG_END.ACTIVE_DETACH);
                DebugClient.DetachProcesses();
                _dbgeng?.Dispose();
            }
        }

        public bool IsThreadSafe => false;

        public int ProcessId => DebugSystemObjects.ProcessSystemId;

        public Architecture Architecture => _architecture ??= DebugControl.CpuType switch
        {
            ImageFileMachine.I386 => Architecture.X86,
            ImageFileMachine.AMD64 => Architecture.X64,
            ImageFileMachine.ARM or
            (ImageFileMachine)0x01c2 or                     // THUMB
            (ImageFileMachine)0x01c4 => Architecture.Arm,   // THUMB2
            (ImageFileMachine)0xAA64 => Architecture.Arm64, // ARM64
            (ImageFileMachine)0x6264 => (Architecture)6 /* Architecture.LoongArch64 */, // LOONGARCH64
            (ImageFileMachine)0x5064 => (Architecture)9 /* Architecture.RiscV64 */, // RISCV64
            _ => (Architecture)(-1)
        };

        public int PointerSize => _pointerSize ??= DebugControl.PointerSize;

        public void FlushCachedData()
        {
            _modules = null;
        }

        public bool GetThreadContext(uint systemId, uint contextFlags, Span<byte> context)
        {
            int curr = DebugSystemObjects.CurrentThreadId;
            try
            {
                if (DebugSystemObjects.GetThreadIdBySystemId((int)systemId, out int id) < 0)
                    return false;

                DebugSystemObjects.CurrentThreadId = id;
                return DebugAdvanced.GetThreadContext(context) >= 0;
            }
            finally
            {
                DebugSystemObjects.CurrentThreadId = curr;
            }
        }

        private ulong[] GetImageBases()
        {
            HResult hr = DebugSymbols.GetNumberModules(out int count, out _);

            if (!hr)
                return Array.Empty<ulong>();

            int index = 0;
            ulong[] bases = new ulong[count];
            for (int i = 0; i < count; ++i)
            {
                hr = DebugSymbols.GetImageBase(i, out ulong baseAddress);
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

            List<ModuleInfo> modules = new();
            HResult hr = DebugSymbols.GetModuleParameters(bases, moduleParams);
            if (hr)
            {
                for (int i = 0; i < bases.Length; ++i)
                {
                    DebugSymbols.GetModuleName(DEBUG_MODNAME.IMAGE, bases[i], out string moduleName);

                    unchecked
                    {
                        // On Linux, getting the typical version information requires scanning through a large chunk of the memory for
                        // a library looking for a particular string.  This can be incredibly slow when there is a large number of modules.
                        // Since ClrMD's ModuleInfo doesn't lazily calculate its Version, this means that the startup for Linux could be
                        // incredibly slow.  We should probably add a way to (optionally) lazily load the Version for modules to make this
                        // more pay for play, but until then we will simply only return the version of libcoreclr.so on Linux, as that's
                        // the bare minimum for functionality.
                        Version? version = null;
                        if (TargetPlatform == OSPlatform.Windows || moduleName.Contains("libcoreclr", StringComparison.OrdinalIgnoreCase))
                            GetVersionInfo(bases[i], moduleParams[i].Size);

                        ModuleInfo? module = ModuleInfo.TryCreate(this, bases[i], moduleName, (int)moduleParams[i].Size, (int)moduleParams[i].TimeDateStamp, version);
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
            if (!DebugDataSpaces.ReadVirtual(address, buffer, out int read))
                return 0;

            return read;
        }

        public Version? GetVersionInfo(ulong baseAddress, uint moduleSize)
        {
            if (TargetPlatform == OSPlatform.Windows)
            {
                if (!FindModuleIndex(baseAddress, out int index))
                    return null;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                try
                {
                    HResult hr = DebugSymbols.GetModuleVersionInformation(index, baseAddress, "\\\\\0", buffer);
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
            else
            {
                // GetModuleVersionInformation has a bug which we are working around here
                byte[] versionString = Encoding.ASCII.GetBytes(VersionString);

                if (DebugDataSpaces.Search(baseAddress, moduleSize, versionString, 1, out ulong offsetFound))
                {
                    byte[] bufferArray = ArrayPool<byte>.Shared.Rent(256);
                    Span<byte> buffer = bufferArray;
                    try
                    {
                        if (DebugDataSpaces.ReadVirtual(offsetFound + (uint)VersionString.Length, buffer, out int read))
                        {
                            string versionStr = Encoding.ASCII.GetString(buffer[0..read]);
                            int space = versionStr.IndexOf(' ');
                            if (space > 0)
                            {
                                versionStr = versionStr[0..space];
                                Version version = new(versionStr);
                                return version;
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(bufferArray);
                    }
                }

                return null;
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
                if (DebugSymbols.GetModuleByOffset(baseAddr, nextIndex, out index, out ulong claimedBaseAddr) < 0)
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
            DebugSystemObjects.GetNumberThreads(out int count);
            if (count == 0)
                return Array.Empty<uint>();

            uint[] result = new uint[count];
            HResult hr = DebugSystemObjects.GetThreadSystemIDs(result);
            if (hr)
                return result;

            return Array.Empty<uint>();
        }

        public ulong GetThreadTeb(uint osThreadId)
        {
            int curr = DebugSystemObjects.CurrentThreadId;
            try
            {
                HResult hr = DebugSystemObjects.GetThreadIdBySystemId((int)osThreadId, out int id);
                if (hr)
                {
                    DebugSystemObjects.CurrentThreadId = id;
                    hr = DebugSystemObjects.GetCurrentThreadTeb(out ulong teb);
                    if (hr)
                        return teb;
                }
            }
            finally
            {
                DebugSystemObjects.CurrentThreadId = curr;
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