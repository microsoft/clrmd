using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_OFFSET_REGION
    {
        private readonly ulong _base;
        private readonly ulong _size;
    }
}