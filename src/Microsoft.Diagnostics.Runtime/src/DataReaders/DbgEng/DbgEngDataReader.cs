// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DbgEng;
using Microsoft.Diagnostics.Runtime.Utilities;

#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DbgEngDataReader : IDisposable, IDataReader
    {
        private static int s_totalInstanceCount;

        private DebugClient _client = null!;
        private DebugControl _control = null!;
        private DebugDataSpaces _spaces = null!;
        private DebugAdvanced _advanced = null!;
        private DebugSymbols _symbols = null!;
        private DebugSystemObjects _systemObjects = null!;

        private bool _disposed;

        private List<ModuleInfo>? _modules;
        private int? _pointerSize;
        private Architecture? _architecture;
        private static readonly RefCountedFreeLibrary _library = new RefCountedFreeLibrary(IntPtr.Zero);

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => OSPlatform.Windows;

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        public DbgEngDataReader(string dumpFile, Stream stream)
            : this(dumpFile)
        {
            stream?.Dispose();
        }

        public DbgEngDataReader(IntPtr pDebugClient)
        {
            if (pDebugClient == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pDebugClient));

            DisplayName = $"DbgEng, IDebugClient={pDebugClient.ToInt64():x}";
            CreateClient(pDebugClient);
            _systemObjects.Init();
        }

        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            DisplayName = dumpFile;

            IntPtr pClient = CreateIDebugClient();
            CreateClient(pClient);
            HResult hr = _client.OpenDumpFile(dumpFile);
            if (hr != 0)
            {
                const int STATUS_MAPPED_FILE_SIZE_ZERO = unchecked((int)0xC000011E);

                if (hr == HResult.E_INVALIDARG || hr == (STATUS_MAPPED_FILE_SIZE_ZERO | 0x10000000))
                    throw new InvalidDataException($"'{dumpFile}' is not a crash dump.");

                throw new ClrDiagnosticsException($"Could not load crash dump, HRESULT: {hr}", hr).AddData("DumpFile", dumpFile);
            }

            // This actually "attaches" to the crash dump.
            HResult result = _control.WaitForEvent(0xffffffff);
            _systemObjects.Init();
            DebugOnly.Assert(result);
        }

        public DbgEngDataReader(int processId, bool invasive, uint msecTimeout)
        {
            DisplayName = $"{processId:x}";

            IntPtr client = CreateIDebugClient();
            CreateClient(client);

            DebugAttach attach = invasive ? DebugAttach.Default : DebugAttach.NonInvasive;
            _control.AddEngineOptions(DebugControl.INITIAL_BREAK);

            HResult hr = _client.AttachProcess((uint)processId, attach);

            if (hr)
                hr = _control.WaitForEvent(msecTimeout);

            if (hr == HResult.S_FALSE)
            {
                throw new TimeoutException("Break in did not occur within the allotted timeout.");
            }

            if (hr != 0)
            {
                if ((uint)hr.Value == 0xd00000bb)
                    throw new InvalidOperationException("Mismatched architecture between this process and the target process.");

                if (!WindowsFunctions.IsProcessRunning(processId))
                    throw new ArgumentException($"Process {processId} is not running.");

                throw new ArgumentException($"Could not attach to process {processId}, HRESULT: 0x{hr:x}");
            }
        }

        public bool IsThreadSafe => true; // Enforced by Debug* wrappers.

        public uint ProcessId => _systemObjects.GetProcessId();

        public Architecture Architecture
        {
            get
            {
                if (_architecture is Architecture architecture)
                    return architecture;

                IMAGE_FILE_MACHINE machineType = _control.GetEffectiveProcessorType();
                switch (machineType)
                {
                    case IMAGE_FILE_MACHINE.I386:
                        architecture = Architecture.X86;
                        break;

                    case IMAGE_FILE_MACHINE.AMD64:
                        architecture = Architecture.Amd64;
                        break;

                    case IMAGE_FILE_MACHINE.ARM:
                    case IMAGE_FILE_MACHINE.THUMB:
                    case IMAGE_FILE_MACHINE.THUMB2:
                        architecture = Architecture.Arm;
                        break;

                    case IMAGE_FILE_MACHINE.ARM64:
                        architecture = Architecture.Arm64;
                        break;

                    default:
                        architecture = Architecture.Unknown;
                        break;
                }

                _architecture = architecture;
                return architecture;
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
        [DllImport("dbgeng.dll")]
        public static extern int DebugCreate(in Guid InterfaceId, out IntPtr Interface);

        public int PointerSize
        {
            get
            {
                _pointerSize = IntPtr.Size;
                if (_pointerSize is int pointerSize)
                    return pointerSize;

                _pointerSize = pointerSize = _control.IsPointer64Bit() ? sizeof(long) : sizeof(int);
                return pointerSize;
            }
        }

        public void FlushCachedData()
        {
            _modules = null;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            uint id = _systemObjects.GetThreadIdBySystemId(threadID);
            _systemObjects.SetCurrentThread(id);
            return _advanced.GetThreadContext(context);
        }

        private ulong[] GetImageBases()
        {
            int count = _symbols.GetNumberModules();

            List<ulong> bases = new List<ulong>(count);
            for (int i = 0; i < count; ++i)
            {
                ulong image = _symbols.GetModuleByIndex(i);
                if (image != 0)
                    bases.Add(image);
            }

            return bases.ToArray();
        }

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_modules != null)
                return _modules;

            ulong[] bases = GetImageBases();
            if (bases.Length == 0)
                return Enumerable.Empty<ModuleInfo>();

            List<ModuleInfo> modules = new List<ModuleInfo>();
            if (_symbols.GetModuleParameters(bases, out DEBUG_MODULE_PARAMETERS[] mods))
            {
                for (int i = 0; i < bases.Length; ++i)
                {
                    string? fn = _symbols.GetModuleNameStringWide(DebugModuleName.Image, i, bases[i]);
                    ModuleInfo info = new ModuleInfo(bases[i], fn, true, mods[i].Size, mods[i].TimeDateStamp, ImmutableArray<byte>.Empty);
                    modules.Add(info);
                }
            }

            _modules = modules;
            return modules;
        }

        private IntPtr CreateIDebugClient()
        {
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            int hr = DebugCreate(guid, out IntPtr ptr);
            DebugOnly.Assert(hr == 0);

            return ptr;
        }

        private void CreateClient(IntPtr ptr)
        {
            _systemObjects = new DebugSystemObjects(_library, ptr);
            _client = new DebugClient(_library, ptr, _systemObjects);
            _control = new DebugControl(_library, ptr, _systemObjects);
            _spaces = new DebugDataSpaces(_library, ptr, _systemObjects);
            _advanced = new DebugAdvanced(_library, ptr, _systemObjects);
            _symbols = new DebugSymbols(_library, ptr, _systemObjects);

            _client.SuppressRelease();
            _control.SuppressRelease();
            _spaces.SuppressRelease();
            _advanced.SuppressRelease();
            _symbols.SuppressRelease();
            _systemObjects.SuppressRelease();

            Interlocked.Increment(ref s_totalInstanceCount);
        }

        public bool Read(ulong address, Span<byte> buffer, out int read)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            read = _spaces.ReadVirtual(address, buffer);
            return read > 0;
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }

        public unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer, out int size) && size == sizeof(T))
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (Read(address, buffer, out int size) && size == IntPtr.Size)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress) => ImmutableArray<byte>.Empty;

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            version = default;

            if (!FindModuleIndex(baseAddress, out int index))
                return false;

            version = _symbols.GetModuleVersionInformation(index, baseAddress);
            return true;
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
                if (!_symbols.GetModuleByOffset(baseAddr, nextIndex, out index, out ulong claimedBaseAddr))
                {
                    index = 0;
                    return false;
                }

                if (claimedBaseAddr == baseAddr)
                    return true;

                nextIndex = index + 1;
            }
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

            int count = Interlocked.Decrement(ref s_totalInstanceCount);
            if (count == 0 && disposing)
            {
                _client.EndSession(DebugEnd.ActiveDetach);
                _client.DetachProcesses();
            }
        }
    }
}
