// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// This interface is used to construct the ClrHeap object and is required
    /// because ClrHeap is the parent of all ClrTypes.
    ///
    /// This interface is required.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractHeap
    {
        ref readonly GCState State { get; }
        IEnumerable<MemoryRange> EnumerateThreadAllocationContexts();
        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles();
        IEnumerable<SyncBlockInfo> EnumerateSyncBlocks();
        IEnumerable<SubHeapInfo> EnumerateSubHeaps();
        (ulong Thread, int Recursion) GetThinLock(uint header);
        bool IsValidMethodTable(ulong mt);
        MemoryRange GetInternalRootArray(ulong subHeapAddress);
        bool GetOOMInfo(ulong subHeapAddress, out OomInfo oomInfo);
        int? GetDynamicAdaptationMode();
    }

    public struct GCState
    {
        public GCKind Kind { get; set; }
        public bool AreGCStructuresValid { get; set; }
        public int HeapCount { get; set; }
        public int MaxGeneration { get; set; }
        public ulong StringMethodTable { get; set; }
        public ulong ObjectMethodTable { get; set; }
        public ulong ExceptionMethodTable { get; set; }
        public ulong FreeMethodTable { get; set; }
    }

    public enum GCKind
    {
        Unknown,
        Workstation,
        Server,
    }


    public struct SubHeapInfo
    {
        public SubHeapInfo() { }

        public ulong Address { get; set; }
        public int HeapIndex { get; set; }

        public ulong Allocated { get; set; }
        public ulong MarkArray { get; set; }
        public HeapMarkState State { get; set; }
        public ulong CurrentSweepPosition { get; set; }
        public ulong SavedSweepEphemeralSegment { get; set; }
        public ulong SavedSweepEphemeralStart { get; set; }
        public ulong BackgroundSavedLowestAddress { get; set; }
        public ulong BackgroundSavedHighestAddress { get; set; }
        public ulong EphemeralHeapSegment { get; set; }
        public ulong LowestAddress { get; set; }
        public ulong HighestAddress { get; set; }
        public ulong CardTable { get; set; }
        public ulong EphemeralAllocContextPointer { get; set; }
        public ulong EphemeralAllocContextLimit { get; set; }

        public SegmentInfo[] Segments { get; set; } = Array.Empty<SegmentInfo>();
        public GenerationInfo[] Generations { get; set; } = Array.Empty<GenerationInfo>();
        public ulong[] FinalizationPointers { get; set; } = Array.Empty<ulong>();

        public readonly bool HasRegions => Generations.Length >= 2 && Generations[0].StartSegment != Generations[1].StartSegment;
    }

    public enum HeapMarkState
    {
        Marking,
        Planning,
        Free
    }

    public struct GenerationInfo
    {
        public ulong StartSegment { get; set; }
        public ulong AllocationStart { get; set; }
        public ulong AllocationContextPointer { get; set; }
        public ulong AllocationContextLimit { get; set; }
    }

    public struct SegmentInfo
    {
        public ulong Address { get; set; }
        public GCSegmentKind Kind { get; set; }
        public MemoryRange ObjectRange { get; set; }
        public MemoryRange CommittedMemory { get; set; }
        public MemoryRange ReservedMemory { get; set; }
        public MemoryRange Generation0 { get; set; }
        public MemoryRange Generation1 { get; set; }
        public MemoryRange Generation2 { get; set; }
        public ClrSegmentFlags Flags { get; set; }
        public ulong BackgroundAllocated { get; set; }
        public ulong Next { get; set; }
    }

    public struct SyncBlockInfo
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

    public struct OomInfo
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