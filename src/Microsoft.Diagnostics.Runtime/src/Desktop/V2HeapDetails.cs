// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2HeapDetails : IHeapDetails
    {
        public readonly ulong HeapAddr;
        public readonly ulong AllocAllocated;

        public readonly GenerationData GenerationTable0;
        public readonly GenerationData GenerationTable1;
        public readonly GenerationData GenerationTable2;
        public readonly GenerationData GenerationTable3;
        public readonly ulong EphemeralHeapSegment;
        public readonly ulong FinalizationFillPointers0;
        public readonly ulong FinalizationFillPointers1;
        public readonly ulong FinalizationFillPointers2;
        public readonly ulong FinalizationFillPointers3;
        public readonly ulong FinalizationFillPointers4;
        public readonly ulong FinalizationFillPointers5;
        public readonly ulong LowestAddress;
        public readonly ulong HighestAddress;
        public readonly ulong CardTable;

        ulong IHeapDetails.FirstHeapSegment => GenerationTable2.StartSegment;
        ulong IHeapDetails.FirstLargeHeapSegment => GenerationTable3.StartSegment;
        ulong IHeapDetails.EphemeralSegment => EphemeralHeapSegment;
        ulong IHeapDetails.EphemeralEnd => AllocAllocated;
        ulong IHeapDetails.EphemeralAllocContextPtr => GenerationTable0.AllocationContextPointer;
        ulong IHeapDetails.EphemeralAllocContextLimit => GenerationTable0.AllocationContextLimit;
        ulong IHeapDetails.FQAllObjectsStart => FinalizationFillPointers0;
        ulong IHeapDetails.FQAllObjectsStop => FinalizationFillPointers3;
        ulong IHeapDetails.FQRootsStart => FinalizationFillPointers3;
        ulong IHeapDetails.FQRootsStop => FinalizationFillPointers5;
        ulong IHeapDetails.Gen0Start => GenerationTable0.AllocationStart;
        ulong IHeapDetails.Gen0Stop => AllocAllocated;
        ulong IHeapDetails.Gen1Start => GenerationTable1.AllocationStart;
        ulong IHeapDetails.Gen1Stop => GenerationTable0.AllocationStart;
        ulong IHeapDetails.Gen2Start => GenerationTable2.AllocationStart;
        ulong IHeapDetails.Gen2Stop => GenerationTable1.AllocationStart;
    }
}