// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Extensions;
using Microsoft.Diagnostics.Runtime.Utilities;
using GCKind = Microsoft.Diagnostics.Runtime.AbstractDac.GCKind;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal sealed class DacHeap : IAbstractHeap
    {
        private readonly SOSDac _sos;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly ISOSDac16? _sos16;
        private readonly IMemoryReader _memoryReader;
        private readonly GCState _gcState;
        private HashSet<ulong>? _validMethodTables;

        private const uint SyncBlockRecLevelMask = 0x0000FC00;
        private const int SyncBlockRecLevelShift = 10;
        private const uint SyncBlockThreadIdMask = 0x000003FF;
        private const uint SyncBlockSpinLock = 0x10000000;
        private const uint SyncBlockHashOrSyncBlockIndex = 0x08000000;

        public DacHeap(SOSDac sos, SOSDac8? sos8, SosDac12? sos12, ISOSDac16? sos16, IMemoryReader reader, in GCInfo gcInfo, in CommonMethodTables commonMethodTables)
        {
            _sos = sos;
            _sos8 = sos8;
            _sos12 = sos12;
            _sos16 = sos16;
            _memoryReader = reader;
            _gcState = new()
            {
                Kind = gcInfo.ServerMode != 0 ? GCKind.Server : GCKind.Workstation,
                AreGCStructuresValid = gcInfo.GCStructuresValid != 0,
                HeapCount = gcInfo.HeapCount,
                MaxGeneration = gcInfo.MaxGeneration,
                ExceptionMethodTable = commonMethodTables.ExceptionMethodTable,
                FreeMethodTable = commonMethodTables.FreeMethodTable,
                ObjectMethodTable = commonMethodTables.ObjectMethodTable,
                StringMethodTable = commonMethodTables.StringMethodTable,
            };
        }

        public ref readonly GCState State { get => ref _gcState; }

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

        public IEnumerable<SubHeapInfo> EnumerateSubHeaps()
        {
            if (_gcState.Kind == AbstractDac.GCKind.Server)
            {
                ClrDataAddress[] heapAddresses = _sos.GetHeapList(_gcState.HeapCount);
                for (int i = 0; i < heapAddresses.Length; i++)
                {
                    if (_sos.GetServerHeapDetails(heapAddresses[i], out HeapDetails heapData))
                    {
                        SubHeapInfo subHeapInfo = CreateSubHeapInfo(heapAddresses[i], i, heapData);
                        yield return subHeapInfo;
                    }
                }
            }
            else
            {
                if (_sos.GetWksHeapDetails(out HeapDetails heapData))
                {
                    SubHeapInfo subHeapInfo = CreateSubHeapInfo(0, 0, heapData);
                    yield return subHeapInfo;
                }
            }
        }

        private SubHeapInfo CreateSubHeapInfo(ulong address, int i, HeapDetails heapData)
        {
            GenerationData[] genData = heapData.GenerationTable;
            IEnumerable<ClrDataAddress> finalization = heapData.FinalizationFillPointers.Take(6);

            if (_sos8 is not null)
            {
                genData = (address != 0 ? _sos8.GetGenerationTable(address) : _sos8.GetGenerationTable()) ?? genData;
                finalization = (address != 0 ? _sos8.GetFinalizationFillPointers(address) : _sos8.GetFinalizationFillPointers()) ?? finalization;
            }

            SubHeapInfo subHeapInfo = new()
            {
                Address = address,
                HeapIndex = i,
                Allocated = heapData.Allocated,
                MarkArray = heapData.MarkArray,
                State = (HeapMarkState)(ulong)heapData.CurrentGCState,
                CurrentSweepPosition = heapData.NextSweepObj,
                SavedSweepEphemeralSegment = heapData.SavedSweepEphemeralSeg,
                SavedSweepEphemeralStart = heapData.SavedSweepEphemeralStart,
                BackgroundSavedLowestAddress = heapData.BackgroundSavedLowestAddress,
                BackgroundSavedHighestAddress = heapData.BackgroundSavedHighestAddress,
                EphemeralHeapSegment = heapData.EphemeralHeapSegment,
                LowestAddress = heapData.LowestAddress,
                HighestAddress = heapData.HighestAddress,
                CardTable = heapData.CardTable,
                EphemeralAllocContextPointer = heapData.EphemeralAllocContextPtr,
                EphemeralAllocContextLimit = heapData.EphemeralAllocContextLimit,

                FinalizationPointers = finalization.Select(r => (ulong)r).ToArray(),
                Generations = ConvertGenerations(genData),
            };

            subHeapInfo.Segments = EnumerateSegments(subHeapInfo).ToArray();
            return subHeapInfo;
        }

        private static GenerationInfo[] ConvertGenerations(GenerationData[] genData)
        {
            var result = new GenerationInfo[genData.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = new()
                {
                    AllocationStart = genData[i].AllocationStart,
                    StartSegment = genData[i].StartSegment,
                    AllocationContextLimit = genData[i].AllocationContextLimit,
                    AllocationContextPointer = genData[i].AllocationContextPointer,
                };

            return result;
        }

        private IEnumerable<SegmentInfo> EnumerateSegments(in SubHeapInfo heap)
        {
            HashSet<ulong> seen = new() { 0 };
            IEnumerable<SegmentInfo> segments = EnumerateSegments(heap, 3, seen);
            segments = segments.Concat(EnumerateSegments(heap, 2, seen));
            if (heap.HasRegions)
            {
                segments = segments.Concat(EnumerateSegments(heap, 1, seen));
                segments = segments.Concat(EnumerateSegments(heap, 0, seen));
            }

            if (heap.Generations.Length > 4)
                segments = segments.Concat(EnumerateSegments(heap, 4, seen));

            return segments;
        }

        private IEnumerable<SegmentInfo> EnumerateSegments(SubHeapInfo heap, int generation, HashSet<ulong> seen)
        {
            ulong address = heap.Generations[generation].StartSegment;

            while (address != 0 && seen.Add(address))
            {
                if (!TryCreateSegment(heap, address, generation, out SegmentInfo segInfo))
                    break;

                yield return segInfo;
                address = segInfo.Next;
            }
        }

        private bool TryCreateSegment(SubHeapInfo subHeap, ulong address, int generation, out SegmentInfo segInfo)
        {
            if (!_sos.GetSegmentData(address, out SegmentData data))
            {
                segInfo = default;
                return false;
            }

            var flags = (ClrSegmentFlags)data.Flags;
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
                    gen0 = new(subHeap.Generations[0].AllocationStart, allocated.End);
                    gen1 = new(subHeap.Generations[1].AllocationStart, gen0.Start);
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

            segInfo = new()
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
            return true;
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

        public MemoryRange GetInternalRootArray(ulong subHeapAddress)
        {
            DacHeapAnalyzeData analyzeData;
            if (subHeapAddress != 0)
                _sos.GetHeapAnalyzeData(subHeapAddress, out analyzeData);
            else
                _sos.GetHeapAnalyzeData(out analyzeData);

            if (analyzeData.InternalRootArray == 0 || analyzeData.InternalRootArrayIndex == 0)
                return default;

            ulong end = analyzeData.InternalRootArray + (uint)_memoryReader.PointerSize * analyzeData.InternalRootArrayIndex;
            return new(analyzeData.InternalRootArray, end);
        }

        public bool GetOOMInfo(ulong subHeapAddress, out OomInfo oomInfo)
        {
            DacOOMData oomData;
            if (subHeapAddress != 0)
            {
                if (!_sos.GetOOMData(subHeapAddress, out oomData) || oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None)
                {
                    oomInfo = default;
                    return false;
                }
            }
            else
            {
                if (!_sos.GetOOMData(out oomData) || oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None)
                {
                    oomInfo = default;
                    return false;
                }
            }

            oomInfo = new()
            {
                AllocSize = oomData.AllocSize,
                AvailablePageFileMB = oomData.AvailablePageFileMB,
                GCIndex = oomData.GCIndex,
                GetMemoryFailure = oomData.GetMemoryFailure,
                IsLOH = oomData.IsLOH != 0,
                Reason = oomData.Reason,
                Size = oomData.Size,
            };
            return true;
        }

        public int? GetDynamicAdaptationMode()
        {
            if (_sos16 != null)
            {
                return _sos16.GetDynamicAdaptationMode();
            }
            else
            {
                return null;
            }
        }
    }
}