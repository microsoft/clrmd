// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DbgEng;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DbgEngDataReader : IDisposable, IDataReader
    {
        private static int s_totalInstanceCount;
        private static bool s_needRelease = true;

        private int? _pointerSize;

        private DebugClient _client;
        private DebugControl _control;
        private DebugDataSpaces _spaces;
        private DebugAdvanced _advanced;
        private DebugSymbols _symbols;
        private DebugSystemObjects _systemObjects;

        private int _instance;
        private bool _disposed;

        private List<ModuleInfo> _modules;
        private bool? _minidump;
        private static readonly RefCountedFreeLibrary _library = new RefCountedFreeLibrary(IntPtr.Zero);

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        private void SetClientInstance()
        {
            _systemObjects.SetCurrentSystemId(_instance);
        }

        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            IntPtr pClient = CreateIDebugClient();
            using DebugClient client = new DebugClient(_library, pClient);
            int hr = client.OpenDumpFile(dumpFile);

            if (hr != 0)
            {
                var kind = (uint)hr == 0x80004005 ? ClrDiagnosticsExceptionKind.CorruptedFileOrUnknownFormat : ClrDiagnosticsExceptionKind.DebuggerError;
                throw new ClrDiagnosticsException($"Could not load crash dump, HRESULT: 0x{hr:x8}", kind, hr).AddData("DumpFile", dumpFile);
            }

            CreateClient(pClient);


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

                throw new ClrDiagnosticsException($"Could not attach to pid {pid:X}, HRESULT: 0x{hr:x8}", ClrDiagnosticsExceptionKind.DebuggerError, hr);
            }
        }

        public uint ProcessId => _systemObjects.GetProcessId();

        public bool IsMinidump
        {
            get
            {
                if (_minidump != null)
                    return (bool)_minidump;

                SetClientInstance();

                DEBUG_CLASS_QUALIFIER qual = _control.GetDebuggeeClassQualifier();
                if (qual == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_SMALL_DUMP)
                {
                    DEBUG_FORMAT flags = _control.GetDumpFormat();
                    _minidump = (flags & DEBUG_FORMAT.USER_SMALL_FULL_MEMORY) == 0;
                    return _minidump.Value;
                }

                _minidump = false;
                return false;
            }
        }

        public Architecture Architecture
        {
            get
            {
                SetClientInstance();
                IMAGE_FILE_MACHINE machineType = _control.GetEffectiveProcessorType();
                switch (machineType)
                {
                    case IMAGE_FILE_MACHINE.I386:
                        return Architecture.X86;

                    case IMAGE_FILE_MACHINE.AMD64:
                        return Architecture.Amd64;

                    case IMAGE_FILE_MACHINE.ARM:
                    case IMAGE_FILE_MACHINE.THUMB:
                    case IMAGE_FILE_MACHINE.THUMB2:
                        return Architecture.Arm;

                    case IMAGE_FILE_MACHINE.ARM64:
                        return Architecture.Arm64;

                    default:
                        return Architecture.Unknown;
                }
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
        [DllImport("dbgeng.dll")]
        public static extern int DebugCreate(ref Guid InterfaceId, out IntPtr Interface);



        public int PointerSize
        {
            get
            {
                if (_pointerSize.HasValue)
                    return _pointerSize.Value;

                _pointerSize = _control.IsPointer64Bit() ? 8 : 4;
                return _pointerSize.Value;
            }
        }

        public void ClearCachedData()
        {
            _modules = null;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            SetClientInstance();
            uint id = _systemObjects.GetThreadIdBySystemId(threadID);
            _systemObjects.SetCurrentThread(id);
            return _advanced.GetThreadContext(context);
        }

        internal int ReadVirtual(ulong address, Span<byte> buffer)
        {
            SetClientInstance();
            return _spaces.ReadVirtual(address, buffer);
        }

        private ulong[] GetImageBases()
        {
            SetClientInstance();
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
            if (GetModuleParameters(bases, out DbgEng.DEBUG_MODULE_PARAMETERS[] mods))
            {
                for (int i = 0; i < bases.Length; ++i)
                {
                    ModuleInfo info = new ModuleInfo(this)
                    {
                        TimeStamp = mods[i].TimeDateStamp,
                        FileSize = mods[i].Size,
                        ImageBase = bases[i],
                        FileName = GetModuleNameString(DebugModuleName.Image, i, bases[i])
                    };

                    modules.Add(info);
                }
            }

            _modules = modules;
            return modules;
        }

        internal bool GetModuleParameters(ulong[] bases, out DbgEng.DEBUG_MODULE_PARAMETERS[] mods)
        {
            SetClientInstance();
            return _symbols.GetModuleParameters(bases, out mods);
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
            _client = new DebugClient(_library, ptr);
            _control = new DebugControl(_library, ptr);
            _spaces = new DebugDataSpaces(_library, ptr);
            _advanced = new DebugAdvanced(_library, ptr);
            _symbols = new DebugSymbols(_library, ptr);
            _systemObjects = new DebugSystemObjects(_library, ptr);

            Interlocked.Increment(ref s_totalInstanceCount);
            _instance = _systemObjects.GetCurrentSystemId();
        }

        internal string GetModuleNameString(DebugModuleName which, int index, ulong imgBase)
        {
            SetClientInstance();
            return _symbols.GetModuleNameStringWide(which, index, imgBase);
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            SetClientInstance();
            if (_spaces.QueryVirtual(addr, out MEMORY_BASIC_INFORMATION64 mem))
            {
                vq = new VirtualQueryData()
                {
                    BaseAddress = mem.BaseAddress,
                    Size = mem.RegionSize
                };

                return true;
            }

            vq = default;
            return false;
        }

        public bool ReadMemory(ulong address, Span<byte> buffer, out int read)
        {
            read = ReadVirtual(address, buffer);
            return read == buffer.Length;
        }
        
        public ulong ReadPointerUnsafe(ulong addr)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (ReadVirtual(addr, buffer) != IntPtr.Size)
                return 0;

            return IntPtr.Size == 4 ? MemoryMarshal.Read<uint>(buffer) : MemoryMarshal.Read<ulong>(buffer);
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            Span<byte> buffer = stackalloc byte[4];
            if (ReadVirtual(addr, buffer) != 4)
                return 0;

            return MemoryMarshal.Read<uint>(buffer);
        }


        public void GetVersionInfo(ulong baseAddr, out VersionInfo version)
        {
            version = default;

            if (!FindModuleIndex(baseAddr, out int index))
                return;

            version = _symbols.GetModuleVersionInformation(index, baseAddr);
        }

        private bool FindModuleIndex(ulong baseAddr, out int index)
        {
            /* GetModuleByOffset returns the first module (from startIndex) which
             * includes baseAddr.
             * However when loading 64-bit dumps of 32-bit processes it seems that
             * the module sizes are sometimes wrong, which may cause a wrong module
             * to be found because it overlaps the beginning of the queried module,
             * so search until we find a module that actually has the correct
             * baseAddr*/
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


        public IEnumerable<uint> EnumerateAllThreads()
        {
            SetClientInstance();
            return _systemObjects.GetThreadIds();
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
            if (count == 0 && s_needRelease && disposing)
            {
                _systemObjects.SetCurrentSystemId(_instance);
                _client.EndSession(DebugEnd.ActiveDetatch);
                _client.DetatchProcesses();

                _client.Dispose();
                _control.Dispose();
                _advanced.Dispose();
                _systemObjects.Dispose();
                _spaces.Dispose();
                _symbols.Dispose();
            }

            // If there are no more debug instances, we can safely reset this variable
            // and start releasing newly created IDebug objects.
            if (count == 0)
                s_needRelease = true;
        }

        private static void AssertSuccess(int hr)
        {
            Debug.Assert(hr >= 0);
        }
    }
}