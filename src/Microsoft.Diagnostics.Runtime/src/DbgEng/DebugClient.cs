// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugClient : CallableCOMWrapper
    {
        internal static readonly Guid IID_IDebugClient = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");

        private readonly DebugSystemObjects _sys;

        public DebugClient(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects system)
            : base(library, IID_IDebugClient, pUnk)
        {
            _sys = system;
            SuppressRelease();
        }

        private ref readonly IDebugClientVTable VTable => ref Unsafe.AsRef<IDebugClientVTable>(_vtable);

        public void EndSession(DebugEnd mode)
        {
            InitDelegate(ref _endSession, VTable.EndSession);

            using IDisposable holder = _sys.Enter();
            int hr = _endSession(Self, mode);
            DebugOnly.Assert(hr == 0);
        }

        public void DetachProcesses()
        {
            InitDelegate(ref this._detachProcesses, VTable.DetachProcesses);

            using IDisposable holder = _sys.Enter();
            int hr = this._detachProcesses(Self);
            DebugOnly.Assert(hr == 0);
        }

        public HResult AttachProcess(uint pid, DebugAttach flags)
        {
            InitDelegate(ref _attachProcess, VTable.AttachProcess);
            HResult hr = _attachProcess(Self, 0, pid, flags);

            _sys.Init();
            return hr;
        }

        public HResult OpenDumpFile(string dumpFile)
        {
            InitDelegate(ref _openDumpFile, VTable.OpenDumpFile);
            return _openDumpFile(Self, dumpFile);
        }

        private EndSessionDelegate? _endSession;
        private DetachProcessesDelegate? _detachProcesses;
        private AttachProcessDelegate? _attachProcess;
        private OpenDumpFileDelegate? _openDumpFile;

        private delegate HResult EndSessionDelegate(IntPtr self, DebugEnd mode);
        private delegate HResult DetachProcessesDelegate(IntPtr self);
        private delegate HResult AttachProcessDelegate(IntPtr self, ulong server, uint pid, DebugAttach AttachFlags);
        private delegate HResult OpenDumpFileDelegate(IntPtr self, [In][MarshalAs(UnmanagedType.LPStr)] string file);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IDebugClientVTable
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
