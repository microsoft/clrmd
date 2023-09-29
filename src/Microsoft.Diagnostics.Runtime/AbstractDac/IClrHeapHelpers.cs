// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrHeapHelpers
    {
        IEnumerable<MemoryRange> EnumerateThreadAllocationContexts();
        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles();
        IEnumerable<SyncBlockInfo> EnumerateSyncBlocks();
        ImmutableArray<ClrSubHeap> GetSubHeaps(ClrHeap heap);
        IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap);
        (ulong Thread, int Recursion) GetThinLock(uint header);
        bool IsValidMethodTable(ulong mt);
        MemoryRange GetInternalRootArray(ClrSubHeap subHeap);
        bool GetOOMInfo(ulong subHeapAddress, out OomInfo oomInfo);
    }

    internal struct SyncBlockInfo
    {
        public int Index { get; set; }
        public ulong Address { get; set; }
        public ulong Object { get; set; }
        public SyncBlockComFlags COMFlags { get; set; }
        public int MonitorHeldCount { get; set; }
        public int Recursion { get; set; }
        public ulong HoldingThread { get; set; }
        public int AdditionalThreadCount { get; set; }
        public ulong AppDomain { get; set; }
    }

    internal struct OomInfo
    {
        public OutOfMemoryReason Reason { get; set; }
        public ulong AllocSize { get; set; }
        public ulong AvailablePageFileMB { get; set; }
        public ulong GCIndex { get; set; }
        public GetMemoryFailureReason GetMemoryFailure { get; set; }
        public ulong Size { get; set; }
        public bool IsLOH { get; set; }
    }
}