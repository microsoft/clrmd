using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrSubHeap
    {
        ulong Address { get; }
        MemoryRange AllocationContext { get; }
        bool HasBackgroundGC { get; }
        bool HasPinnedObjectHeap { get; }
        bool HasRegions { get; }
        ClrHeap Heap { get; }
        int Index { get; }
        ImmutableArray<IClrSegment> Segments { get; }
    }
}