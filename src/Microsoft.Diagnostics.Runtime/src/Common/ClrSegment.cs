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
        /// The start address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        public abstract ulong Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        public abstract ulong End { get; }

        /// <summary>
        /// The number of bytes in the segment.
        /// </summary>
        public ulong Length => End - Start;

        /// <summary>
        /// The GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        public abstract ClrHeap Heap { get; }

        /// <summary>
        /// The processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        public abstract int ProcessorAffinity { get; }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        public virtual ulong ReservedEnd => 0;

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        public virtual ulong CommittedEnd => 0;

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        public abstract ulong FirstObject { get; }

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        /// <param name="type">The type of the first object.</param>
        /// <returns>The first object on this segment or 0 if this segment contains no objects.</returns>
        public abstract ulong GetFirstObject(out ClrType type);

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)
        /// </summary>
        public abstract ulong NextObject(ulong objRef);

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)
        /// </summary>
        public abstract ulong NextObject(ulong objRef, out ClrType type);

        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections.
        /// </summary>
        public virtual bool IsLarge => false;

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        public virtual bool IsEphemeral => false;

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        public virtual ulong Gen0Start => Start;

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        public virtual ulong Gen0Length => Length;

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        public virtual ulong Gen1Start => End;

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        public virtual ulong Gen1Length => 0;

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        public virtual ulong Gen2Start => End;

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        public virtual ulong Gen2Length => 0;

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        public abstract IEnumerable<ulong> EnumerateObjectAddresses();

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
            if (Gen0Start <= obj && obj < Gen0Start + Gen0Length)
                return 0;

            if (Gen1Start <= obj && obj < Gen1Start + Gen1Length)
                return 1;

            if (Gen2Start <= obj && obj < Gen2Start + Gen2Length)
                return 2;

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