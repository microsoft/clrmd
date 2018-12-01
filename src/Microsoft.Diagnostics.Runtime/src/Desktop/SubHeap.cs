// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class SubHeap
    {
        internal int HeapNum { get; }
        private IHeapDetails ActualHeap { get; }

        /// <summary>
        /// The allocation context pointers/limits for this heap.  The keys of this
        /// dictionary are the allocation pointers, the values of this dictionary are
        /// the limits.  If an allocation pointer is ever reached while walking a
        /// segment, you must "skip" past the allocation limit.  That is:
        /// if (curr_obj is in AllocPointers)
        /// curr_obj = AllocPointers[curr_obj] + min_object_size;
        /// </summary>
        internal Dictionary<ulong, ulong> AllocPointers { get; set; }

        /// <summary>
        /// Returns the address of the ephemeral segment.  Users of this API should use
        /// HeapSegment.Ephemeral instead of this property.
        /// </summary>
        internal ulong EphemeralSegment => ActualHeap.EphemeralSegment;

        /// <summary>
        /// Returns the actual end of the ephemeral segment.
        /// </summary>
        internal ulong EphemeralEnd => ActualHeap.EphemeralEnd;

        internal ulong Gen0Start => ActualHeap.Gen0Start;
        internal ulong Gen1Start => ActualHeap.Gen1Start;
        internal ulong Gen2Start => ActualHeap.Gen2Start;
        internal ulong FirstLargeSegment => ActualHeap.FirstLargeHeapSegment;
        internal ulong FirstSegment => ActualHeap.FirstHeapSegment;
        internal ulong FQAllObjectsStart => ActualHeap.FQAllObjectsStart;
        internal ulong FQAllObjectsStop => ActualHeap.FQAllObjectsStop;
        internal ulong FQRootsStart => ActualHeap.FQRootsStart;
        internal ulong FQRootsStop => ActualHeap.FQRootsStop;

        internal SubHeap(IHeapDetails heap, int heapNum, Dictionary<ulong, ulong> allocPointers)
        {
            ActualHeap = heap;
            HeapNum = heapNum;
            AllocPointers = allocPointers;
        }
    }
}