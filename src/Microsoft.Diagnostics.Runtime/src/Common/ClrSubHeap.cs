using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The managed heap in CLR is made up of a number of logical "heaps".  When using
    /// Workstation GC, the managed heap has only one logical "heap".  When using Server GC,
    /// there can be many of them.  This class tracks information about logical heaps.
    /// </summary>
    public class ClrSubHeap
    {
        public ClrSubHeap(ClrHeap clrHeap, int index, ulong address, in HeapDetails heap, IClrSubHeapHelpers helpers)
        {
            Address = address;
            Index = index;
            Allocated = heap.Allocated;
            MarkArray = heap.MarkArray;
            State = (GCState)(ulong)heap.CurrentGCState;
            NextSweepObject = heap.NextSweepObj;
            SavedSweepEphemeralSegment = heap.SavedSweepEphemeralSeg;
            SavedSweepEphemeralStart = heap.SavedSweepEphemeralStart;
            BackgroundSavedLowestAddress = heap.BackgroundSavedLowestAddress;
            BackgroundSavedHighestAddress = heap.BackgroundSavedHighestAddress;
            EphemeralHeapSegment = heap.EphemeralHeapSegment;
            LowestAddress = heap.LowestAddress;
            HighestAddress = heap.HighestAddress;
            CardTable = heap.CardTable;

            int genTableSize = helpers.GenerationTableSize;

            // Use the expanded GenerationTable if ISOSDacInterface8 exists.
            GenerationData[] genData = helpers.TryGetGenerationData(Address) ?? heap.GenerationTable;
            GenerationTable = genData.Select(r => new ClrGenerationData(r)).ToImmutableArray();

            ClrDataAddress[] finalization = helpers.TryGetFinalizationPointers(Address) ?? heap.FinalizationFillPointers;
            FinalizationPointers = finalization.Select(r => (ulong)r).ToImmutableArray();

            HasRegions = GenerationTable.Length >= 2 && GenerationTable[0].StartSegment != GenerationTable[1].StartSegment;
            HasPinnedObjectHeap = genTableSize > 4;

            FinalizerQueueRoots = new MemoryRange(heap.FQRootsStart, heap.FQRootsStop);
            FinalizerQueueObjects = new MemoryRange(heap.FQAllObjectsStart, heap.FQAllObjectsStop);
            AllocationContext = new MemoryRange(heap.EphemeralAllocContextPtr, heap.EphemeralAllocContextLimit);

            Segments = EnumerateSegments(clrHeap, helpers).ToImmutableArray();
        }

        private IEnumerable<ClrSegment> EnumerateSegments(ClrHeap heap, IClrSubHeapHelpers helpers)
        {
            HashSet<ulong> seen = new() { 0 };
            IEnumerable<ClrSegment> segments = EnumerateSegments(heap, helpers, 3, seen);
            segments = segments.Concat(EnumerateSegments(heap, helpers, 2, seen));
            if (HasRegions)
            {
                segments = segments.Concat(EnumerateSegments(heap, helpers, 1, seen));
                segments = segments.Concat(EnumerateSegments(heap, helpers, 0, seen));
            }

            if (GenerationTable.Length > 4)
                segments = segments.Concat(EnumerateSegments(heap, helpers, 4, seen));

            return segments;
        }

        private IEnumerable<ClrSegment> EnumerateSegments(ClrHeap heap, IClrSubHeapHelpers helpers, int generation, HashSet<ulong> seen)
        {
            ulong address = GenerationTable[generation].StartSegment;

            while (address != 0 && seen.Add(address))
            {
                Console.WriteLine($"heap:{Index}, address:{address:x} gen:{generation}");
                ClrSegment? segment = helpers.CreateSegment(heap, address, generation, this);

                if (segment is null)
                    break;

                yield return segment;
                address = segment.Next;
            }
        }

        public ImmutableArray<ClrSegment> Segments { get; }

        public MemoryRange FinalizerQueueRoots { get; }
        public MemoryRange FinalizerQueueObjects { get; }
        public MemoryRange AllocationContext { get; }

        public int Index { get; }

        public bool HasPinnedObjectHeap { get;  }
        public bool HasRegions { get; }
        public bool HasBackgroundGC { get; }

        public ulong Address { get; }
        
        public ulong Allocated { get; }
        public ulong MarkArray { get; }

        internal GCState State { get; }

        internal ulong NextSweepObject { get; }
        internal ulong SavedSweepEphemeralSegment { get; }
        internal ulong SavedSweepEphemeralStart { get; }
        internal ulong BackgroundSavedLowestAddress { get; }
        internal ulong BackgroundSavedHighestAddress { get; }

        public ImmutableArray<ClrGenerationData> GenerationTable { get; }
        public ulong EphemeralHeapSegment { get; }

        public ImmutableArray<ulong> FinalizationPointers { get; }

        public ulong LowestAddress { get; }
        public ulong HighestAddress { get; }
        public ulong CardTable { get; }

        internal enum GCState
        {
            Marking,
            Planning,
            Free
        }
    }
}
