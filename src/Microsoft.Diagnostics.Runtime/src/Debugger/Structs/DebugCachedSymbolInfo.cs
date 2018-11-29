using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_CACHED_SYMBOL_INFO
    {
        public UInt64 ModBase;
        public UInt64 Arg1;
        public UInt64 Arg2;
        public UInt32 Id;
        public UInt32 Arg3;
    }
}