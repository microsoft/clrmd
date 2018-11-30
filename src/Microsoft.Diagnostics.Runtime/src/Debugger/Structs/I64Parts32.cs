using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct I64PARTS32
    {
        [FieldOffset(0)]
        public uint LowPart;
        [FieldOffset(4)]
        public uint HighPart;
    }
}