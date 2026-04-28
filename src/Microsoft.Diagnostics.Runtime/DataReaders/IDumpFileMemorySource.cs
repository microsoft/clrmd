// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    /// <summary>
    /// A single contiguous (VirtualAddress, FileOffset, Size) tuple
    /// that maps a target-process VA range to a region within a dump file.
    /// </summary>
    internal readonly struct DumpMemorySegment
    {
        public DumpMemorySegment(ulong virtualAddress, ulong fileOffset, ulong size)
        {
            VirtualAddress = virtualAddress;
            FileOffset = fileOffset;
            Size = size;
        }

        public ulong VirtualAddress { get; }
        public ulong FileOffset { get; }
        public ulong Size { get; }
    }

    /// <summary>
    /// Implemented by an <see cref="IDataReader"/> that is backed by a single
    /// dump file on disk. Lets <see cref="LockFreeMmfDataReader"/> reuse the
    /// reader's already-parsed memory layout instead of re-parsing the dump.
    /// </summary>
    internal interface IDumpFileMemorySource
    {
        /// <summary>
        /// Returns the (VA, FileOffset, Size) tuples for every memory range
        /// available in the underlying dump file. The result must be either
        /// pre-sorted by VirtualAddress or safely sortable.
        /// </summary>
        IReadOnlyList<DumpMemorySegment> EnumerateMemorySegments();
    }
}
