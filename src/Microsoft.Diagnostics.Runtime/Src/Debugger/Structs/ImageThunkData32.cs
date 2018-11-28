using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA32
    {
        [FieldOffset(0)]
        public UInt32 ForwarderString; // PBYTE
        [FieldOffset(0)]
        public UInt32 Function; // PDWORD
        [FieldOffset(0)]
        public UInt32 Ordinal;
        [FieldOffset(0)]
        public UInt32 AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }
}