// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
    internal unsafe class DbgEngDataReader : IDisposable, IDataReader2
    {
        private static int s_totalInstanceCount;
        private static bool s_needRelease = true;

        private IDebugDataSpaces _spaces;
        private IDebugDataSpaces2 _spaces2;
        private IDebugDataSpacesPtr _spacesPtr;
        private IDebugSymbols _symbols;
        private IDebugSymbols3 _symbols3;
        private IDebugControl2 _control;
        private IDebugAdvanced _advanced;
        private IDebugSystemObjects _systemObjects;
        private IDebugSystemObjects3 _systemObjects3;

        private uint _instance;
        private bool _disposed;

        private readonly byte[] _ptrBuffer = new byte[IntPtr.Size];
        private List<ModuleInfo> _modules;
        private bool? _minidump;

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        private void SetClientInstance()
        {
            if (_systemObjects3 != null && s_totalInstanceCount > 1)
                _systemObjects3.SetCurrentSystemId(_instance);
        }

        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            IDebugClient client = CreateIDebugClient();
            int hr = client.OpenDumpFile(dumpFile);

            if (hr != 0)
            {
                var kind = (uint)hr == 0x80004005 ? ClrDiagnosticsExceptionKind.CorruptedFileOrUnknownFormat : ClrDiagnosticsExceptionKind.DebuggerError;
                throw new ClrDiagnosticsException($"Could not load crash dump, HRESULT: 0x{hr:x8}", kind, hr).AddData("DumpFile", dumpFile);
            }

            CreateClient(client);

            // This actually "attaches" to the crash dump.
            _control.WaitForEvent(0, 0xffffffff);
        }

        public DbgEngDataReader(IDebugClient client)
        {
            //* We need to be very careful to not cleanup the IDebugClient interfaces
            // * (that is, detach from the target process) if we created this wrapper
            // * from a pre-existing IDebugClient interface.  Setting s_needRelease to
            // * false will keep us from *ever* explicitly detaching from any IDebug
            // * interface (even ones legitimately attached with other constructors),
            // * but this is the best we can do with DbgEng's design.  Better to leak
            // * a small amount of memory (and file locks) than detatch someone else's
            // * IDebug interface unexpectedly.
            // 
            CreateClient(client);
            s_needRelease = false;
        }

        public DbgEngDataReader(int pid, AttachFlag flags, uint msecTimeout)
        {
            IDebugClient client = CreateIDebugClient();
            CreateClient(client);

            DEBUG_ATTACH attach = flags == AttachFlag.Invasive ? DEBUG_ATTACH.DEFAULT : DEBUG_ATTACH.NONINVASIVE;
            int hr = _control.AddEngineOptions(DEBUG_ENGOPT.INITIAL_BREAK);

            if (hr == 0)
                hr = client.AttachProcess(0, (uint)pid, attach);

            if (hr == 0)
                hr = _control.WaitForEvent(0, msecTimeout);

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

        public uint ProcessId
        {
            get
            {
                int hr = _systemObjects.GetCurrentProcessSystemId(out uint id);
                return hr == 0 ? id : uint.MaxValue;
            }
        }

        public bool IsMinidump
        {
            get
            {
                if (_minidump != null)
                    return (bool)_minidump;

                SetClientInstance();

                _control.GetDebuggeeType(out DEBUG_CLASS cls, out DEBUG_CLASS_QUALIFIER qual);

                if (qual == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_SMALL_DUMP)
                {
                    _control.GetDumpFormatFlags(out DEBUG_FORMAT flags);
                    _minidump = (flags & DEBUG_FORMAT.USER_SMALL_FULL_MEMORY) == 0;
                    return _minidump.Value;
                }

                _minidump = false;
                return false;
            }
        }

        public Architecture GetArchitecture()
        {
            SetClientInstance();

            int hr = _control.GetExecutingProcessorType(out IMAGE_FILE_MACHINE machineType);
            if (hr != 0)
                throw new ClrDiagnosticsException($"Failed to get processor type, HRESULT: {hr:x8}", ClrDiagnosticsExceptionKind.DebuggerError, hr);

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

                default:
                    return Architecture.Unknown;
            }
        }

        private static IDebugClient CreateIDebugClient()
        {
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            DebugCreate(ref guid, out object obj);

            IDebugClient client = (IDebugClient)obj;
            return client;
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
        [DllImport("dbgeng.dll")]
        public static extern uint DebugCreate(ref Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);

        public void Close()
        {
            Dispose();
        }

        internal IDebugClient DebuggerInterface { get; private set; }

        public uint GetPointerSize()
        {
            SetClientInstance();
            int hr = _control.IsPointer64Bit();
            if (hr == 0)
                return 8;
            if (hr == 1)
                return 4;

            throw new ClrDiagnosticsException($"IsPointer64Bit failed, HRESULT: {hr:x8}", ClrDiagnosticsExceptionKind.DebuggerError, hr);
        }

        public void Flush()
        {
            _modules = null;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            GetThreadIdBySystemId(threadID, out uint id);

            SetCurrentThreadId(id);
            GetThreadContext(context, contextSize);

            return true;
        }

        private void GetThreadContext(IntPtr context, uint contextSize)
        {
            SetClientInstance();
            _advanced.GetThreadContext(context, contextSize);
        }

        internal int ReadVirtual(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            SetClientInstance();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length < bytesRequested)
                bytesRequested = buffer.Length;

            int res = _spaces.ReadVirtual(address, buffer, (uint)bytesRequested, out uint read);
            bytesRead = (int)read;
            return res;
        }

        private ulong[] GetImageBases()
        {
            if (GetNumberModules(out uint count, out uint unloadedCount) < 0)
                return null;

            List<ulong> bases = new List<ulong>((int)count);
            for (uint i = 0; i < count; ++i)
            {
                if (GetModuleByIndex(i, out ulong image) < 0)
                    continue;

                bases.Add(image);
            }

            return bases.ToArray();
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            if (_modules != null)
                return _modules;

            ulong[] bases = GetImageBases();
            if (bases == null || bases.Length == 0)
                return new ModuleInfo[0];

            DEBUG_MODULE_PARAMETERS[] mods = new DEBUG_MODULE_PARAMETERS[bases.Length];
            List<ModuleInfo> modules = new List<ModuleInfo>();
            HashSet<ulong> encounteredBases = new HashSet<ulong>();

            if (bases != null && CanEnumerateModules)
            {
                int hr = GetModuleParameters(bases.Length, bases, 0, mods);
                if (hr >= 0)
                {
                    for (int i = 0; i < bases.Length; ++i)
                    {
                        ModuleInfo info = new ModuleInfo(this)
                        {
                            TimeStamp = mods[i].TimeDateStamp,
                            FileSize = mods[i].Size,
                            ImageBase = bases[i]
                        };

                        StringBuilder sbpath = new StringBuilder();
                        if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], null, 0, out uint needed) >= 0 && needed > 1)
                        {
                            sbpath.EnsureCapacity((int)needed);
                            if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], sbpath, needed, out needed) >= 0)
                                info.FileName = sbpath.ToString();
                        }

                        modules.Add(info);
                    }
                }
            }

            _modules = modules;
            return modules;
        }

        public bool CanEnumerateModules => _symbols3 != null;

        internal int GetModuleParameters(int count, ulong[] bases, int start, DEBUG_MODULE_PARAMETERS[] mods)
        {
            SetClientInstance();
            return _symbols.GetModuleParameters((uint)count, bases, (uint)start, mods);
        }

        private void CreateClient(IDebugClient client)
        {
            DebuggerInterface = client;

            _spaces = (IDebugDataSpaces)DebuggerInterface;
            _spacesPtr = (IDebugDataSpacesPtr)DebuggerInterface;
            _symbols = (IDebugSymbols)DebuggerInterface;
            _control = (IDebugControl2)DebuggerInterface;

            // These interfaces may not be present in older DbgEng dlls.
            _spaces2 = DebuggerInterface as IDebugDataSpaces2;
            _symbols3 = DebuggerInterface as IDebugSymbols3;
            _advanced = DebuggerInterface as IDebugAdvanced;
            _systemObjects = DebuggerInterface as IDebugSystemObjects;
            _systemObjects3 = DebuggerInterface as IDebugSystemObjects3;

            Interlocked.Increment(ref s_totalInstanceCount);

            if (_systemObjects3 == null && s_totalInstanceCount > 1)
                throw new ClrDiagnosticsException("This version of DbgEng is too old to create multiple instances of DataTarget.", ClrDiagnosticsExceptionKind.DebuggerError);

            if (_systemObjects3 != null)
                _systemObjects3.GetCurrentSystemId(out _instance);
        }

        internal int GetModuleNameString(DEBUG_MODNAME Which, int Index, ulong Base, StringBuilder Buffer, uint BufferSize, out uint NameSize)
        {
            if (_symbols3 == null)
            {
                NameSize = 0;
                return -1;
            }

            SetClientInstance();
            return _symbols3.GetModuleNameString(Which, (uint)Index, Base, Buffer, BufferSize, out NameSize);
        }

        internal int GetNumberModules(out uint count, out uint unloadedCount)
        {
            if (_symbols3 == null)
            {
                count = 0;
                unloadedCount = 0;
                return -1;
            }

            SetClientInstance();
            return _symbols3.GetNumberModules(out count, out unloadedCount);
        }

        internal int GetModuleByIndex(uint i, out ulong image)
        {
            if (_symbols3 == null)
            {
                image = 0;
                return -1;
            }

            SetClientInstance();
            return _symbols3.GetModuleByIndex(i, out image);
        }

        internal int GetNameByOffsetWide(ulong offset, StringBuilder sb, int p, out uint size, out ulong disp)
        {
            SetClientInstance();
            return _symbols3.GetNameByOffsetWide(offset, sb, p, out size, out disp);
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            vq = new VirtualQueryData();
            if (_spaces2 == null)
                return false;

            SetClientInstance();
            int hr = _spaces2.QueryVirtual(addr, out MEMORY_BASIC_INFORMATION64 mem);
            vq.BaseAddress = mem.BaseAddress;
            vq.Size = mem.RegionSize;

            return hr == 0;
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return ReadVirtual(address, buffer, bytesRequested, out bytesRead) >= 0;
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            if (ReadVirtual(addr, _ptrBuffer, IntPtr.Size, out int read) != 0)
                return 0;

            fixed (byte* r = _ptrBuffer)
            {
                if (IntPtr.Size == 4)
                    return *((uint*)r);

                return *((ulong*)r);
            }
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            if (ReadVirtual(addr, _ptrBuffer, 4, out int read) != 0)
                return 0;

            fixed (byte* r = _ptrBuffer)
                return *((uint*)r);
        }

        internal void SetSymbolPath(string path)
        {
            SetClientInstance();
            _symbols.SetSymbolPath(path);
            _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, ".reload", DEBUG_EXECUTE.NOT_LOGGED);
        }

        internal int QueryVirtual(ulong addr, out MEMORY_BASIC_INFORMATION64 mem)
        {
            if (_spaces2 == null)
            {
                mem = new MEMORY_BASIC_INFORMATION64();
                return -1;
            }

            SetClientInstance();
            return _spaces2.QueryVirtual(addr, out mem);
        }

        internal int GetModuleByModuleName(string image, int start, out uint index, out ulong baseAddress)
        {
            SetClientInstance();
            return _symbols.GetModuleByModuleName(image, (uint)start, out index, out baseAddress);
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            version = default;

            int hr = _symbols.GetModuleByOffset(addr, 0, out uint index, out ulong baseAddr);
            if (hr != 0)
                return;

            hr = GetModuleVersionInformation(index, baseAddr, "\\", null, 0, out uint needed);
            if (hr != 0)
                return;

            byte[] buffer = new byte[needed];
            hr = GetModuleVersionInformation(index, baseAddr, "\\", buffer, needed, out needed);
            if (hr != 0)
                return;

            int minor = (ushort)Marshal.ReadInt16(buffer, 8);
            int major = (ushort)Marshal.ReadInt16(buffer, 10);
            int patch = (ushort)Marshal.ReadInt16(buffer, 12);
            int revision = (ushort)Marshal.ReadInt16(buffer, 14);
            
            version = new VersionInfo(major, minor, revision, patch);
        }

        internal int GetModuleVersionInformation(uint index, ulong baseAddress, string p, byte[] buffer, uint needed1, out uint needed2)
        {
            if (_symbols3 == null)
            {
                needed2 = 0;
                return -1;
            }

            SetClientInstance();
            return _symbols3.GetModuleVersionInformation(index, baseAddress, "\\", buffer, needed1, out needed2);
        }

        internal int GetModuleNameString(DEBUG_MODNAME requestType, uint index, ulong baseAddress, StringBuilder sbpath, uint needed1, out uint needed2)
        {
            if (_symbols3 == null)
            {
                needed2 = 0;
                return -1;
            }

            SetClientInstance();
            return _symbols3.GetModuleNameString(requestType, index, baseAddress, sbpath, needed1, out needed2);
        }

        internal int GetModuleParameters(uint Count, ulong[] Bases, uint Start, DEBUG_MODULE_PARAMETERS[] Params)
        {
            SetClientInstance();
            return _symbols.GetModuleParameters(Count, Bases, Start, Params);
        }

        internal void GetThreadIdBySystemId(uint threadID, out uint id)
        {
            SetClientInstance();
            _systemObjects.GetThreadIdBySystemId(threadID, out id);
        }

        internal void SetCurrentThreadId(uint id)
        {
            SetClientInstance();
            _systemObjects.SetCurrentThreadId(id);
        }

        internal void GetExecutingProcessorType(out IMAGE_FILE_MACHINE machineType)
        {
            SetClientInstance();
            _control.GetEffectiveProcessorType(out machineType);
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            SetClientInstance();

            bool res = _spacesPtr.ReadVirtual(address, buffer, (uint)bytesRequested, out uint read) >= 0;
            bytesRead = res ? (int)read : 0;
            return res;
        }

        public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            SetClientInstance();
            return _spaces.ReadVirtual(address, buffer, bytesRequested, out bytesRead);
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            SetClientInstance();

            int hr = _systemObjects.GetNumberThreads(out uint count);
            if (hr == 0)
            {
                uint[] sysIds = new uint[count];

                hr = _systemObjects.GetThreadIdsByIndex(0, count, null, sysIds);
                if (hr == 0)
                    return sysIds;
            }

            return new uint[0];
        }

        public ulong GetThreadTeb(uint thread)
        {
            SetClientInstance();

            ulong teb = 0;
            int hr = _systemObjects.GetCurrentThreadId(out uint id);
            bool haveId = hr == 0;

            if (_systemObjects.GetThreadIdBySystemId(thread, out id) == 0 && _systemObjects.SetCurrentThreadId(id) == 0)
                _systemObjects.GetCurrentThreadTeb(out teb);

            if (haveId)
                _systemObjects.SetCurrentThreadId(id);

            return teb;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            int count = Interlocked.Decrement(ref s_totalInstanceCount);
            if (count == 0 && s_needRelease && disposing)
            {
                if (_systemObjects3 != null)
                    _systemObjects3.SetCurrentSystemId(_instance);

                DebuggerInterface.EndSession(DEBUG_END.ACTIVE_DETACH);
                DebuggerInterface.DetachProcesses();
            }

            // If there are no more debug instances, we can safely reset this variable
            // and start releasing newly created IDebug objects.
            if (count == 0)
                s_needRelease = true;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            GetThreadIdBySystemId(threadID, out uint id);

            SetCurrentThreadId(id);
            fixed (byte* pContext = &context[0])
                GetThreadContext(new IntPtr(pContext), contextSize);

            return true;
        }
    }
}