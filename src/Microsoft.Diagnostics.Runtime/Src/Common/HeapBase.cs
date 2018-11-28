// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal abstract class HeapBase : ClrHeap
    {
        static protected readonly ClrObject[] s_emptyObjectSet = new ClrObject[0];
        private ulong _minAddr;          // Smallest and largest segment in the GC heap.  Used to make SegmentForObject faster.  
        private ulong _maxAddr;
        private ClrSegment[] _segments;
        private ulong[] _sizeByGen = new ulong[4];
        private ulong _totalHeapSize;
        private int _lastSegmentIdx;       // The last segment we looked at.
        private bool _canWalkHeap;
        private int _pointerSize;

        public HeapBase(RuntimeBase runtime)
        {
            _canWalkHeap = runtime.CanWalkHeap;
            MemoryReader = new MemoryReader(runtime.DataReader, 0x10000);
            _pointerSize = runtime.PointerSize;
        }

        public override ulong GetMethodTable(ulong obj)
        {
            if (MemoryReader.ReadPtr(obj, out ulong mt))
                return mt;

            return 0;
        }

        public override bool ReadPointer(ulong addr, out ulong value)
        {
            if (MemoryReader.Contains(addr))
                return MemoryReader.ReadPtr(addr, out value);

            return Runtime.ReadPointer(addr, out value);
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
        public override ulong TotalHeapSize
        {
            get { return _totalHeapSize; }
        }

        public override ulong GetSizeByGen(int gen)
        {
            Debug.Assert(gen >= 0 && gen < 4);
            return _sizeByGen[gen];
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (var module in Runtime.Modules)
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
            if (runtime.GetHeaps(out SubHeap[] heaps))
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

            _minAddr = ulong.MaxValue;
            _maxAddr = ulong.MinValue;
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

        public override IEnumerable<ClrObject> EnumerateObjects()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                ClrSegment seg = _segments[i];
                
                for (ulong obj = seg.GetFirstObject(out ClrType type); obj != 0; obj = seg.NextObject(obj, out type))
                {
                    _lastSegmentIdx = i;
                    yield return ClrObject.Create(obj, type);
                }
            }
        }

        public override IEnumerable<ulong> EnumerateObjectAddresses()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                ClrSegment seg = _segments[i];
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    _lastSegmentIdx = i;
                    yield return obj;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(ulong objRef)
        {
            if (_minAddr <= objRef && objRef < _maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = _lastSegmentIdx;
                for (;;)
                {
                    ClrSegment segment = _segments[curIdx];
                    unchecked
                    {
                        long offsetInSegment = (long)(objRef - segment.Start);
                        if (0 <= offsetInSegment)
                        {
                            var intOffsetInSegment = (long)offsetInSegment;
                            if (intOffsetInSegment < (long)segment.Length)
                            {
                                _lastSegmentIdx = curIdx;
                                return segment;
                            }
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

        internal override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully)
        {
            if (type == null)
                type = GetObjectType(obj);
            else
                Debug.Assert(type == GetObjectType(obj));

            if (!type.ContainsPointers)
                return s_emptyObjectSet;

            GCDesc gcdesc = type.GCDesc;
            if (gcdesc == null)
                return s_emptyObjectSet;

            ulong size = type.GetSize(obj);
            if (carefully)
            {
                ClrSegment seg = GetSegmentByAddress(obj);
                if (seg == null || obj + size > seg.End || (!seg.IsLarge && size > 85000))
                    return s_emptyObjectSet;
            }

            List<ClrObject> result = new List<ClrObject>();
            MemoryReader reader = GetMemoryReaderForAddress(obj);
            gcdesc.WalkObject(obj, size, (ptr) => ReadPointer(reader, ptr), (reference, offset) => result.Add(new ClrObject(reference, GetObjectType(reference))));
            return result;
        }
        
        internal override void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback)
        {
            if (type == null)
                type = GetObjectType(obj);
            else
                Debug.Assert(type == GetObjectType(obj));

            if (!type.ContainsPointers)
                return;

            GCDesc gcdesc = type.GCDesc;
            if (gcdesc == null)
                return;

            ulong size = type.GetSize(obj);
            if (carefully)
            {
                ClrSegment seg = GetSegmentByAddress(obj);
                if (seg == null || obj + size > seg.End || (!seg.IsLarge && size > 85000))
                    return;
            }

            MemoryReader reader = GetMemoryReaderForAddress(obj);
            gcdesc.WalkObject(obj, size, (ptr) => ReadPointer(reader, ptr), callback);
        }

        private ulong ReadPointer(MemoryReader reader, ulong addr)
        {
            if (reader.ReadPtr(addr, out ulong result))
                return result;

            return 0;
        }

        protected abstract MemoryReader GetMemoryReaderForAddress(ulong obj);
    }

}
