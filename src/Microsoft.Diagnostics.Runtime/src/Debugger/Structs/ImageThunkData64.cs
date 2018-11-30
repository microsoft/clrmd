using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA64
    {
        [FieldOffset(0)]
        public ulong ForwarderString; // PBYTE
        [FieldOffset(0)]
        public ulong Function; // PDWORD
        [FieldOffset(0)]
        public ulong Ordinal;
        [FieldOffset(0)]
        public ulong AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }
}