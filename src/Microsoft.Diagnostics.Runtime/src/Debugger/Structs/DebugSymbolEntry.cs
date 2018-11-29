using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SYMBOL_ENTRY
    {
        public UInt64 ModuleBase;
        public UInt64 Offset;
        public UInt64 Id;
        public UInt64 Arg64;
        public UInt32 Size;
        public UInt32 Flags;
        public UInt32 TypeId;
        public UInt32 NameSize;
        public UInt32 Token;
        public SymTag Tag;
        public UInt32 Arg32;
        public UInt32 Reserved;
    }
}