using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct LANGANDCODEPAGE
    {
        [FieldOffset(0)]
        public ushort wLanguage;
        [FieldOffset(2)]
        public ushort wCodePage;
    }
}