// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

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
        private HashSet<ulong>? _validMethodTables;

        private const uint SyncBlockRecLevelMask = 0x0000FC00;
        private const int SyncBlockRecLevelShift = 10;
        private const uint SyncBlockThreadIdMask = 0x000003FF;
        private const uint SyncBlockSpinLock = 0x10000000;
        private const uint SyncBlockHashOrSyncBlockIndex = 0x08000000;
        private const uint SyncBlockHashCodeIndex = 0x04000000;
        private const int SyncBlockIndexBits = 26;
        private const uint SyncBlockIndexMask = ((1u << SyncBlockIndexBits) - 1u);

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
                        yield return new FullSyncBlock(data, curr);
                    else if (data.COMFlags != 0)
                        yield return new ComSyncBlock(data.Object, curr, data.COMFlags);
                    else
                        yield return new SyncBlock(data.Object, curr);
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
                ImmutableArray<ClrSubHeap>.Builder heapsBuilder = ImmutableArray.CreateBuilder<ClrSubHeap>(heapAddresses.Length);
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
                Flags = flags,
                Next = data.Next,
                BackgroundAllocated = data.BackgroundAllocated,
            };
        }

        public ClrThinLock? GetThinLock(ClrHeap heap, uint header)
        {
            if (!HasThinlock(header))
                return null;

            (uint threadId, uint recursion) = ClrHeapHelpers.GetThinlockData(header);
            ulong threadAddress = _sos.GetThreadFromThinlockId(threadId);

            if (threadAddress == 0)
                return null;

            ClrThread? thread = heap.Runtime.Threads.FirstOrDefault(t => t.Address == threadAddress);
            return new ClrThinLock(thread, (int)recursion);
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

        public ObjectCorruption? VerifyObject(SyncBlockContainer syncBlocks, ClrSegment seg, ClrObject obj)
        {
            // Is the object address pointer aligned?
            if ((obj.Address & ((uint)_memoryReader.PointerSize - 1)) != 0)
                return new ObjectCorruption(obj, 0, ObjectCorruptionKind.ObjectNotPointerAligned);

            if (!obj.IsFree)
            {
                // Can we read the method table?
                if (!_memoryReader.Read(obj.Address, out ulong mt))
                    return new ObjectCorruption(obj, 0, ObjectCorruptionKind.CouldNotReadMethodTable);

                // Is the method table we read valid?
                if (!IsValidMethodTable(mt))
                    return new ObjectCorruption(obj, 0, ObjectCorruptionKind.InvalidMethodTable);

                // This shouldn't happen if VerifyMethodTable above returns success, but we'll make sure.
                if (obj.Type is null)
                    return new ObjectCorruption(obj, 0, ObjectCorruptionKind.InvalidMethodTable);
            }

            // Check object size
            int intSize = obj.Size > int.MaxValue ? int.MaxValue : (int)obj.Size;
            if (obj + obj.Size > seg.ObjectRange.End || (!obj.IsFree && obj.Size > seg.MaxObjectSize))
                return new ObjectCorruption(obj, _memoryReader.PointerSize, ObjectCorruptionKind.ObjectTooLarge);

            if (obj.IsFree)
                return null;

            // Validate members
            bool verifyMembers;
            try
            {
                // Type can't be null, we checked above.  The compiler just get lost in the IsFree checks.
                verifyMembers = obj.Type!.ContainsPointers && ShouldVerifyMembers(seg, obj);
            }
            catch (IOException)
            {
                return new ObjectCorruption(obj, 0, ObjectCorruptionKind.CouldNotReadCardTable);
            }

            if (verifyMembers)
            {
                GCDesc gcdesc = obj.Type!.GCDesc;
                if (gcdesc.IsEmpty)
                    return new ObjectCorruption(obj, 0, ObjectCorruptionKind.CouldNotReadGCDesc);

                ulong freeMt = seg.SubHeap.Heap.FreeType.MethodTable;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                if (read != intSize)
                    return new ObjectCorruption(obj, read >= 0 ? read : 0, ObjectCorruptionKind.CouldNotReadObject);

                foreach ((ulong objRef, int offset) in gcdesc.WalkObject(buffer, intSize))
                {
                    if ((objRef & ((uint)_memoryReader.PointerSize - 1)) != 0)
                        return new ObjectCorruption(obj, offset, ObjectCorruptionKind.ObjectReferenceNotPointerAligned);
                    if (!_memoryReader.Read(objRef, out ulong mt) || !IsValidMethodTable(mt))
                        return new ObjectCorruption(obj, offset, ObjectCorruptionKind.InvalidObjectReference);
                    else if ((mt & ~1ul) == freeMt)
                        return new ObjectCorruption(obj, offset, ObjectCorruptionKind.FreeObjectReference);
                }

                ArrayPool<byte>.Shared.Return(buffer);
            }

            // Object header validation tests:
            uint objHeader = _memoryReader.Read<uint>(obj - sizeof(uint));

            // Validate SyncBlock
            SyncBlock? blk = syncBlocks.TryGetSyncBlock(obj);
            if ((objHeader & SyncBlockHashOrSyncBlockIndex) != 0 && (objHeader & SyncBlockHashCodeIndex) == 0)
            {
                uint index = (objHeader & SyncBlockIndexMask);
                int clrIndex = blk?.Index ?? -1;

                if (index == 0)
                    return new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockZero, -1, clrIndex);
                else if (index != clrIndex)
                    return new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockMismatch, (int)index, clrIndex);
            }
            else if (blk is not null)
            {
                return new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockMismatch, -1, blk.Index);
            }

            // Validate Thinlock
            if (HasThinlock(objHeader))
            {
                ClrRuntime runtime = seg.SubHeap.Heap.Runtime;
                (uint threadId, _) = ClrHeapHelpers.GetThinlockData(objHeader);
                ulong address = _sos.GetThreadFromThinlockId(threadId);
                if (address == 0 || !runtime.Threads.Any(th => th.Address == address))
                    return new ObjectCorruption(obj, -4, ObjectCorruptionKind.InvalidThinlock);
            }

            return null;
        }

        private bool ShouldVerifyMembers(ClrSegment seg, ClrObject obj)
        {
            ClrHeapHelpers.ShouldCheckBgcMark(seg, out bool considerBgcMark, out bool checkCurrentSweep, out bool checkSavedSweep);
            return FgcShouldConsiderObject(seg, obj, considerBgcMark, checkCurrentSweep, checkSavedSweep);
        }

        private bool FgcShouldConsiderObject(ClrSegment seg, ClrObject obj, bool considerBgcMark, bool checkCurrentSweep, bool checkSavedSweep)
        {
            ClrSubHeap heap = seg.SubHeap;
            bool noBgcMark = false;
            if (considerBgcMark)
            {
                if (checkCurrentSweep && obj < heap.NextSweepObject)
                {
                    noBgcMark = true;
                }
                else
                {
                    if (checkSavedSweep)
                    {
                        if (obj >= heap.SavedSweepEphemeralStart)
                            noBgcMark = true;
                    }
                    else
                    {
                        if (obj >= seg.BackgroundAllocated)
                            noBgcMark = true;
                    }
                }
            }
            else
            {
                noBgcMark = true;
            }

            return noBgcMark || BackgroundObjectMarked(heap, obj);
        }

        private const uint MarkBitPitch = 8;
        private const uint MarkWordWidth = 32;
        private const uint MarkWordSize = MarkBitPitch * MarkWordWidth;

