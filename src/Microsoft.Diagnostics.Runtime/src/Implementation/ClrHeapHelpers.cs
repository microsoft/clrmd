// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrHeapHelpers : IClrHeapHelpers
    {
        private readonly ClrDataProcess _clrDataProcess;
        private readonly SOSDac _sos;
        private readonly SOSDac8? _sos8;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac12? _sos12;
        private readonly IMemoryReader _memoryReader;
        private readonly CacheOptions _cacheOptions;
        private readonly GCInfo _gcInfo;

        public bool IsServerMode => _gcInfo.ServerMode != 0;
        public bool AreGCStructuresValid => _gcInfo.GCStructuresValid != 0;
        public ulong SizeOfPlugAndGap { get; }

        public ClrHeapHelpers(ClrDataProcess clrDataProcess, SOSDac sos, SOSDac6? sos6, SOSDac8? sos8, SOSDac12? sos12, IMemoryReader reader, CacheOptions cacheOptions)
        {
            _clrDataProcess = clrDataProcess;
            _sos = sos;
            _sos8 = sos8;
            _sos6 = sos6;
            _sos12 = sos12;
            _memoryReader = reader;
            _cacheOptions = cacheOptions;
            SizeOfPlugAndGap = (ulong)_memoryReader.PointerSize * 4;

            if (!_sos.GetGCHeapData(out _gcInfo))
                _gcInfo = default; // Ensure _gcInfo.GCStructuresValid == false.
        }

        public IClrTypeFactory CreateTypeFactory(ClrHeap heap) => new ClrTypeFactory(heap, _clrDataProcess, _sos, _sos6, _sos8, _cacheOptions);

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

            HandleData[] handles;
            try
            {
                // Yes this is a huge array.  Older versions of ISOSHandleEnum have a memory leak when
                // we loop below.  If we can fill the array without having to call back into
                // SOSHandleEnum.ReadHandles then we avoid that leak entirely.
                handles = new HandleData[0x18000];
            }
            catch (OutOfMemoryException)
            {
                handles = new HandleData[256];
            }

            int fetched;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    if (handles[i].Type == (int)ClrHandleKind.Dependent)
                    {
                        ulong obj = _memoryReader.ReadPointer(handles[i].Handle);
                        if (obj != 0)
                            yield return (obj, handles[i].Secondary);
                    }
                }
            }
        }

        public IEnumerable<SyncBlock> EnumerateSyncBlocks()
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
                    if (data.MonitorHeld != 0 || data.HoldingThread != 0 || data.Recursion != 0 || data.AdditionalThreadCount != 0)
                        yield return new FullSyncBlock(data);
                    else if (data.COMFlags != 0)
                        yield return new ComSyncBlock(data.Object, data.COMFlags);
                    else
                        yield return new SyncBlock(data.Object);
                }

                curr++;
                if (curr > max)
                    break;

                hr = _sos.GetSyncBlockData(curr, out data);
            } while (hr);
        }

        public ImmutableArray<ClrSubHeap> GetSubHeaps(ClrHeap heap)
        {
            if (IsServerMode)
            {
                ClrDataAddress[] heapAddresses = _sos.GetHeapList(_gcInfo.HeapCount);
                var heapsBuilder = ImmutableArray.CreateBuilder<ClrSubHeap>(heapAddresses.Length);
                for (int i = 0; i < heapAddresses.Length; i++)
                {
                    if (_sos.GetServerHeapDetails(heapAddresses[i], out HeapDetails heapData))
                    {
                        GenerationData[] genData = heapData.GenerationTable;
                        ClrDataAddress[] finalization = heapData.FinalizationFillPointers;

                        if (_sos8 is not null)
                        {
                            genData = _sos8.GetGenerationTable(heapAddresses[i]) ?? genData;
                            finalization = _sos8.GetFinalizationFillPointers(heapAddresses[i]) ?? finalization;
                        }

                        heapsBuilder.Add(new(this, heap, i, heapAddresses[i], heapData, genData, finalization.Select(addr => (ulong)addr)));
                    }
                }

                return heapsBuilder.MoveToImmutable();
            }
            else
            {
                if (_sos.GetWksHeapDetails(out HeapDetails heapData))
                {
                    GenerationData[] genData = heapData.GenerationTable;
                    ClrDataAddress[] finalization = heapData.FinalizationFillPointers;

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
            const nint heap_segment_flags_readonly = 1;

            if (!_sos.GetSegmentData(address, out SegmentData data))
                return null;

            bool ro = (data.Flags & heap_segment_flags_readonly) == heap_segment_flags_readonly;

            GCSegmentKind kind = GCSegmentKind.Generation2;
            if (ro)
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

            MemoryRange committed, gen0, gen1, gen2;
            if (subHeap.HasRegions)
            {
                committed = new(allocated.Start - SizeOfPlugAndGap, data.Committed);
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
                committed = new(allocated.Start, data.Committed);
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
                Next = data.Next,
            };
        }
    }
}
