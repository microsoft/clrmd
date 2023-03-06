using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrSubHeap
    {
        ulong Address { get; }
        MemoryRange AllocationContext { get; }
        bool HasBackgroundGC { get; }
        bool HasPinnedObjectHeap { get; }
        bool HasRegions { get; }
        IClrHeap Heap { get; }
        int Index { get; }
        ImmutableArray<IClrSegment> Segments { get; }
    }
}