using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA64
    {
        [FieldOffset(0)]
        public UInt64 ForwarderString; // PBYTE
        [FieldOffset(0)]
        public UInt64 Function; // PDWORD
        [FieldOffset(0)]
        public UInt64 Ordinal;
        [FieldOffset(0)]
        public UInt64 AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }
}