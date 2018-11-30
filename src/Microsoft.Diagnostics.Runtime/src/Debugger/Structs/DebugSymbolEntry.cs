using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SYMBOL_ENTRY
    {
        public ulong ModuleBase;
        public ulong Offset;
        public ulong Id;
        public ulong Arg64;
        public uint Size;
        public uint Flags;
        public uint TypeId;
        public uint NameSize;
        public uint Token;
        public SymTag Tag;
        public uint Arg32;
        public uint Reserved;
    }
}