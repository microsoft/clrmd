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
        private bool _regions;
        private ulong _heapAllocated;
        private ulong _ephemAddress;
        private readonly SOSDac _sos;

        // Segments only
        private ulong _ephemGen0Start;
        private ulong _ephemGen1Start;

        // Regions only
        private int _generation;

        public SegmentBuilder(SOSDac sos)
        {
            _sos = sos;
        }

        public bool Initialize(ulong address, int generation, in HeapDetails heap)
        {
            _regions = heap.SavedSweepEphemeralSeg.Value == -1;

            _heapAllocated = heap.Allocated;
            if (_regions)
            {
                _generation = generation;
            }
            else
            {
                _ephemGen0Start = heap.GenerationTable[0].AllocationStart;
                _ephemGen1Start = heap.GenerationTable[1].AllocationStart;
            }
            _ephemAddress = heap.EphemeralHeapSegment;

            return _sos.GetSegmentData(address, out _segment);
        }

        #region ISegmentData
        public int LogicalHeap { get; set; }
        
        // 0x20 is sizeof(aligned_plug_and_gap)
        public ulong BaseAddress => _regions ? (_segment.Start - 0x20) : _segment.Address;

        public ulong Start => _segment.Start;

        public ulong End => IsEphemeralSegment ? _heapAllocated : (ulong)_segment.Allocated;

        public ulong ReservedEnd => _segment.Reserved;

        public ulong CommittedEnd => _segment.Committed;

        public ulong Gen0Start
        {
            get
            {
                if (_regions)
                {
                    return (_generation == 0) ? Start : End;
                }
                else
                {
                    return IsEphemeralSegment ? _ephemGen0Start : End;
                }
            }
        }

        public ulong Gen0Length => End - Gen0Start;

        public ulong Gen1Start
        {
            get
            {
                if (_regions)
                {
                    return (_generation == 1) ? Start : End;
                }
                else
                {
                    return IsEphemeralSegment ? _ephemGen1Start : End;
                }
            }
        }

        public ulong Gen1Length
        {
            get
            {
                if (_regions)
                {
                    return End - Gen1Start;
                }
                else
                {
                    return Gen0Start - Gen1Start;
                }
            }
        }

        public ulong Gen2Start
        {
            get
            {
                if (_regions)
                {
                    return (_generation >= 2) ? Start : End;
                }
                else
                {
                    return Start;
                }
            }
        }

        public ulong Gen2Length
        {
            get
            {
                if (_regions)
                {
                    return End - Gen2Start;
                }
                else
                {
                    return Gen1Start - Start;
                }
            }
        }

        public bool IsLargeObjectSegment { get; set; }

        public bool IsPinnedObjectSegment { get; set; }

        public bool IsEphemeralSegment => _ephemAddress == _segment.Address;
        #endregion

        public ulong Next => _segment.Next;
    }
}