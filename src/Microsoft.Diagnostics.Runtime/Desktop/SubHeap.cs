using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class SubHeap
    {
        internal int HeapNum { get; private set; }
        private IHeapDetails ActualHeap { get; set; }

        /// <summary>
        /// The allocation context pointers/limits for this heap.  The keys of this
        /// dictionary are the allocation pointers, the values of this dictionary are
        /// the limits.  If an allocation pointer is ever reached while walking a
        /// segment, you must "skip" past the allocation limit.  That is:
        ///     if (curr_obj is in AllocPointers)
        ///         curr_obj = AllocPointers[curr_obj] + min_object_size;
        /// </summary>
        internal Dictionary<ulong, ulong> AllocPointers { get; set; }

        /// <summary>
        /// Returns the address of the ephemeral segment.  Users of this API should use
        /// HeapSegment.Ephemeral instead of this property.
        /// </summary>
        internal ulong EphemeralSegment { get { return ActualHeap.EphemeralSegment; } }

        /// <summary>
        /// Returns the actual end of the ephemeral segment.
        /// </summary>
        internal ulong EphemeralEnd { get { return ActualHeap.EphemeralEnd; } }

        internal ulong Gen0Start { get { return ActualHeap.Gen0Start; } }
        internal ulong Gen1Start { get { return ActualHeap.Gen1Start; } }
        internal ulong Gen2Start { get { return ActualHeap.Gen2Start; } }
        internal ulong FirstLargeSegment { get { return ActualHeap.FirstLargeHeapSegment; } }
        internal ulong FirstSegment { get { return ActualHeap.FirstHeapSegment; } }
        internal ulong FQAllObjectsStart { get { return ActualHeap.FQAllObjectsStart; } }
        internal ulong FQAllObjectsStop { get { return ActualHeap.FQAllObjectsStop; } }
        internal ulong FQRootsStart { get { return ActualHeap.FQRootsStart; } }
        internal ulong FQRootsStop { get { return ActualHeap.FQRootsStop; } }

        internal SubHeap(IHeapDetails heap, int heapNum, Dictionary<ulong, ulong> allocPointers)
        {
            ActualHeap = heap;
            HeapNum = heapNum;
            AllocPointers = allocPointers;
        }
    }
}
