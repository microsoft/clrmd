﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// This class is a HashSet of ulong for object addresses.
    /// </summary>
    public class ObjectSet
    {
        private static int MinObjSize => IntPtr.Size * 3;
        private readonly HeapHashSegment[] _segments;

        /// <summary>
        /// The ClrHeap this is an object set over.
        /// </summary>
        public ClrHeap Heap { get; }

        /// <summary>
        /// The collection of segments and associated objects.
        /// </summary>
        protected ImmutableArray<HeapHashSegment> Segments => _segments.AsImmutableArray();

        /// <summary>
        /// Gets or sets the count of objects in this set.
        /// </summary>
        public int Count { get; protected set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="heap">A ClrHeap to add objects from.</param>
        public ObjectSet(ClrHeap heap)
        {
            Heap = heap ?? throw new ArgumentNullException(nameof(heap));

            List<HeapHashSegment> segments = new(heap.Segments.Length);
            foreach (ClrSegment seg in heap.Segments)
            {
                ulong start = seg.Start;
                ulong end = seg.End;

                if (start < end)
                {
                    segments.Add(new HeapHashSegment
                    {
                        StartAddress = start,
                        EndAddress = end,
                        Objects = new BitArray((int)((uint)(end - start) / MinObjSize), false)
                    });
                }
            }

            _segments = segments.ToArray();
        }

        /// <summary>
        /// Returns true if this set contains the given object, false otherwise.  The behavior of this function is undefined if
        /// obj lies outside the GC heap.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if this set contains the given object, false otherwise.</returns>
        public virtual bool Contains(ulong obj)
        {
            if (GetSegment(obj, out HeapHashSegment seg))
            {
                int offset = GetOffset(obj, seg);
                return seg.Objects[offset];
            }

            return false;
        }

        /// <summary>
        /// Adds the given object to the set.  Returns true if the object was added to the set, returns false if the object was already in the set.
        /// </summary>
        /// <param name="obj">The object to add to the set.</param>
        /// <returns>True if the object was added to the set, returns false if the object was already in the set.</returns>
        public virtual bool Add(ulong obj)
        {
            if (GetSegment(obj, out HeapHashSegment seg))
            {
                int offset = GetOffset(obj, seg);
                if (seg.Objects[offset])
                {
                    return false;
                }

                seg.Objects.Set(offset, true);
                Count++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the given object from the set.  Returns true if the object was removed, returns false if the object was not in the set.
        /// </summary>
        /// <param name="obj">The object to remove from the set.</param>
        /// <returns>True if the object was removed, returns false if the object was not in the set.</returns>
        public virtual bool Remove(ulong obj)
        {
            if (GetSegment(obj, out HeapHashSegment seg))
            {
                int offset = GetOffset(obj, seg);
                if (seg.Objects[offset])
                {
                    seg.Objects.Set(offset, false);
                    Count--;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Empties the set.
        /// </summary>
        public virtual void Clear()
        {
            for (int i = 0; i < _segments.Length; i++)
                _segments[i].Objects.SetAll(false);

            Count = 0;
        }

        /// <summary>
        /// Calculates the offset of an object within a segment.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="seg">The segment.</param>
        /// <returns>The index into seg.Objects.</returns>
        protected static int GetOffset(ulong obj, HeapHashSegment seg)
        {
            return checked((int)((uint)(obj - seg.StartAddress) / MinObjSize));
        }

        /// <summary>
        /// Gets the segment for the given object.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="seg">The resulting segment.</param>
        /// <returns>True if obj lies within a gc segment, false otherwise.</returns>
        protected bool GetSegment(ulong obj, out HeapHashSegment seg)
        {
            if (obj != 0)
            {
                int lower = 0;
                int upper = _segments.Length - 1;

                while (lower <= upper)
                {
                    int mid = (lower + upper) >> 1;

                    if (obj < _segments[mid].StartAddress)
                    {
                        upper = mid - 1;
                    }
                    else if (obj >= _segments[mid].EndAddress)
                    {
                        lower = mid + 1;
                    }
                    else
                    {
                        seg = _segments[mid];
                        return true;
                    }
                }
            }

            seg = default;
            return false;
        }

        /// <summary>
        /// A segment of memory in the heap.
        /// </summary>
        protected struct HeapHashSegment
        {
            /// <summary>
            /// The objects in the memory range.
            /// </summary>
            public BitArray Objects;

            /// <summary>
            /// The start address of the segment.
            /// </summary>
            public ulong StartAddress;

            /// <summary>
            /// The end address of the segment.
            /// </summary>
            public ulong EndAddress;
        }
    }
}