#pragma warning disable IDE0051 // Remove unused private members. This is information we'd like to keep.
        private const uint DtGcPageSize = 0x1000;
        private const uint CardWordWidth = 32;
        private uint CardSize => ((uint)_memoryReader.PointerSize / 4) * DtGcPageSize / CardWordWidth;
#pragma warning restore IDE0051 // Remove unused private members

        private static void ShouldCheckBgcMark(ClrSegment seg, out bool considerBgcMark, out bool checkCurrentSweep, out bool checkSavedSweep)
        {
            considerBgcMark = false;
            checkCurrentSweep = false;
            checkSavedSweep = false;

            ClrSubHeap heap = seg.SubHeap;
            if (heap.State == ClrSubHeap.GCState.Planning)
            {
                // We are doing the next_sweep_obj comparison here because we have yet to
                // turn on the swept flag for the segment but in_range_for_segment will return
                // FALSE if the address is the same as reserved.
                if ((seg.Flags & ClrSegmentFlags.Swept) == ClrSegmentFlags.Swept || !seg.ObjectRange.Contains(heap.NextSweepObject))
                {
                    // this seg was already swept.
                }
                else
                {
                    considerBgcMark = true;
                    if (seg.Address == heap.SavedSweepEphemeralSegment)
                        checkSavedSweep = true;

                    if (seg.ObjectRange.Contains(heap.NextSweepObject))
                        checkCurrentSweep = true;
                }
            }
        }

        private bool BackgroundObjectMarked(ClrSubHeap heap, ClrObject obj)
        {
            if (obj >= heap.BackgroundSavedLowestAddress && obj < heap.BackgroundSavedHighestAddress)
                return MarkArrayMarked(heap, obj);

            return true;
        }

        private bool MarkArrayMarked(ClrSubHeap heap, ClrObject obj)
        {
            ulong address = heap.MarkArray + sizeof(uint) * MarkWordOf(obj);
            if (!_memoryReader.Read(address, out uint entry))
                throw new IOException($"Could not read mark array at {address:x}");

            return (entry & (1u << MarkBitOf(obj))) != 0;
        }

        private static int MarkBitOf(ulong address) => (int)((address / MarkBitPitch) % MarkWordWidth);
        private static ulong MarkWordOf(ulong address) => address / MarkWordSize;


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
                if (!_sos.GetOOMData(out oomData) || (oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None))
                    return null;
            }
            else
            {
                if (!_sos.GetOOMData(subHeap.Address, out oomData) || (oomData.Reason == OutOfMemoryReason.None && oomData.GetMemoryFailure == GetMemoryFailureReason.None))
                    return null;
            }

            return new ClrOutOfMemoryInfo(oomData);
        }
    }
}