using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_BREAKPOINT_PARAMETERS
    {
        public UInt64 Offset;
        public UInt32 Id;
        public DEBUG_BREAKPOINT_TYPE BreakType;
        public UInt32 ProcType;
        public DEBUG_BREAKPOINT_FLAG Flags;
        public UInt32 DataSize;
        public DEBUG_BREAKPOINT_ACCESS_TYPE DataAccessType;
        public UInt32 PassCount;
        public UInt32 CurrentPassCount;
        public UInt32 MatchThread;
        public UInt32 CommandSize;
        public UInt32 OffsetExpressionSize;
    }
}