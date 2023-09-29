// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrHeapHelpers
    {
        IEnumerable<MemoryRange> EnumerateThreadAllocationContexts();
        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles();
        IEnumerable<SyncBlock> EnumerateSyncBlocks();
        ImmutableArray<ClrSubHeap> GetSubHeaps(ClrHeap heap);
        IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap);
        ClrThinLock? GetThinLock(ClrHeap clrHeap, uint header);
        int VerifyObject(SyncBlockContainer syncBlocks, ClrSegment seg, ClrObject obj, Span<ObjectCorruption> result);
        bool IsValidMethodTable(ulong mt);
        MemoryRange GetInternalRootArray(ClrSubHeap subHeap);
        ClrOutOfMemoryInfo? GetOOMInfo(ClrSubHeap subHeap);
    }
}