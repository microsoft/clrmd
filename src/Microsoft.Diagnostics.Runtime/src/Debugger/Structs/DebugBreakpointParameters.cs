using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_BREAKPOINT_PARAMETERS
    {
        public ulong Offset;
        public uint Id;
        public DEBUG_BREAKPOINT_TYPE BreakType;
        public uint ProcType;
        public DEBUG_BREAKPOINT_FLAG Flags;
        public uint DataSize;
        public DEBUG_BREAKPOINT_ACCESS_TYPE DataAccessType;
        public uint PassCount;
        public uint CurrentPassCount;
        public uint MatchThread;
        public uint CommandSize;
        public uint OffsetExpressionSize;
    }
}