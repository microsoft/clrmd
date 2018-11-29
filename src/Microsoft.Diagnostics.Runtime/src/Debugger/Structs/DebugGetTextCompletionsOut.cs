using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_GET_TEXT_COMPLETIONS_OUT
    {
        public DEBUG_GET_TEXT_COMPLETIONS Flags;
        public UInt32 ReplaceIndex;
        public UInt32 MatchCount;
        public UInt32 Reserved1;
        public UInt64 Reserved2;
        public UInt64 Reserved3;
    }
}