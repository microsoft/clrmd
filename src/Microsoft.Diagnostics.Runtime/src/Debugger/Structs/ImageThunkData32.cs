using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA32
    {
        [FieldOffset(0)]
        public uint ForwarderString; // PBYTE
        [FieldOffset(0)]
        public uint Function; // PDWORD
        [FieldOffset(0)]
        public uint Ordinal;
        [FieldOffset(0)]
        public uint AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }
}