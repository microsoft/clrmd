using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SYMBOL_PARAMETERS
    {
        public UInt64 Module;
        public UInt32 TypeId;
        public UInt32 ParentSymbol;
        public UInt32 SubElements;
        public DEBUG_SYMBOL Flags;
        public UInt64 Reserved;
    }
}