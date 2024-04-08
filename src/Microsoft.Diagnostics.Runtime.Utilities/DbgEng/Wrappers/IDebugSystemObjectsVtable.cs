// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct IDebugSystemObjectsVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        private readonly nint GetEventThread;
        private readonly nint GetEventProcess;
        public readonly delegate* unmanaged[Stdcall]<nint, out int, int> GetCurrentThreadId;
        public readonly delegate* unmanaged[Stdcall]<nint, int, int> SetCurrentThreadId;
        private readonly nint GetCurrentProcessId;
        private readonly nint SetCurrentProcessId;
        public readonly delegate* unmanaged[Stdcall]<nint, out int, int> GetNumberThreads;
        private readonly nint GetTotalNumberThreads;
        public readonly delegate* unmanaged[Stdcall]<nint, int, int, uint*, uint*, int> GetThreadIdsByIndex;
        private readonly nint GetThreadIdByProcessor;
        private readonly nint GetCurrentThreadDataOffset;
        private readonly nint GetThreadIdByDataOffset;
        public readonly delegate* unmanaged[Stdcall]<nint, out ulong, int> GetCurrentThreadTeb;
        private readonly nint GetThreadIdByTeb;
        private readonly nint GetCurrentThreadSystemId;
        public readonly delegate* unmanaged[Stdcall]<nint, int, out int, int> GetThreadIdBySystemId;
        private readonly nint GetCurrentThreadHandle;
        private readonly nint GetThreadIdByHandle;
        private readonly nint GetNumberProcesses;
        private readonly nint GetProcessIdsByIndex;
        private readonly nint GetCurrentProcessDataOffset;
        private readonly nint GetProcessIdByDataOffset;
        public readonly delegate* unmanaged[Stdcall]<nint, out ulong, int> GetCurrentProcessPeb;
        private readonly nint GetProcessIdByPeb;
        public readonly delegate* unmanaged[Stdcall]<nint, out int, int> GetCurrentProcessSystemId;
        private readonly nint GetProcessIdBySystemId;
        private readonly nint GetCurrentProcessHandle;
        private readonly nint GetProcessIdByHandle;
        private readonly nint GetCurrentProcessExecutableName;
        public readonly delegate* unmanaged[Stdcall]<nint, out uint, int> GetCurrentProcessUpTime;
        private readonly nint GetImplicitThreadDataOffset;
        private readonly nint SetImplicitThreadDataOffset;
        private readonly nint GetImplicitProcessDataOffset;
        private readonly nint SetImplicitProcessDataOffset;
        private readonly nint GetEventSystem;
        public readonly delegate* unmanaged[Stdcall]<nint, out int, int> GetCurrentSystemId;
        public readonly delegate* unmanaged[Stdcall]<nint, int, int> SetCurrentSystemId;
        private readonly nint GetNumberSystems;
        private readonly nint GetSystemIdsByIndex;
        private readonly nint GetTotalNumberThreadsAndProcesses;
        private readonly nint GetCurrentSystemServer;
        private readonly nint GetSystemByServer;
        private readonly nint GetCurrentSystemServerName;
        public readonly delegate* unmanaged[Stdcall]<nint, char*, int, out int, int> GetCurrentProcessExecutableNameWide;
        public readonly nint GetCurrentSystemServerNameWide;
    }
}