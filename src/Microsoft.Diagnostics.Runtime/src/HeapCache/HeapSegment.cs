// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DumpAnalyzer.Definitions.Interfaces.Native;
using System.Diagnostics;

namespace DumpAnalyzer.Library.Native
{
    [DebuggerDisplay("{Start} - {End}")]
    internal class HeapSegment : INativeHeapSegment
    {
        // Used for creating a MemorySegmentData object for searching for a segment for a heap memory read request
        internal HeapSegment(ulong startAddress, ulong size) : this(startAddress, size, segmentRVA: 0)
        { }

        internal HeapSegment(ulong startAddress, ulong size, ulong segmentRVA)
        {
            Start = startAddress;
            Size = size;
            SegmentRVA = segmentRVA;
        }

        internal ulong Size { get; }
        internal ulong SegmentRVA { get; }

        public ulong Start { get; }

        public ulong End => this.Start + this.Size;

    }
}
