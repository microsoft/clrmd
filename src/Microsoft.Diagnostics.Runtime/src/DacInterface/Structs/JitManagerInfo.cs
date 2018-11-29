using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct JitManagerInfo
    {
        public readonly ulong Address;
        public readonly CodeHeapType Type;
        public readonly ulong HeapList;
    }
}