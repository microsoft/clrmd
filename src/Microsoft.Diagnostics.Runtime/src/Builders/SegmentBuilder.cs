// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed class SegmentBuilder : ISegmentData
    {
        private SegmentData _segment;
        private ulong _heapAllocated;
        private ulong _ephemGen0Start;
        private ulong _ephemGen1Start;
        private ulong _ephemAddress;
        private readonly SOSDac _sos;

        public SegmentBuilder(SOSDac sos)
        {
            _sos = sos;
        }

        public bool Initialize(ulong address, in HeapDetails heap)
        {
            _heapAllocated = heap.Allocated;
            _ephemGen0Start = heap.GenerationTable[0].AllocationStart;
            _ephemGen1Start = heap.GenerationTable[1].AllocationStart;
            _ephemAddress = heap.EphemeralHeapSegment;

            return _sos.GetSegmentData(address, out _segment);
        }

        #region ISegmentData
        public int LogicalHeap { get; set; }

        public ulong BaseAddress => _segment.Address;

        public ulong Start => _segment.Start;

        public ulong End => IsEphemeralSegment ? _heapAllocated : (ulong)_segment.Allocated;

        public ulong ReservedEnd => _segment.Reserved;

        public ulong CommittedEnd => _segment.Committed;

        public ulong Gen0Start => IsEphemeralSegment ? _ephemGen0Start : End;

        public ulong Gen0Length => End - Gen0Start;

        public ulong Gen1Start => IsEphemeralSegment ? _ephemGen1Start : End;

        public ulong Gen1Length => Gen0Start - Gen1Start;

        public ulong Gen2Start => Start;

        public ulong Gen2Length => Gen1Start - Start;

        public bool IsLargeObjectSegment { get; set; }

        public bool IsEphemeralSegment => _ephemAddress == _segment.Address;
        #endregion

        public ulong Next => _segment.Next;
    }
}