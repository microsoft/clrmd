using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct LANGANDCODEPAGE
    {
        [FieldOffset(0)]
        public UInt16 wLanguage;
        [FieldOffset(2)]
        public UInt16 wCodePage;
    }
}