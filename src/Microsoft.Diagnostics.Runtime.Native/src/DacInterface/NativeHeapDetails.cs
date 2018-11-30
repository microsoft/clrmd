using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeHeapDetails
    {
        public readonly ulong Address;
        public readonly ulong AllocAllocated;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly GenerationData[] GenerationTable;
        public readonly ulong EphemeralHeapSegment;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public readonly ulong[] FinalizationFillPointers;

        public readonly ulong LowestAddress;
        public readonly ulong HighestAddress;
        public readonly ulong CardTable;
    }
}