// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IClrHeapHelpers
    {
        IClrTypeFactory CreateTypeFactory(ClrHeap heap);

        bool IsServerMode { get; }
        bool AreGCStructuresValid { get; }
        IEnumerable<MemoryRange> EnumerateThreadAllocationContexts();
        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles();
        IEnumerable<SyncBlock> EnumerateSyncBlocks();
        ImmutableArray<ClrSubHeap> GetSubHeaps(ClrHeap heap);
        IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap);
        ClrThinLock? GetThinLock(ClrHeap clrHeap, uint header);
        ObjectCorruption? VerifyObject(SyncBlockContainer syncBlocks, ClrSegment seg, ClrObject obj);
        bool IsValidMethodTable(ulong mt);
    }
}
