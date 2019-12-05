// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CA1721 // Property names should not match get methods

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdHeap : ClrHeap
    {
        private const int MaxGen2ObjectSize = 85000;
        private readonly IHeapHelpers _helpers;
        private readonly IReadOnlyList<FinalizerQueueSegment> _fqRoots;
        private readonly IReadOnlyList<FinalizerQueueSegment> _fqObjects;
        private readonly Dictionary<ulong, ulong> _allocationContext;
        private int _lastSegmentIndex;
        private (ulong, ulong)[] _dependants;

        [ThreadStatic]
        private static MemoryReader _memoryReader;

        public override ClrRuntime Runtime { get; }

        public override bool CanWalkHeap { get; }

        public override IReadOnlyList<ClrSegment> Segments { get; }

        public override ClrType FreeType { get; }

        public override ClrType StringType { get; }

        public override ClrType ObjectType { get; }

        public override ClrType ExceptionType { get; }

        public override bool IsServer { get; }

        public ClrmdHeap(ClrRuntime runtime, IHeapBuilder heapBuilder)
        {
            if (heapBuilder is null)
                throw new NullReferenceException(nameof(heapBuilder));

            _helpers = heapBuilder.HeapHelpers;

            Runtime = runtime;
            CanWalkHeap = heapBuilder.CanWalkHeap;
            IsServer = heapBuilder.IsServer;

            // Prepopulate a few important method tables.

            StringType = _helpers.Factory.GetOrCreateType(this, heapBuilder.StringMethodTable, 0);
            ObjectType = _helpers.Factory.GetOrCreateType(this, heapBuilder.ObjectMethodTable, 0);
            FreeType = _helpers.Factory.GetOrCreateType(this, heapBuilder.FreeMethodTable, 0);
            ExceptionType = _helpers.Factory.GetOrCreateType(this, heapBuilder.ExceptionMethodTable, 0);

            // Segments must be in sorted order.  We won't check all of them but we will at least check the beginning and end
            Segments = heapBuilder.CreateSegments(this, out IReadOnlyList<AllocationContext> allocContext, out _fqRoots, out _fqObjects);
            if (Segments.Count > 0 && Segments[0].Start > Segments[Segments.Count - 1].Start)
                throw new InvalidOperationException("IHeapBuilder returned segments out of order.");

            _allocationContext = allocContext.ToDictionary(k => k.Pointer, v => v.Limit);
        }

        internal IEnumerable<ClrObject> EnumerateObjects(ClrSegment seg)
        {
            bool large = seg.IsLargeObjectSegment;
            uint minObjSize = (uint)IntPtr.Size * 3;

            ulong obj = seg.FirstObject;

            if (_memoryReader == null)
                _memoryReader = new MemoryReader(_helpers.DataReader, 0x10000);

            _memoryReader.EnsureRangeInCache(obj);
            while (obj < seg.CommittedEnd)
            {
                if (!_memoryReader.ReadPtr(obj, out ulong mt))
                    break;

                ClrType type = _helpers.Factory.GetOrCreateType(mt, obj);
                if (type == null)
                    break;

                ClrObject result = new ClrObject(obj, type);
                yield return result;

                ulong size = result.Size;
                size = Align(size, large);
                if (size < minObjSize)
                    size = minObjSize;

                obj += size;
                while (!large && _allocationContext.TryGetValue(obj, out ulong nextObj))
                {
                    nextObj += Align(minObjSize, large);

                    // Only if there's data corruption:
                    if (obj >= nextObj)
                        yield break;

                    if (obj >= seg.End)
                        yield break;

                    obj = nextObj;
                }
            }

            _memoryReader = null;
        }

        public override IEnumerable<ClrObject> EnumerateObjects() => Segments.SelectMany(s => EnumerateObjects(s));

        internal static ulong Align(ulong size, bool large)
        {
            ulong AlignConst;
            ulong AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~AlignLargeConst;

            return (size + AlignConst) & ~AlignConst;
        }

        public override ClrType GetObjectType(ulong objRef)
        {
            if (_memoryReader != null && _memoryReader.Contains(objRef) && _memoryReader.TryReadPtr(objRef, out ulong mt))
            {
            }
            else
            {
                mt = _helpers.DataReader.ReadPointerUnsafe(objRef);
            }

            if (mt == 0)
                return null;

            return _helpers.Factory.GetOrCreateType(mt, objRef);
        }

        public override ClrSegment GetSegmentByAddress(ulong objRef)
        {
            if (Segments == null || Segments.Count == 0)
                return null;

            if (Segments[0].FirstObject <= objRef && objRef < Segments[Segments.Count - 1].End)
            {
                // Start the segment search where you where last
                int curIdx = _lastSegmentIndex;
                for (; ; )
                {
                    ClrSegment segment = Segments[curIdx];
                    unchecked
                    {
                        long offsetInSegment = (long)(objRef - segment.Start);
                        if (offsetInSegment >= 0)
                        {
                            long intOffsetInSegment = offsetInSegment;
                            if (intOffsetInSegment < (long)segment.Length)
                            {
                                _lastSegmentIndex = curIdx;
                                return segment;
                            }
                        }
                    }

                    // Get the next segment loop until you come back to where you started.
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == _lastSegmentIndex)
                        break;
                }
            }

            return null;
        }

        public override ulong GetObjectSize(ulong objRef, ClrType type)
        {
            ulong size;
            if (type.ComponentSize == 0)
            {
                size = (uint)type.BaseSize;
            }
            else
            {
                uint countOffset = (uint)IntPtr.Size;
                ulong loc = objRef + countOffset;

                uint count;


                if (_memoryReader != null)
                    _memoryReader.ReadDword(loc, out count);
                else
                    count = _helpers.DataReader.ReadUnsafe<uint>(loc);

                // Strings in v4+ contain a trailing null terminator not accounted for.
                if (StringType == type)
                    count++;

                size = count * (ulong)type.ComponentSize + (ulong)(type.BaseSize);
            }

            uint minSize = (uint)IntPtr.Size * 3;
            if (size < minSize)
                size = minSize;
            return size;
        }

        public override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                if (_dependants == null)
                {
                    _dependants = _helpers.EnumerateDependentHandleLinks().ToArray();
                    Array.Sort(_dependants, (x, y) => x.Item1.CompareTo(y.Item1));
                }

                if (_dependants.Length > 0)
                {
                    int index = _dependants.Search(obj, (x, y) => x.Item1.CompareTo(y));
                    if (index != -1)
                    {
                        while (index >= 1 && _dependants[index - 1].Item1 == obj)
                            index--;

                        while (index < _dependants.Length && _dependants[index].Item1 == obj)
                        {
                            ulong dependantObj = _dependants[index++].Item2;
                            yield return new ClrObject(dependantObj, GetObjectType(dependantObj));
                        }
                    }
                }
            }

            if (type.IsCollectible)
            {
                ulong la = _helpers.DataReader.ReadPointerUnsafe(type.LoaderAllocatorHandle);
                if (la != 0)
                    yield return new ClrObject(la, GetObjectType(la));
            }

            if (type.ContainsPointers)
            {
                GCDesc gcdesc = type.GCDesc;
                if (!gcdesc.IsEmpty)
                {
                    ulong size = GetObjectSize(obj, type);
                    if (carefully)
                    {
                        ClrSegment seg = GetSegmentByAddress(obj);
                        if (seg == null || obj + size > seg.End || (!seg.IsLargeObjectSegment && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    foreach ((ulong reference, int offset) in gcdesc.WalkObject(obj, size, ReadPointerForGCDesc))
                        yield return new ClrObject(reference, GetObjectType(reference));
                }
            }
        }

        private ulong ReadPointerForGCDesc(ulong ptr)
        {
            if (_memoryReader != null && _memoryReader.Contains(ptr) && _memoryReader.ReadPtr(ptr, out ulong value))
                return value;

            return _helpers.DataReader.ReadPointerUnsafe(ptr);
        }

        public override IEnumerable<IClrRoot> EnumerateRoots()
        {
            // Handle table
            foreach (ClrHandle handle in Runtime.EnumerateHandles())
                if (handle.IsStrong)
                    yield return handle;

            // Finalization Queue
            foreach (ClrFinalizerRoot root in EnumerateFinalizerRoots())
                yield return root;

            // Threads
            foreach (IClrRoot root in EnumerateStackRoots())
                yield return root;
        }

        private IEnumerable<IClrRoot> EnumerateStackRoots()
        {
            foreach (ClrThread thread in Runtime.Threads)
            {
                if (thread.IsAlive)
                {
                    foreach (IClrRoot root in thread.EnumerateStackRoots())
                        yield return root;
                }
            }
        }

        public override IEnumerable<ClrObject> EnumerateFinalizableObjects() => EnumerateFQ(_fqObjects).Select(root => root.Object);

        public override IEnumerable<ClrFinalizerRoot> EnumerateFinalizerRoots() => EnumerateFQ(_fqRoots);

        private IEnumerable<ClrFinalizerRoot> EnumerateFQ(IEnumerable<FinalizerQueueSegment> fqList)
        {
            if (fqList == null)
                yield break;

            foreach (FinalizerQueueSegment seg in fqList)
            {
                for (ulong ptr = seg.Start; ptr < seg.End; ptr += (uint)IntPtr.Size)
                {
                    ulong obj = _helpers.DataReader.ReadPointerUnsafe(ptr);
                    if (obj == 0)
                        continue;

                    ulong mt = _helpers.DataReader.ReadPointerUnsafe(obj);
                    ClrType type = _helpers.Factory.GetOrCreateType(mt, obj);
                    if (type != null)
                        yield return new ClrFinalizerRoot(ptr, new ClrObject(obj, type));
                }
            }
        }
    }
}