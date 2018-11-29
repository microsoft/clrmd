using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WDBGEXTS_THREAD_OS_INFO
    {
        public UInt32 ThreadId;
        public UInt32 ExitStatus;
        public UInt32 PriorityClass;
        public UInt32 Priority;
        public UInt64 CreateTime;
        public UInt64 ExitTime;
        public UInt64 KernelTime;
        public UInt64 UserTime;
        public UInt64 StartOffset;
        public UInt64 Affinity;
    }
}