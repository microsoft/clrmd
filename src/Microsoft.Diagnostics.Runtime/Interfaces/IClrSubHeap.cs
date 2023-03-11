// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrSubHeap
    {
        ulong Address { get; }
        MemoryRange AllocationContext { get; }
        bool HasBackgroundGC { get; }
        bool HasPinnedObjectHeap { get; }
        bool HasRegions { get; }
        IClrHeap Heap { get; }
        int Index { get; }
        ImmutableArray<IClrSegment> Segments { get; }
    }
}