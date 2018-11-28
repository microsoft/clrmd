using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct IMAGE_DOS_HEADER
    {
        public const short IMAGE_DOS_SIGNATURE = 0x5A4D; // MZ.  
        [FieldOffset(0)]
        public short e_magic;
        [FieldOffset(60)]
        public int e_lfanew; // Offset to the IMAGE_FILE_HEADER
    }
}