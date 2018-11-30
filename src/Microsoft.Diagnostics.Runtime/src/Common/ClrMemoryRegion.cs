// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a region of memory in the process which Clr allocated and controls.
    /// </summary>
    public abstract class ClrMemoryRegion
    {
        /// <summary>
        /// The start address of the memory region.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The size of the memory region in bytes.
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// The type of heap/memory that the region contains.
        /// </summary>
        public ClrMemoryRegionType Type { get; set; }

        /// <summary>
        /// The AppDomain pointer that corresponds to this heap.  You can obtain the
        /// name of the AppDomain index or name by calling the appropriate function
        /// on RuntimeBase.
        /// Note:  HasAppDomainData must be true before getting this property.
        /// </summary>
        public abstract ClrAppDomain AppDomain { get; }

        /// <summary>
        /// The Module pointer that corresponds to this heap.  You can obtain the
        /// filename of the module with this property.
        /// Note:  HasModuleData must be true or this property will be null.
        /// </summary>
        public abstract string Module { get; }

        /// <summary>
        /// Returns the heap number associated with this data.  Returns -1 if no
        /// GC heap is associated with this memory region.
        /// </summary>
        public abstract int HeapNumber { get; set; }

        /// <summary>
        /// Returns the gc segment type associated with this data.  Only callable if
        /// HasGCHeapData is true.
        /// </summary>
        public abstract GCSegmentType GCSegmentType { get; set; }

        /// <summary>
        /// Returns a string describing the region of memory (for example "JIT Code Heap"
        /// or "GC Segment").
        /// </summary>
        /// <param name="detailed">
        /// Whether or not to include additional data such as the module,
        /// AppDomain, or GC Heap associaed with it.
        /// </param>
        public abstract string ToString(bool detailed);

        /// <summary>
        /// Equivalent to GetDisplayString(false).
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }
    }
}