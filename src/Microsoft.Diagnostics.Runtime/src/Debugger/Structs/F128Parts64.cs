using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct F128PARTS64
    {
        [FieldOffset(0)]
        public UInt64 LowPart;
        [FieldOffset(8)]
        public UInt64 HighPart;
    }
}