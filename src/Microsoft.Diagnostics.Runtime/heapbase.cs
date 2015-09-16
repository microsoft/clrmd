// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{
    internal abstract class HeapBase : ClrHeap
    {
        private Address _minAddr;          // Smallest and largest segment in the GC heap.  Used to make SegmentForObject faster.  
        private Address _maxAddr;
        private ClrSegment[] _segments;
        private ulong[] _sizeByGen = new Address[4];
        private ulong _totalHeapSize;
        private int _lastSegmentIdx;       // The last segment we looked at.
        private bool _canWalkHeap;
        private int _pointerSize;

        public HeapBase(RuntimeBase runtime)
        {
            _canWalkHeap = runtime.CanWalkHeap;
            if (runtime.DataReader.CanReadAsync)
                MemoryReader = new AsyncMemoryReader(runtime.DataReader, 0x10000);
            else
                MemoryReader = new MemoryReader(runtime.DataReader, 0x10000);
            _pointerSize = runtime.PointerSize;
        }

        public override bool ReadPointer(Address addr, out Address value)
        {
            if (MemoryReader.Contains(addr))
                return MemoryReader.ReadPtr(addr, out value);

            return GetRuntime().ReadPointer(addr, out value);
        }

        internal int Revision { get; set; }

        protected abstract int GetRuntimeRevision();

        public override int PointerSize
        {
            get
            {
                return _pointerSize;
            }
        }

        public override bool CanWalkHeap
        {
            get
            {
                return _canWalkHeap;
            }
        }

        public override IList<ClrSegment> Segments
        {
            get
            {
                if (Revision != GetRuntimeRevision())
                    ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());
                return _segments;
            }
        }
        public override Address TotalHeapSize
        {
            get { return _totalHeapSize; }
        }

        public override Address GetSizeByGen(int gen)
        {
            Debug.Assert(gen >= 0 && gen < 4);
            return _sizeByGen[gen];
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (var module in GetRuntime().EnumerateModules())
            {
                var type = module.GetTypeByName(name);
                if (type != null)
                    return type;
            }

            return null;
        }

        internal MemoryReader MemoryReader { get; private set; }

        protected void UpdateSegmentData(HeapSegment segment)
        {
            _totalHeapSize += segment.Length;
            _sizeByGen[0] += segment.Gen0Length;
            _sizeByGen[1] += segment.Gen1Length;
            if (!segment.IsLarge)
                _sizeByGen[2] += segment.Gen2Length;
            else
                _sizeByGen[3] += segment.Gen2Length;
        }

        protected void InitSegments(RuntimeBase runtime)
        {
            // Populate segments
            SubHeap[] heaps;
            if (runtime.GetHeaps(out heaps))
            {
                var segments = new List<HeapSegment>();
                foreach (var heap in heaps)
                {
                    if (heap != null)
                    {
                        ISegmentData seg = runtime.GetSegmentData(heap.FirstLargeSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, true, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }

                        seg = runtime.GetSegmentData(heap.FirstSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, false, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }
                    }
                }

                UpdateSegments(segments.ToArray());
            }
            else
            {
                _segments = new ClrSegment[0];
            }
        }

        private void UpdateSegments(ClrSegment[] segments)
        {
            // sort the segments.  
            Array.Sort(segments, delegate (ClrSegment x, ClrSegment y) { return x.Start.CompareTo(y.Start); });
            _segments = segments;

            _minAddr = Address.MaxValue;
            _maxAddr = Address.MinValue;
            _totalHeapSize = 0;
            _sizeByGen = new ulong[4];
            foreach (var gcSegment in _segments)
            {
                if (gcSegment.Start < _minAddr)
                    _minAddr = gcSegment.Start;
                if (_maxAddr < gcSegment.End)
                    _maxAddr = gcSegment.End;

                _totalHeapSize += gcSegment.Length;
                if (gcSegment.IsLarge)
                    _sizeByGen[3] += gcSegment.Length;
                else
                {
                    _sizeByGen[2] += gcSegment.Gen2Length;
                    _sizeByGen[1] += gcSegment.Gen1Length;
                    _sizeByGen[0] += gcSegment.Gen0Length;
                }
            }
        }


        public override IEnumerable<Address> EnumerateObjects()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                var seg = _segments[i];
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    _lastSegmentIdx = i;
                    yield return obj;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(Address objRef)
        {
            if (_minAddr <= objRef && objRef < _maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = _lastSegmentIdx;
                for (;;)
                {
                    var segment = _segments[curIdx];
                    var offsetInSegment = (long)(objRef - segment.Start);
                    if (0 <= offsetInSegment)
                    {
                        var intOffsetInSegment = (long)offsetInSegment;
                        if (intOffsetInSegment < (long)segment.Length)
                        {
                            _lastSegmentIdx = curIdx;
                            return segment;
                        }
                    }

                    // Get the next segment loop until you come back to where you started.  
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == _lastSegmentIdx)
                        break;
                }
            }
            return null;
        }
    }


    internal class HeapSegment : ClrSegment
    {
        public override int ProcessorAffinity
        {
            get { return _subHeap.HeapNum; }
        }
        public override Address Start { get { return _segment.Start; } }
        public override Address End { get { return _subHeap.EphemeralSegment == _segment.Address ? _subHeap.EphemeralEnd : _segment.End; } }
        public override ClrHeap Heap { get { return _heap; } }

        public override bool IsLarge { get { return _large; } }

        public override Address ReservedEnd { get { return _segment.Reserved; } }
        public override Address CommittedEnd { get { return _segment.Committed; } }

        public override Address Gen0Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen0Start;
                else
                    return End;
            }
        }
        public override Address Gen0Length { get { return End - Gen0Start; } }
        public override Address Gen1Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen1Start;
                else
                    return End;
            }
        }
        public override Address Gen1Length { get { return Gen0Start - Gen1Start; } }
        public override Address Gen2Start { get { return Start; } }
        public override Address Gen2Length { get { return Gen1Start - Start; } }


        public override IEnumerable<Address> EnumerateObjects()
        {
            for (ulong obj = FirstObject; obj != 0; obj = NextObject(obj))
                yield return obj;
        }

        public override Address FirstObject
        {
            get
            {
                if (Gen2Start == End)
                    return 0;
                _heap.MemoryReader.EnsureRangeInCache(Gen2Start);
                return Gen2Start;
            }
        }

        public override Address NextObject(Address addr)
        {
            if (addr >= CommittedEnd)
                return 0;

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType type = _heap.GetObjectType(addr);
            if (type == null)
                return 0;

            ulong size = type.GetSize(addr);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            addr += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (addr >= End)
                return 0;

            // Ensure we aren't at the start of an alloc context
            ulong tmp;
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(addr, out tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (addr >= tmp)
                    return 0;

                // Otherwise:
                addr = tmp;

                if (addr >= End)
                    return 0;
            }

            return addr;
        }

        #region private
        internal static Address Align(ulong size, bool large)
        {
            Address AlignConst;
            Address AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~(AlignLargeConst);

            return (size + AlignConst) & ~(AlignConst);
        }

        public override bool IsEphemeral { get { return _segment.Address == _subHeap.EphemeralSegment; ; } }
        internal HeapSegment(RuntimeBase clr, ISegmentData segment, SubHeap subHeap, bool large, HeapBase heap)
        {
            _clr = clr;
            _large = large;
            _segment = segment;
            _heap = heap;
            _subHeap = subHeap;
        }

        private bool _large;
        private RuntimeBase _clr;
        private ISegmentData _segment;
        private SubHeap _subHeap;
        private HeapBase _heap;
        #endregion
    }
}
