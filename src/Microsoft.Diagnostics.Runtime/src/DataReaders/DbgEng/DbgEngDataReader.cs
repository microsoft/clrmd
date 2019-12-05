// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DbgEng;

#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DbgEngDataReader : IDisposable, IDataReader
    {
        private static int s_totalInstanceCount;
        private static bool s_needRelease = true; //todo

        private DebugClient _client;
        private DebugControl _control;
        private DebugDataSpaces _spaces;
        private DebugAdvanced _advanced;
        private DebugSymbols _symbols;
        private DebugSystemObjects _systemObjects;

        private bool _disposed;

        private List<ModuleInfo> _modules;
        private int? _pointerSize;
        private bool? _minidump;
        private Architecture? _architecture;
        private static readonly RefCountedFreeLibrary _library = new RefCountedFreeLibrary(IntPtr.Zero);

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            IntPtr pClient = CreateIDebugClient();
            CreateClient(pClient);
            int hr = _client.OpenDumpFile(dumpFile);
            if (hr != 0)
            {
                var kind = (uint)hr == 0x80004005 ? ClrDiagnosticsExceptionKind.CorruptedFileOrUnknownFormat : ClrDiagnosticsExceptionKind.DebuggerError;
                throw new ClrDiagnosticsException($"Could not load crash dump, HRESULT: 0x{hr:x8}", kind, hr).AddData("DumpFile", dumpFile);
            }

            // This actually "attaches" to the crash dump.
            bool result = _control.WaitForEvent(0xffffffff);
            Debug.Assert(result);
        }

        public DbgEngDataReader(int pid, bool invasive, uint msecTimeout)
        {
            IntPtr client = CreateIDebugClient();
            CreateClient(client);

            DebugAttach attach = invasive ? DebugAttach.Default : DebugAttach.NonInvasive;
            _control.AddEngineOptions(DebugControl.INITIAL_BREAK);

            int hr = _client.AttachProcess((uint)pid, attach);

            if (hr == 0)
                hr = _control.WaitForEvent(msecTimeout) ? 0 : -1;

            if (hr == 1)
            {
                throw new TimeoutException("Break in did not occur within the allotted timeout.");
            }

            if (hr != 0)
            {
                if ((uint)hr == 0xd00000bb)
                    throw new InvalidOperationException("Mismatched architecture between this process and the target process.");

                throw new ClrDiagnosticsException($"Could not attach to process {pid:X}, HRESULT: 0x{hr:x8}", ClrDiagnosticsExceptionKind.DebuggerError, hr);
            }
        }

        public uint ProcessId => _systemObjects.GetProcessId();

        public bool IsFullMemoryAvailable
        {
            get
            {
                if (_minidump.HasValue)
                    return !_minidump.Value;

                DEBUG_CLASS_QUALIFIER qual = _control.GetDebuggeeClassQualifier();
                if (qual == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_SMALL_DUMP)
                {
                    DEBUG_FORMAT flags = _control.GetDumpFormat();
                    _minidump = (flags & DEBUG_FORMAT.USER_SMALL_FULL_MEMORY) == 0;
                    return !_minidump.Value;
                }

                _minidump = false;
                return true;
            }
        }

        public Architecture Architecture
        {
            get
            {
                if (_architecture.HasValue)
                    return _architecture.Value;

                IMAGE_FILE_MACHINE machineType = _control.GetEffectiveProcessorType();
                switch (machineType)
                {
                    case IMAGE_FILE_MACHINE.I386:
                        _architecture = Architecture.X86;
                        break;

                    case IMAGE_FILE_MACHINE.AMD64:
                        _architecture = Architecture.Amd64;
                        break;

                    case IMAGE_FILE_MACHINE.ARM:
                    case IMAGE_FILE_MACHINE.THUMB:
                    case IMAGE_FILE_MACHINE.THUMB2:
                        _architecture = Architecture.Arm;
                        break;

                    case IMAGE_FILE_MACHINE.ARM64:
                        _architecture = Architecture.Arm64;
                        break;

                    default:
                        _architecture = Architecture.Unknown;
                        break;
                }

                return _architecture.Value;
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
        [DllImport("dbgeng.dll")]
        public static extern int DebugCreate(ref Guid InterfaceId, out IntPtr Interface);

        public int PointerSize
        {
            get
            {
                _pointerSize = IntPtr.Size;
                if (_pointerSize.HasValue)
                    return _pointerSize.Value;

                _pointerSize = _control.IsPointer64Bit() ? 8 : 4;
                return _pointerSize.Value;
            }
        }

        public void FlushCachedData()
        {
            _modules = null;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
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

        public IList<ModuleInfo> EnumerateModules()
        {
            if (_modules != null)
                return _modules;

            ulong[] bases = GetImageBases();
            if (bases.Length == 0)
                return Array.Empty<ModuleInfo>();

            List<ModuleInfo> modules = new List<ModuleInfo>();
            if (_symbols.GetModuleParameters(bases, out DbgEng.DEBUG_MODULE_PARAMETERS[] mods))
            {
                for (int i = 0; i < bases.Length; ++i)
                {
                    string fn = _symbols.GetModuleNameStringWide(DebugModuleName.Image, i, bases[i]);
                    ModuleInfo info = new ModuleInfo(this, bases[i], mods[i].Size, mods[i].TimeDateStamp, fn);
                    modules.Add(info);
                }
            }

            _modules = modules;
            return modules;
        }

        private IntPtr CreateIDebugClient()
        {
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            int hr = DebugCreate(ref guid, out IntPtr ptr);
            Debug.Assert(hr == 0);

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

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            if (_spaces.QueryVirtual(addr, out MEMORY_BASIC_INFORMATION64 mem))
            {
                vq = new VirtualQueryData(mem.BaseAddress, mem.RegionSize);
                return true;
            }

            vq = default;
            return false;
        }

        public bool ReadMemory(ulong address, Span<byte> buffer, out int read)
        {
            read = _spaces.ReadVirtual(address, buffer);
            return read == buffer.Length;
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (_spaces.ReadVirtual(addr, buffer) != IntPtr.Size)
                return 0;

            return IntPtr.Size == 4 ? MemoryMarshal.Read<uint>(buffer) : MemoryMarshal.Read<ulong>(buffer);
        }

        public unsafe bool Read<T>(ulong addr, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (ReadMemory(addr, buffer, out _))
            {
                value = Unsafe.As<byte, T>(ref buffer[0]);
                return true;
            }

            value = default;
            return false;
        }

        public T ReadUnsafe<T>(ulong addr) where T : unmanaged
        {
            Read(addr, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (ReadMemory(address, buffer, out _))
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            version = default;

            if (!FindModuleIndex(baseAddress, out int index))
                return;

            version = _symbols.GetModuleVersionInformation(index, baseAddress);
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

        public IEnumerable<uint> EnumerateAllThreads() => _systemObjects.GetThreadIds();

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
            if (count == 0 && s_needRelease && disposing)
            {
                _client.EndSession(DebugEnd.ActiveDetatch);
                _client.DetatchProcesses();
            }

            // If there are no more debug instances, we can safely reset this variable
            // and start releasing newly created IDebug objects.
            if (count == 0)
                s_needRelease = true;
        }
    }
}