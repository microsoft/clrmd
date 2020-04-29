// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrSegment represents a contiguous region of memory that is devoted to the GC heap.
    /// Segments.  It has a start and end and knows what heap it belongs to.   Segments can
    /// optional have regions for Gen 0, 1 and 2, and Large properties.
    /// </summary>
    public abstract class ClrSegment
    {
        /// <summary>
        /// Gets the GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        public abstract ClrHeap Heap { get; }

        /// <summary>
        /// The memory range of the segment on which objects are allocated.  All objects in this segment fall within this range.
        /// </summary>
        public abstract MemoryRange ObjectRange { get; }

        /// <summary>
        /// Gets the start address of the segment.  Equivalent to <see cref="ObjectRange"/>.<see cref="Start"/>.
        /// </summary>
        public virtual ulong Start => ObjectRange.Start;

        /// <summary>
        /// Gets the end address of the segment.  Equivalent to <see cref="ObjectRange"/>.<see cref="Length"/>.
        /// </summary>
        public virtual ulong End => ObjectRange.End;

        /// <summary>
        /// Equivalent to <see cref="ObjectRange"/>.<see cref="Length"/>.
        /// </summary>
        public virtual ulong Length => ObjectRange.Length;

        /// <summary>
        /// Gets the processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        public abstract int LogicalHeap { get; }

        /// <summary>
        /// Gets the range of memory reserved (but not commited) for this segment.
        /// </summary>
        public abstract MemoryRange ReservedMemory { get; }

        /// <summary>
        /// Gets the range of memory committed for the segment (this may be larger than MemoryRange).
        /// </summary>
        public abstract MemoryRange CommittedMemory { get; }

        /// <summary>
        /// Gets the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        public abstract ulong FirstObjectAddress { get; }

        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections.
        /// </summary>
        public abstract bool IsLargeObjectSegment { get; }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        public abstract bool IsEphemeralSegment { get; }

        /// <summary>
        /// The memory range for Generation 0 on this segment.  This will be empty if <see cref="IsEphemeralSegment"/> is false.
        /// </summary>
        public abstract MemoryRange Generation0 { get; }

        /// <summary>
        /// The memory range for Generation 1 on this segment.  This will be empty if <see cref="IsEphemeralSegment"/> is false.
        /// </summary>
        public abstract MemoryRange Generation1 { get; }

        /// <summary>
        /// The memory range for Generation e on this segment.  This will be equivalent to ObjectRange if <see cref="IsEphemeralSegment"/> is false.
        /// </summary>
        public abstract MemoryRange Generation2 { get; }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        public abstract IEnumerable<ClrObject> EnumerateObjects();

        /// <summary>
        /// Returns the object after the given object.
        /// </summary>
        /// <param name="obj">A valid object address that resides on this segment.</param>
        /// <returns>The next object on this segment, or 0 if <paramref name="obj"/> is the last object on the segment.</returns>
        public abstract ulong GetNextObjectAddress(ulong obj);

        /// <summary>
        /// Returns the object before the given object.  Note that this function may take a while because in the worst case
        /// scenario we have to linearly walk all the way from the beginning of the segment to the object.
        /// </summary>
        /// <param name="obj">An address that resides on this segment.  This does not need to point directly to a good object.</param>
        /// <returns>The previous object on this segment, or 0 if <paramref name="obj"/> is the first object on the segment.</returns>
        public abstract ulong GetPreviousObjectAddress(ulong obj);

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>
        /// The generation of the given object if that object lies in this segment.  The return
        /// value is undefined if the object does not lie in this segment.
        /// </returns>
        public virtual int GetGeneration(ulong obj)
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
            return $"HeapSegment {Length / 1000000.0:n2}mb [{Start:X8}, {End:X8}]";
        }
    }
}
