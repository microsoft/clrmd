// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        abstract public ulong Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public ulong End { get; }

        /// <summary>
        /// The number of bytes in the segment.
        /// </summary>
        public ulong Length { get { return (End - Start); } }

        /// <summary>
        /// The GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// The processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        abstract public int ProcessorAffinity { get; }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        virtual public ulong ReservedEnd { get { return 0; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        virtual public ulong CommittedEnd { get { return 0; } }

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        abstract public ulong FirstObject { get; }

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        /// <param name="type">The type of the first object.</param>
        /// <returns>The first object on this segment or 0 if this segment contains no objects.</returns>
        abstract public ulong GetFirstObject(out ClrType type);


        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        abstract public ulong NextObject(ulong objRef);

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        abstract public ulong NextObject(ulong objRef, out ClrType type);


        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections. 
        /// </summary>
        virtual public bool IsLarge { get { return false; } }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        virtual public bool IsEphemeral { get { return false; } }

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        virtual public ulong Gen0Start { get { return Start; } }

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        virtual public ulong Gen0Length { get { return Length; } }

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Start { get { return End; } }

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Length { get { return 0; } }

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Start { get { return End; } }

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Length { get { return 0; } }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        abstract public IEnumerable<ulong> EnumerateObjectAddresses();

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>The generation of the given object if that object lies in this segment.  The return
        ///          value is undefined if the object does not lie in this segment.
        /// </returns>
        virtual public int GetGeneration(ulong obj)
        {
            if (Gen0Start <= obj && obj < (Gen0Start + Gen0Length))
            {
                return 0;
            }

            if (Gen1Start <= obj && obj < (Gen1Start + Gen1Length))
            {
                return 1;
            }

            if (Gen2Start <= obj && obj < (Gen2Start + Gen2Length))
            {
                return 2;
            }

            return -1;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("HeapSegment {0:n2}mb [{1:X8}, {2:X8}]", Length / 1000000.0, Start, End);
        }
    }

}
