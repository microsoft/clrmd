using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugClient : CallableCOMWrapper
    {
        internal static Guid IID_IDebugClient = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
        private IDebugClientVTable* VTable => (IDebugClientVTable*)_vtable;
        public DebugClient(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects system)
            : base(library, ref IID_IDebugClient, pUnk)
        {
            _sys = system;
            SuppressRelease();
        }

        private EndSessionDelegate _endSession;
        private DetatchProcessesDelegate _detatchProcesses;
        private AttachProcessDelegate _attachProcess;
        private OpenDumpFileDelegate _openDumpFile;
        private readonly DebugSystemObjects _sys;

        public void EndSession(DebugEnd mode)
        {
            InitDelegate(ref _endSession, VTable->EndSession);

            using IDisposable holder = _sys.Enter();
            int hr = _endSession(Self, mode);
            Debug.Assert(hr == 0);
        }

        public void DetatchProcesses()
        {
            InitDelegate(ref _detatchProcesses, VTable->DetachProcesses);

            using IDisposable holder = _sys.Enter();
            int hr = _detatchProcesses(Self);
            Debug.Assert(hr == 0);
        }

        public int AttachProcess(uint pid, DebugAttach flags)
        {
            InitDelegate(ref _attachProcess, VTable->AttachProcess);
            int hr = _attachProcess(Self, 0, pid, flags);

            _sys.Init();
            return hr;
        }

        public int OpenDumpFile(string dumpFile)
        {
            InitDelegate(ref _openDumpFile, VTable->OpenDumpFile);
            int hr = _openDumpFile(Self, dumpFile);

            _sys.Init();
            return hr;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EndSessionDelegate(IntPtr self, DebugEnd mode);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DetatchProcessesDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int AttachProcessDelegate(IntPtr self, ulong server, uint pid, DebugAttach AttachFlags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenDumpFileDelegate(IntPtr self, [In][MarshalAs(UnmanagedType.LPStr)] string file);
    }

#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051
#pragma warning disable CA1823

    internal struct IDebugClientVTable
    {
        public readonly IntPtr AttachKernel;
        public readonly IntPtr GetKernelConnectionOptions;
        public readonly IntPtr SetKernelConnectionOptions;
        public readonly IntPtr StartProcessServer;
        public readonly IntPtr ConnectProcessServer;
        public readonly IntPtr DisconnectProcessServer;
        public readonly IntPtr GetRunningProcessSystemIds;
        public readonly IntPtr GetRunningProcessSystemIdByExecutableName;
        public readonly IntPtr GetRunningProcessDescription;
        public readonly IntPtr AttachProcess;
        public readonly IntPtr CreateProcess;
        public readonly IntPtr CreateProcessAndAttach;
        public readonly IntPtr GetProcessOptions;
        public readonly IntPtr AddProcessOptions;
        public readonly IntPtr RemoveProcessOptions;
        public readonly IntPtr SetProcessOptions;
        public readonly IntPtr OpenDumpFile;
        public readonly IntPtr WriteDumpFile;
        public readonly IntPtr ConnectSession;
        public readonly IntPtr StartServer;
        public readonly IntPtr OutputServer;
        public readonly IntPtr TerminateProcesses;
        public readonly IntPtr DetachProcesses;
        public readonly IntPtr EndSession;
        public readonly IntPtr GetExitCode;
        public readonly IntPtr DispatchCallbacks;
        public readonly IntPtr ExitDispatch;
        public readonly IntPtr CreateClient;
        public readonly IntPtr GetInputCallbacks;
        public readonly IntPtr SetInputCallbacks;
        public readonly IntPtr GetOutputCallbacks;
        public readonly IntPtr SetOutputCallbacks;
        public readonly IntPtr GetOutputMask;
        public readonly IntPtr SetOutputMask;
        public readonly IntPtr GetOtherOutputMask;
        public readonly IntPtr SetOtherOutputMask;
        public readonly IntPtr GetOutputWidth;
        public readonly IntPtr SetOutputWidth;
        public readonly IntPtr GetOutputLinePrefix;
        public readonly IntPtr SetOutputLinePrefix;
        public readonly IntPtr GetIdentity;
        public readonly IntPtr OutputIdentity;
        public readonly IntPtr GetEventCallbacks;
        public readonly IntPtr SetEventCallbacks;
        public readonly IntPtr FlushCallbacks;
    }
}
