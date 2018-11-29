using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_FILE_HEADER
    {
        [FieldOffset(0)]
        public UInt16 Machine;
        [FieldOffset(2)]
        public UInt16 NumberOfSections;
        [FieldOffset(4)]
        public UInt32 TimeDateStamp;
        [FieldOffset(8)]
        public UInt32 PointerToSymbolTable;
        [FieldOffset(12)]
        public UInt32 NumberOfSymbols;
        [FieldOffset(16)]
        public UInt16 SizeOfOptionalHeader;
        [FieldOffset(18)]
        public UInt16 Characteristics;
    }
}