// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct IDebugClientVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        // IDebugClient
        private readonly nint AttachKernel;
        private readonly nint GetKernelConnectionOptions;
        private readonly nint SetKernelConnectionOptions;
        private readonly nint StartProcessServer;
        private readonly nint ConnectProcessServer;
        private readonly nint DisconnectProcessServer;
        private readonly nint GetRunningProcessSystemIds;
        private readonly nint GetRunningProcessSystemIdByExecutableName;
        private readonly nint GetRunningProcessDescription;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, uint, DEBUG_ATTACH, int> AttachProcess;
        private readonly nint CreateProcess;
        private readonly nint CreateProcessAndAttach;
        private readonly nint GetProcessOptions;
        private readonly nint AddProcessOptions;
        private readonly nint RemoveProcessOptions;
        private readonly nint SetProcessOptions;
        private readonly nint OpenDumpFile;
        private readonly nint WriteDumpFile;
        private readonly nint ConnectSession;
        private readonly nint StartServer;
        private readonly nint OutputServer;
        private readonly nint TerminateProcesses;
        public readonly delegate* unmanaged[Stdcall]<nint, int> DetachProcesses;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_END, int> EndSession;
        private readonly nint GetExitCode;
        private readonly nint DispatchCallbacks;
        private readonly nint ExitDispatch;
        private readonly nint CreateClient;
        private readonly nint GetInputCallbacks;
        private readonly nint SetInputCallbacks;
        private readonly nint GetOutputCallbacks;
        private readonly nint SetOutputCallbacks;
        private readonly nint GetOutputMask;
        private readonly nint SetOutputMask;
        private readonly nint GetOtherOutputMask;
        private readonly nint SetOtherOutputMask;
        private readonly nint GetOutputWidth;
        private readonly nint SetOutputWidth;
        private readonly nint GetOutputLinePrefix;
        private readonly nint SetOutputLinePrefix;
        private readonly nint GetIdentity;
        private readonly nint OutputIdentity;
        private readonly nint GetEventCallbacks;
        private readonly nint SetEventCallbacks;
        private readonly nint FlushCallbacks;

        // IDebugClient2
        private readonly nint WriteDumpFile2;
        private readonly nint AddDumpInformationFile;
        private readonly nint EndProcessServer;
        private readonly nint WaitForProcessServerEnd;
        private readonly nint IsKernelDebuggerEnabled;
        private readonly nint TerminateCurrentProcess;
        private readonly nint DetachCurrentProcess;
        private readonly nint AbandonCurrentProcess;

        // IDebugClient3
        private readonly nint GetRunningProcessSystemIdByExecutableNameWide;
        private readonly nint GetRunningProcessDescriptionWide;
        private readonly nint CreateProcessWide;
        private readonly nint CreateProcessAndAttachWide;

        // IDebugClient4
        public readonly delegate* unmanaged[Stdcall]<nint, char*, ulong, int> OpenDumpFileWide;
        public readonly delegate* unmanaged[Stdcall]<nint, nint, ulong, DEBUG_DUMP, DEBUG_FORMAT, nint, int> WriteDumpFileWide;
        private readonly nint AddDumpInformationFileWide;
        private readonly nint GetNumberDumpFiles;
        private readonly nint GetDumpFile;
        private readonly nint GetDumpFileWide;

        /* IDebugClient5 */
        private readonly nint AttachKernelWide;
        private readonly nint GetKernelConnectionOptionsWide;
        private readonly nint SetKernelConnectionOptionsWide;
        private readonly nint StartProcessServerWide;
        private readonly nint ConnectProcessServerWid;
        private readonly nint StartServerWide;
        private readonly nint OutputServersWide;
        public readonly delegate* unmanaged[Stdcall]<nint, nint*, int> GetOutputCallbacksWide;
        public readonly delegate* unmanaged[Stdcall]<nint, nint, int> SetOutputCallbacksWide;
        private readonly nint GetOutputLinePrefixWide;
        private readonly nint SetOutputLinePrefixWide;
        private readonly nint GetIdentityWide;
        private readonly nint OutputIdentityWide;
        public readonly delegate* unmanaged[Stdcall]<nint, nint*, int> GetEventCallbacksWide;
        public readonly delegate* unmanaged[Stdcall]<nint, nint, int> SetEventCallbacksWide;
        private readonly nint CreateProcess2;
        private readonly nint CreateProcess2Wide;
        private readonly nint CreateProcessAndAttach2;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, char*, DEBUG_CREATE_PROCESS_OPTIONS*, int, char*, char*, uint, DEBUG_ATTACH, int> CreateProcessAndAttach2Wide;
        private readonly nint PushOutputLinePrefix;
        private readonly nint PushOutputLinePrefixWide;
        private readonly nint PopOutputLinePrefix;
        private readonly nint GetNumberInputCallbacks;
        private readonly nint GetNumberOutputCallbacks;
        private readonly nint GetNumberEventCallbacks;
        private readonly nint GetQuitLockString;
        private readonly nint SetQuitLockString;
        private readonly nint GetQuitLockStringWide;
        private readonly nint SetQuitLockStringWide;
    }
}