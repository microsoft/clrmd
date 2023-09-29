// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Extensions;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrHeapHelpers : IClrHeapHelpers
    {
        private readonly SOSDac _sos;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly IMemoryReader _memoryReader;
        private readonly GCState _gcState;
        private HashSet<ulong>? _validMethodTables;

        private const uint SyncBlockRecLevelMask = 0x0000FC00;
        private const int SyncBlockRecLevelShift = 10;
        private const uint SyncBlockThreadIdMask = 0x000003FF;
        private const uint SyncBlockSpinLock = 0x10000000;
        private const uint SyncBlockHashOrSyncBlockIndex = 0x08000000;

        public ClrHeapHelpers(SOSDac sos, SOSDac8? sos8, SosDac12? sos12, IMemoryReader reader, in GCState gcState)
        {
            _sos = sos;
            _sos8 = sos8;
            _sos12 = sos12;
            _memoryReader = reader;
            _gcState = gcState;
        }

        public IEnumerable<MemoryRange> EnumerateThreadAllocationContexts()
        {
            if (_sos12 is not null && _sos12.GetGlobalAllocationContext(out ulong allocPointer, out ulong allocLimit))
            {
                if (allocPointer < allocLimit)
                    yield return new(allocPointer, allocLimit);
            }

            if (!_sos.GetThreadStoreData(out ThreadStoreData threadStore))
                yield break;

            ulong address = threadStore.FirstThread;
            for (int i = 0; i < threadStore.ThreadCount && address != 0; i++)
            {
                if (!_sos.GetThreadData(address, out ThreadData thread))
                    break;

                if (thread.AllocationContextPointer < thread.AllocationContextLimit)
                    yield return new(thread.AllocationContextPointer, thread.AllocationContextLimit);

                address = thread.NextThread;
            }
        }

        public IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles()
        {
            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles(ClrHandleKind.Dependent);
            if (handleEnum is null)
                yield break;

            foreach (HandleData handle in handleEnum.ReadHandles())
            {
                if (handle.Type == (int)ClrHandleKind.Dependent)
                {
                    ulong obj = _memoryReader.ReadPointer(handle.Handle);
                    if (obj != 0)
                        yield return (obj, handle.Secondary);
                }
            }
        }

        public IEnumerable<SyncBlockInfo> EnumerateSyncBlocks()
        {
            HResult hr = _sos.GetSyncBlockData(1, out SyncBlockData data);
            if (!hr || data.TotalSyncBlockCount == 0)
                yield break;

            int max = data.TotalSyncBlockCount >= int.MaxValue ? int.MaxValue : (int)data.TotalSyncBlockCount;

            int curr = 1;
            do
            {
                if (data.Free == 0)
                {
                    yield return new()
                    {
                        Index = curr,
                        Address = data.Address,
                        Object = data.Object,
                        AppDomain = data.AppDomain,
                        AdditionalThreadCount = data.AdditionalThreadCount.ToSigned(),
                        COMFlags = (SyncBlockComFlags)data.COMFlags,
                        HoldingThread = data.HoldingThread,
                        MonitorHeldCount = data.MonitorHeld.ToSigned(),
                        Recursion = data.Recursion.ToSigned()
                    };
                }

                curr++;
                if (curr > max)
                    break;

                hr = _sos.GetSyncBlockData(curr, out data);
            } while (hr);
        }

        public ImmutableArray<ClrSubHeap> GetSubHeaps(ClrHeap heap)
        {
            if (_gcState.Kind == AbstractDac.GCKind.Server)
            {
                ClrDataAddress[] heapAddresses = _sos.GetHeapList(_gcState.HeapCount);
                ImmutableArray<ClrSubHeap>.Builder heapsBuilder = ImmutableArray.CreateBuilder<ClrSubHeap>(heapAddresses.Length);
                for (int i = 0; i < heapAddresses.Length; i++)
                {
                    if (_sos.GetServerHeapDetails(heapAddresses[i], out HeapDetails heapData))
                    {
                        GenerationData[] genData = heapData.GenerationTable;
                        IEnumerable<ClrDataAddress> finalization = heapData.FinalizationFillPointers.Take(6);

                        if (_sos8 is not null)
                        {
                            genData = _sos8.GetGenerationTable(heapAddresses[i]) ?? genData;
                            finalization = _sos8.GetFinalizationFillPointers(heapAddresses[i]) ?? finalization;
                        }

                        heapsBuilder.Add(new(this, heap, i, heapAddresses[i], heapData, genData, finalization.Select(addr => (ulong)addr)));
                    }
                }

                return heapsBuilder.MoveOrCopyToImmutable();
            }
            else
            {
                if (_sos.GetWksHeapDetails(out HeapDetails heapData))
                {
                    GenerationData[] genData = heapData.GenerationTable;
                    IEnumerable<ClrDataAddress> finalization = heapData.FinalizationFillPointers.Take(6);

                    if (_sos8 is not null)
                    {
                        genData = _sos8.GetGenerationTable() ?? genData;
                        finalization = _sos8.GetFinalizationFillPointers() ?? finalization;
                    }

                    return ImmutableArray.Create(new ClrSubHeap(this, heap, 0, 0, heapData, genData, finalization.Select(addr => (ulong)addr)));
                }
            }

            return ImmutableArray<ClrSubHeap>.Empty;
        }

        public IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap)
        {
            HashSet<ulong> seen = new() { 0 };
            IEnumerable<ClrSegment> segments = EnumerateSegments(heap, 3, seen);
            segments = segments.Concat(EnumerateSegments(heap, 2, seen));
            if (heap.HasRegions)
            {
                segments = segments.Concat(EnumerateSegments(heap, 1, seen));
                segments = segments.Concat(EnumerateSegments(heap, 0, seen));
            }

            if (heap.GenerationTable.Length > 4)
                segments = segments.Concat(EnumerateSegments(heap, 4, seen));

            return segments;
        }

        private IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap, int generation, HashSet<ulong> seen)
        {
            ulong address = heap.GenerationTable[generation].StartSegment;

            while (address != 0 && seen.Add(address))
            {
                ClrSegment? segment = CreateSegment(heap, address, generation);

                if (segment is null)
                    break;

                yield return segment;
                address = segment.Next;
            }
        }

        private ClrSegment? CreateSegment(ClrSubHeap subHeap, ulong address, int generation)
        {
            if (!_sos.GetSegmentData(address, out SegmentData data))
                return null;

            ClrSegmentFlags flags = (ClrSegmentFlags)data.Flags;
            GCSegmentKind kind = GCSegmentKind.Generation2;
            if ((flags & ClrSegmentFlags.ReadOnly) == ClrSegmentFlags.ReadOnly)
            {
                kind = GCSegmentKind.Frozen;
            }
            else if (generation == 3)
            {
                kind = GCSegmentKind.Large;
            }
            else if (generation == 4)
            {
                kind = GCSegmentKind.Pinned;
            }
            else
            {
                // We are not a Frozen, Large, or Pinned segment/region:
                if (subHeap.HasRegions)
                {
                    if (generation == 0)
                        kind = GCSegmentKind.Generation0;
                    else if (generation == 1)
                        kind = GCSegmentKind.Generation1;
                    else if (generation == 2)
                        kind = GCSegmentKind.Generation2;
                }
                else
                {
                    if (subHeap.EphemeralHeapSegment == address)
                        kind = GCSegmentKind.Ephemeral;
                    else
                        kind = GCSegmentKind.Generation2;
                }
            }

            // The range of memory occupied by allocated objects
            MemoryRange allocated = new(data.Start, subHeap.EphemeralHeapSegment == address ? subHeap.Allocated : (ulong)data.Allocated);

            // There's a bit of calculation involved with finding the committed start.
            // For regions, it's "allocated.Start - sizeof(aligned_plug_and_gap)".
            // For segments, it's adjusted by segment_info_size which can be different based
            // on whether background GC is enabled.  Since we don't have that information, we'll
            // use a heuristic here and hope for the best.

            ulong committedStart;

            if (kind == GCSegmentKind.Frozen)
                committedStart = allocated.Start - (uint)IntPtr.Size;
            else if ((allocated.Start & 0x1ffful) == 0x1000)
                committedStart = allocated.Start - 0x1000;
            else
                committedStart = allocated.Start & ~0xffful;

            MemoryRange committed, gen0, gen1, gen2;
            if (subHeap.HasRegions)
            {
                committed = new(committedStart, data.Committed);
                gen0 = default;
                gen1 = default;
                gen2 = default;

                switch (generation)
                {
                    case 0:
                        gen0 = new(allocated.Start, allocated.End);
                        break;

                    case 1:
                        gen1 = new(allocated.Start, allocated.End);
                        break;

                    default:
                        gen2 = new(allocated.Start, allocated.End);
                        break;
                }
            }
            else
            {
                committed = new(committedStart, data.Committed);
                if (kind == GCSegmentKind.Ephemeral)
                {
                    gen0 = new(subHeap.GenerationTable[0].AllocationStart, allocated.End);
                    gen1 = new(subHeap.GenerationTable[1].AllocationStart, gen0.Start);
                    gen2 = new(allocated.Start, gen1.Start);
                }
                else
                {
                    gen0 = default;
                    gen1 = default;
                    gen2 = allocated;
                }
            }

            // The range of memory reserved
            MemoryRange reserved = new(committed.End, data.Reserved);

            return new ClrSegment(subHeap)
            {
                Address = data.Address,
                Kind = kind,
                ObjectRange = allocated,
                CommittedMemory = committed,
                ReservedMemory = reserved,
                Generation0 = gen0,
                Generation1 = gen1,
                Generation2 = gen2,
                Flags = flags,
                Next = data.Next,
                BackgroundAllocated = data.BackgroundAllocated,
            };
        }

        public (ulong Thread, int Recursion) GetThinLock(uint header)
        {
            if (!HasThinlock(header))
                return default;

            (uint threadId, uint recursion) = GetThinlockData(header);
            ulong threadAddress = _sos.GetThreadFromThinlockId(threadId);

            if (threadAddress == 0)
                return default;

            return (threadAddress, recursion.ToSigned());
        }

        private static bool HasThinlock(uint header)
        {
            return (header & (SyncBlockHashOrSyncBlockIndex | SyncBlockSpinLock)) == 0 && (header & SyncBlockThreadIdMask) != 0;
        }

        private static (uint ThreadId, uint Recursion) GetThinlockData(uint header)
        {
            uint threadId = header & SyncBlockThreadIdMask;
            uint recursion = (header & SyncBlockRecLevelMask) >> SyncBlockRecLevelShift;

            return (threadId, recursion);
        }

        public bool IsValidMethodTable(ulong mt)
        {
            // clear the mark bit
            mt &= ~1ul;

            HashSet<ulong> validMts = _validMethodTables ??= new();
            lock (validMts)
                if (validMts.Contains(mt))
                    return true;

            bool verified = _sos.GetMethodTableData(mt, out _);
            if (verified)
            {
                lock (validMts)
                    validMts.Add(mt);
            }

            return verified;
        }

        public MemoryRange GetInternalRootArray(ClrSubHeap subHeap)
        {
            DacHeapAnalyzeData analyzeData;
            if (subHeap.Heap.IsServer)
                _sos.GetHeapAnalyzeData(subHeap.Address, out analyzeData);
            else
                _sos.GetHeapAnalyzeData(out analyzeData);

            if (analyzeData.InternalRootArray == 0 || analyzeData.InternalRootArrayIndex == 0)
                return default;

            ulong end = analyzeData.InternalRootArray + (uint)_memoryReader.PointerSize * analyzeData.InternalRootArrayIndex;
            return new(analyzeData.InternalRootArray, end);
        }

        public ClrOutOfMemoryInfo? GetOOMInfo(ClrSubHeap subHeap)
        {
            DacOOMData oomData;
            if (subHeap.Heap.IsServer)
            {
                if (!_sos.GetOOMData(subHeap.Address, out oomData) || (oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None))
                    return null;
            }
            else
            {
                if (!_sos.GetOOMData(out oomData) || (oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None))
                    return null;
            }

            return new ClrOutOfMemoryInfo(oomData);
        }
    }
}