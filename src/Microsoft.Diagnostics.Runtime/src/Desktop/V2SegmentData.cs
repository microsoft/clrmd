// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2SegmentData : ISegmentData
    {
        public readonly ulong SegmentAddr;
        public readonly ulong Allocated;
        public readonly ulong Committed;
        public readonly ulong Reserved;
        public readonly ulong Used;
        public readonly ulong Mem;
        public readonly ulong Next;
        public readonly ulong GCHeap;
        public readonly ulong HighAllocMark;

        ulong ISegmentData.Address => SegmentAddr;
        ulong ISegmentData.Next => Next;
        ulong ISegmentData.Start => Mem;
        ulong ISegmentData.End => Allocated;
        ulong ISegmentData.Reserved => Reserved;
        ulong ISegmentData.Committed => Committed;
    }
}