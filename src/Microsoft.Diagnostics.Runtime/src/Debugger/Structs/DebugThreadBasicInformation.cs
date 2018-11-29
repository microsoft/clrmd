using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_THREAD_BASIC_INFORMATION
    {
        public DEBUG_TBINFO Valid;
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