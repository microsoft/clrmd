using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_NT_HEADERS64
    {
        [FieldOffset(0)]
        public uint Signature;
        [FieldOffset(4)]
        public IMAGE_FILE_HEADER FileHeader;
        [FieldOffset(24)]
        public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
    }
}