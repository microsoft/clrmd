// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2SegmentData : ISegmentData
    {
        public ulong segmentAddr;
        public ulong allocated;
        public ulong committed;
        public ulong reserved;
        public ulong used;
        public ulong mem;
        public ulong next;
        public ulong gc_heap;
        public ulong highAllocMark;

        public ulong Address => segmentAddr;
        public ulong Next => next;
        public ulong Start => mem;
        public ulong End => allocated;
        public ulong Reserved => reserved;
        public ulong Committed => committed;
    }
}