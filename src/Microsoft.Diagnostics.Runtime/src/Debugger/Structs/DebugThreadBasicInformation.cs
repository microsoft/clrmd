using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_THREAD_BASIC_INFORMATION
    {
        public DEBUG_TBINFO Valid;
        public uint ExitStatus;
        public uint PriorityClass;
        public uint Priority;
        public ulong CreateTime;
        public ulong ExitTime;
        public ulong KernelTime;
        public ulong UserTime;
        public ulong StartOffset;
        public ulong Affinity;
    }
}