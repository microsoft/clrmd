using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_GET_TEXT_COMPLETIONS_IN
    {
        public DEBUG_GET_TEXT_COMPLETIONS Flags;
        public UInt32 MatchCountLimit;
        public UInt64 Reserved0;
        public UInt64 Reserved1;
        public UInt64 Reserved2;
    }
}