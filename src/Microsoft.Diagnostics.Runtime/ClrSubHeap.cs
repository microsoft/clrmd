// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Interfaces;
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
    public class ClrSubHeap : IClrSubHeap
    {
        internal ClrSubHeap(IClrHeapHelpers helpers, ClrHeap clrHeap, int index, ulong address, in HeapDetails heap, IEnumerable<GenerationData> genData, IEnumerable<ulong> finalizationPointers)
        {
            Heap = clrHeap;
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

            GenerationTable = genData.Select(data => new ClrGenerationData(data)).ToImmutableArray();
            FinalizationPointers = finalizationPointers.ToImmutableArray();

            HasRegions = GenerationTable.Length >= 2 && GenerationTable[0].StartSegment != GenerationTable[1].StartSegment;
            HasPinnedObjectHeap = GenerationTable.Length > 4;

            FinalizerQueueRoots = new MemoryRange(heap.FQRootsStart, heap.FQRootsStop);
            FinalizerQueueObjects = new MemoryRange(heap.FQAllObjectsStart, heap.FQAllObjectsStop);
            AllocationContext = new MemoryRange(heap.EphemeralAllocContextPtr, heap.EphemeralAllocContextLimit);

            Segments = helpers.EnumerateSegments(this).ToImmutableArray();
        }

        public ClrHeap Heap { get; }
        IClrHeap IClrSubHeap.Heap => Heap;

        public ImmutableArray<ClrSegment> Segments { get; }

        public MemoryRange FinalizerQueueRoots { get; }
        public MemoryRange FinalizerQueueObjects { get; }
        public MemoryRange AllocationContext { get; }

        public int Index { get; }

        public bool HasPinnedObjectHeap { get; }
        public bool HasRegions { get; }
        public bool HasBackgroundGC { get; }

        public ulong Address { get; }

        public ulong Allocated { get; }
        public ulong MarkArray { get; }

        internal GCState State { get; }

        internal ulong NextSweepObject { get; }
        public ulong SavedSweepEphemeralSegment { get; }
        public ulong SavedSweepEphemeralStart { get; }
        internal ulong BackgroundSavedLowestAddress { get; }
        internal ulong BackgroundSavedHighestAddress { get; }

        public ImmutableArray<ClrGenerationData> GenerationTable { get; }
        public ulong EphemeralHeapSegment { get; }

        public ImmutableArray<ulong> FinalizationPointers { get; }

        public ulong LowestAddress { get; }
        public ulong HighestAddress { get; }
        public ulong CardTable { get; }

        ImmutableArray<IClrSegment> IClrSubHeap.Segments => Segments.CastArray<IClrSegment>();

        internal enum GCState
        {
            Marking,
            Planning,
            Free
        }
    }
}