// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    /// <summary>
    /// Implemented by an <see cref="IDataReader"/> whose memory is mapped contiguously
    /// in this process (e.g. <see cref="LockFreeMmfDataReader"/>) and that can therefore
    /// expose a zero-copy <see cref="ReadOnlySpan{T}"/> view over a target VA range.
    ///
    /// <para>
    /// Used by the GC walker hot path to avoid the per-object <c>Read</c> + memcpy
    /// in favor of walking the GCDesc directly against the mapped pointer.
    /// </para>
    /// </summary>
    internal interface ISegmentedDirectMemoryAccess
    {
        /// <summary>
        /// Attempts to obtain a zero-copy view of <paramref name="length"/> bytes starting
        /// at target virtual address <paramref name="address"/>.
        /// </summary>
        /// <param name="address">Target virtual address (start of the desired range).</param>
        /// <param name="length">Number of bytes the caller needs. <paramref name="address"/>
        /// is the range start; <c>span.Length</c> on success carries the end.</param>
        /// <param name="span">On success, a span over the mapped bytes; valid only while
        /// the reader is alive. On failure, <c>default</c>.</param>
        /// <returns>
        /// <see langword="true"/> if the entire range fits within a single mapped segment.
        /// <see langword="false"/> if the address is unmapped, the range crosses a segment
        /// boundary, or the request would otherwise fall outside the mapped view. Callers
        /// are expected to fall back to the standard <c>Read</c> path on failure.
        /// </returns>
        bool TryGetDirectSpan(ulong address, int length, out ReadOnlySpan<byte> span);
    }
}
