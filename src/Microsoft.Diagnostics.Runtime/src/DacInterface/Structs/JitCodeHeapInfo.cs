using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct JitCodeHeapInfo : ICodeHeap
    {
        public readonly CodeHeapType Type;
        public readonly ulong Address;
        public readonly ulong CurrentAddress;

        CodeHeapType ICodeHeap.Type => Type;
        ulong ICodeHeap.Address => Address;
    }
}