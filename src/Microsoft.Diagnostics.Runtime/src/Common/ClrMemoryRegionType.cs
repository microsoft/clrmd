// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Types of memory regions in a Clr process.
    /// </summary>
    public enum ClrMemoryRegionType
    {
        // Loader heaps
        /// <summary>
        /// Data on the loader heap.
        /// </summary>
        LowFrequencyLoaderHeap,

        /// <summary>
        /// Data on the loader heap.
        /// </summary>
        HighFrequencyLoaderHeap,

        /// <summary>
        /// Data on the stub heap.
        /// </summary>
        StubHeap,

        // Virtual Call Stub heaps
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        IndcellHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        LookupHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        ResolveHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        DispatchHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        CacheEntryHeap,

        // Other regions
        /// <summary>
        /// Heap for JIT code data.
        /// </summary>
        JitHostCodeHeap,
        /// <summary>
        /// Heap for JIT loader data.
        /// </summary>
        JitLoaderCodeHeap,
        /// <summary>
        /// Heap for module jump thunks.
        /// </summary>
        ModuleThunkHeap,
        /// <summary>
        /// Heap for module lookup tables.
        /// </summary>
        ModuleLookupTableHeap,

        /// <summary>
        /// A segment on the GC heap (committed memory).
        /// </summary>
        GCSegment,

        /// <summary>
        /// A segment on the GC heap (reserved, but not committed, memory).
        /// </summary>
        ReservedGCSegment,

        /// <summary>
        /// A portion of Clr's handle table.
        /// </summary>
        HandleTableChunk
    }
}