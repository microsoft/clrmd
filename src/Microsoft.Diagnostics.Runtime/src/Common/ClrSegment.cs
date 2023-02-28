// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The kind of GC Segment or region.
    /// </summary>
    public enum SegmentKind
    {
        /// <summary>
        /// An Ephemeral segment is one which has Gen0, Gen1, and Gen2 sections.
        /// </summary>
        Ephemeral,

        /// <summary>
        /// This "segment" is actually a GC Gen0 region.  This is only enumerated
        /// when the GC regions feature is present in the target CLR.
        /// </summary>
        Generation0,

        /// <summary>
        /// This "segment" is actually a GC Gen1 region.  This is only enumerated
        /// when the GC regions feature is present in the target CLR.
        /// </summary>
        Generation1,

        /// <summary>
        /// This segment contains only Gen2 objects.  This may be a segment or
        /// region.
        /// </summary>
        Generation2,

        /// <summary>
        /// This segment is frozen, meaning it is both pinned and no objects will
        /// ever be collected.
        /// </summary>
        Frozen,

        /// <summary>
        /// A large object segment.  Objects here are above a certain size (usually
        /// 85,000 bytes) and all objects here are pinned.
        /// </summary>
        Large,

        /// <summary>
        /// Pinned object segment.  All objects here are pinned.
        /// </summary>
        Pinned,
    }

    /// <summary>
    /// A ClrSegment represents a contiguous region of memory that is devoted to the GC heap.
    /// Segments.  It has a start and end and knows what heap it belongs to.   Segments can
    /// optional have regions for Gen 0, 1 and 2, and Large properties.
    /// </summary>
    public sealed class ClrSegment
    {
        const int MarkerCount = 16;
        private readonly IMemoryReader _memoryReader;
        private readonly ulong[] _markers = new ulong[MarkerCount];

        public ClrSegment(IMemoryReader reader)
        {
            _memoryReader = reader;
        }

        /// <summary>
        /// The address of the CLR segment object.
        /// </summary>
        public ulong Address { get; init; }

        /// <summary>
        /// The memory range of the segment on which objects are allocated.  All objects in this segment fall within this range.
        /// </summary>
        public MemoryRange ObjectRange { get; init; }

        /// <summary>
        /// Gets the start address of the segment.  Equivalent to <see cref="ObjectRange"/>.<see cref="Start"/>.
        /// </summary>
        public ulong Start => ObjectRange.Start;

        /// <summary>
        /// Gets the end address of the segment.  Equivalent to <see cref="ObjectRange"/>.<see cref="Length"/>.
        /// </summary>
        public ulong End => ObjectRange.End;

        /// <summary>
        /// Equivalent to <see cref="ObjectRange"/>.<see cref="Length"/>.
        /// </summary>
        public ulong Length => ObjectRange.Length;

        /// <summary>
        /// Gets the processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        public ClrSubHeap SubHeap { get; init; }

        /// <summary>
        /// Gets the range of memory reserved (but not committed) for this segment.
        /// </summary>
        public MemoryRange ReservedMemory { get; init; }

        /// <summary>
        /// Gets the range of memory committed for the segment (this may be larger than MemoryRange).
        /// </summary>
        public MemoryRange CommittedMemory { get; init; }

        /// <summary>
        /// Gets the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        public ulong FirstObjectAddress => ObjectRange.Start;

        public SegmentKind Kind { get; init; }

        /// <summary>
        /// Returns true if the objects in this segment are pinned and cannot be relocated.
        /// </summary>
        public bool IsPinned => Kind == SegmentKind.Pinned || Kind == SegmentKind.Large || Kind == SegmentKind.Frozen;

        /// <summary>
        /// The memory range for Generation 0 on this segment.  This will be empty if this is not an ephemeral segment.
        /// </summary>
        public MemoryRange Generation0 { get; init; }

        /// <summary>
        /// The memory range for Generation 1 on this segment.  This will be empty if this is not an ephemeral segment.
        /// </summary>
        public MemoryRange Generation1 { get; init; }

        /// <summary>
        /// The memory range for Generation 2 on this segment.  This will be empty if this is not an ephemeral segment.
        /// </summary>
        public MemoryRange Generation2 { get; init; }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        public IEnumerable<ClrObject> EnumerateObjects()
        {

        }

        private ulong SkipAllocationContext(ulong address)
        {
        }

        internal int GetMarkerIndex(ulong obj)
        {
            if (obj <= FirstObjectAddress)
                return -1;

            if (obj >= End)
                return _markers.Length - 1;

            ulong step = Length / ((uint)_markers.Length + 1);

            if (step == 0)
                return -1;

            ulong offset = obj - FirstObjectAddress;
            int index = (int)(offset / step) - 1;

            if (index >= _markers.Length)
                index = _markers.Length - 1;

            return index;
        }

        internal void SetMarkerIndex(int marker, ulong obj)
        {
            if (0 <= marker && marker < _markers.Length && _markers[marker] == 0)
                _markers[marker] = obj;
        }

        /// <summary>
        /// Returns the object after the given object.
        /// </summary>
        /// <param name="obj">A valid object address that resides on this segment.</param>
        /// <returns>The next object on this segment, or 0 if <paramref name="obj"/> is the last object on the segment.</returns>
        public ulong GetNextObjectAddress(ulong obj)
        {
            if (obj == 0)
                return 0;

            if (!ObjectRange.Contains(obj))
                throw new InvalidOperationException($"Segment [{FirstObjectAddress:x},{CommittedMemory:x}] does not contain object {obj:x}");

            bool largeOrPinned = Kind == SegmentKind.Large || Kind == SegmentKind.Pinned;
            uint minObjSize = (uint)IntPtr.Size * 3;
            IMemoryReader memoryReader = _memoryReader;
            ulong mt = memoryReader.ReadPointer(obj);

            ClrType? type = Heap.GetOrCreateType(Heap, mt, obj);
            if (type is null)
                return 0;

            ulong size;
            if (type.ComponentSize == 0)
            {
                size = (uint)type.StaticSize;
            }
            else
            {
                uint count = memoryReader.Read<uint>(obj + (uint)IntPtr.Size);

                // Strings in v4+ contain a trailing null terminator not accounted for.
                if (Heap.StringType == type)
                    count++;

                size = count * (ulong)type.ComponentSize + (ulong)type.StaticSize;
            }

            size = ClrmdHeap.Align(size, largeOrPinned);
            if (size < minObjSize)
                size = minObjSize;

            ulong result = obj + size;

            if (!largeOrPinned)
                result = SkipAllocationContext(result); // ignore mt here because it won't be used

            if (result >= End)
                return 0;

            int marker = GetMarkerIndex(result);
            if (marker != -1 && _markers[marker] == 0)
                _markers[marker] = result;

            return result;
        }


        /// <summary>
        /// Returns the object before the given object.  Note that this function may take a while because in the worst case
        /// scenario we have to linearly walk all the way from the beginning of the segment to the object.
        /// </summary>
        /// <param name="obj">An address that resides on this segment.  This does not need to point directly to a good object.</param>
        /// <returns>The previous object on this segment, or 0 if <paramref name="obj"/> is the first object on the segment.</returns>
        public ulong GetPreviousObjectAddress(ulong obj)
        {
            if (!ObjectRange.Contains(obj))
                throw new InvalidOperationException($"Segment [{FirstObjectAddress:x},{CommittedMemory:x}] does not contain address {obj:x}");

            if (obj == FirstObjectAddress)
                return 0;

            // Default to the start of the segment
            ulong prevAddr = FirstObjectAddress;

            // Look for markers that are closer to the address.  We keep the size of _markers small so a linear walk
            // should be roughly as fast as a binary search.
            foreach (ulong marker in _markers)
            {
                // Markers can be 0 even when _markers was fully initialized by a full heap walk.  This is because parts of
                // the ephemeral GC heap may be not in use (allocation contexts) or when objects on the large object heap
                // are so big that there's simply not a valid object starting point in that range.
                if (marker != 0)
                {
                    if (marker >= obj)
                        break;

                    prevAddr = marker;
                }
            }

            // Linear walk from the last known good previous address to the one we are looking for.
            // This could take a while if we don't know a close enough address.
            ulong curr = prevAddr;
            while (curr != 0 && curr <= obj)
            {
                ulong next = GetNextObjectAddress(curr);

                if (next >= obj)
                    return curr;

                curr = next;
            }

            return 0;
        }

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>
        /// The generation of the given object if that object lies in this segment.  The return
        /// value is undefined if the object does not lie in this segment.
        /// </returns>
        public int GetGeneration(ulong obj)
        {
            if (Generation2.Contains(obj))
                return 2;

            if (Generation1.Contains(obj))
                return 1;

            if (Generation0.Contains(obj))
                return 0;

            return -1;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return $"[{Start:x12}, {End:x12}]";
        }

        /// <summary>
        /// The next segment in the heap.
        /// </summary>
        internal ulong Next { get; init; }
    }
}
