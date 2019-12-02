// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal unsafe sealed class HeapBuilder : IHeapBuilder, ISegmentBuilder
    {
        private readonly SOSDac _sos;
        private readonly CommonMethodTables _mts;
        private readonly ulong _firstThread;
        private readonly HashSet<ulong> _seenSegments = new HashSet<ulong>() { 0 };
        private HeapDetails _heap;
        private SegmentData _segment;
        private List<AllocationContext> _threadAllocContexts;

        #region IHeapBuilder
        public ITypeFactory TypeFactory { get; }
        public IDataReader DataReader { get; }

        public bool IsServer { get; }

        public int LogicalHeapCount { get; }
        
        public ulong ArrayMethodTable => _mts.ArrayMethodTable;

        public ulong StringMethodTable => _mts.StringMethodTable;

        public ulong ObjectMethodTable => _mts.ObjectMethodTable;

        public ulong ExceptionMethodTable => _mts.ExceptionMethodTable;

        public ulong FreeMethodTable => _mts.FreeMethodTable;

        public bool CanWalkHeap { get; }
        #endregion

        #region ISegmentBuilder
        public int LogicalHeap { get; private set; }

        public ulong Start => _segment.Start;

        public ulong End => IsEphemeralSegment ? _heap.Allocated : _segment.Allocated;

        public ulong ReservedEnd => _segment.Reserved;

        public ulong CommitedEnd => _segment.Committed;

        public ulong Gen0Start => IsEphemeralSegment ? _heap.GenerationTable[0].AllocationStart : End;

        public ulong Gen0Length => End - Gen0Start;

        public ulong Gen1Start => IsEphemeralSegment ? _heap.GenerationTable[1].AllocationStart : End;

        public ulong Gen1Length => Gen0Start - Gen1Start;

        public ulong Gen2Start => Start;

        public ulong Gen2Length => Gen1Start - Start;

        public bool IsLargeObjectSegment { get; private set; }

        public bool IsEphemeralSegment => _heap.EphemeralHeapSegment == _segment.Address;
        #endregion

        public HeapBuilder(ITypeFactory factory, SOSDac sos, IDataReader reader, List<AllocationContext> allocationContexts, ulong firstThread)
        {
            TypeFactory = factory;
            _sos = sos;
            DataReader = reader;
            _firstThread = firstThread;
            _threadAllocContexts = allocationContexts;

            if (_sos.GetCommonMethodTables(out _mts))
                CanWalkHeap = ArrayMethodTable != 0 && StringMethodTable != 0 && ExceptionMethodTable != 0 && FreeMethodTable != 0 && ObjectMethodTable != 0;

            if (_sos.GetGcHeapData(out GCInfo gcdata))
            {
                if (gcdata.MaxGeneration != 2)
                    throw new NotSupportedException($"The GC reported a max generation of {gcdata.MaxGeneration} which this build of ClrMD does not support.");

                IsServer = gcdata.ServerMode != 0;
                LogicalHeapCount = gcdata.HeapCount;
                CanWalkHeap &= gcdata.GCStructuresValid != 0;
            }
            else
            {
                CanWalkHeap = false;
            }
        }

        public IReadOnlyList<ClrSegment> CreateSegments(ClrHeap clrHeap, out IReadOnlyList<AllocationContext> allocationContexts,
                        out IReadOnlyList<FinalizerQueueSegment> fqRoots, out IReadOnlyList<FinalizerQueueSegment> fqObjects)
        {
            List<ClrSegment> result = new List<ClrSegment>();
            List<AllocationContext> allocContexts = _threadAllocContexts ?? new List<AllocationContext>();
            List<FinalizerQueueSegment> finalizerRoots = new List<FinalizerQueueSegment>();
            List<FinalizerQueueSegment> finalizerObjects = new List<FinalizerQueueSegment>();

            // This function won't be called twice, but just in case make sure we don't reuse this list
            _threadAllocContexts = null;

            if (allocContexts.Count == 0)
            {
                ulong next = _firstThread;
                HashSet<ulong> seen = new HashSet<ulong>() { next };  // Ensure we don't hit an infinite loop
                while (_sos.GetThreadData(next, out ThreadData thread))
                {
                    if (thread.AllocationContextPointer != 0 && thread.AllocationContextPointer != thread.AllocationContextLimit)
                        allocContexts.Add(new AllocationContext(thread.AllocationContextPointer, thread.AllocationContextLimit));

                    next = thread.NextThread;
                    if (next == 0 || !seen.Add(next))
                        break;
                }
            }

            if (IsServer)
            {
                ulong[] heapList = _sos.GetHeapList(LogicalHeapCount);
                foreach (ulong addr in heapList)
                    AddHeap(clrHeap, addr, allocContexts, result, finalizerRoots, finalizerObjects);
            }
            else
            {
                AddHeap(clrHeap, allocContexts, result, finalizerRoots, finalizerObjects);
            }

            result.Sort((x, y) => x.Start.CompareTo(y.Start));

            allocationContexts = allocContexts;
            fqRoots = finalizerRoots;
            fqObjects = finalizerObjects;
            return result;
        }

        public void AddHeap(ClrHeap clrHeap, ulong address, List<AllocationContext> allocationContexts, List<ClrSegment> segments,
                            List<FinalizerQueueSegment> fqRoots, List<FinalizerQueueSegment> fqObjects)
        {
            _seenSegments.Clear();
            LogicalHeap = 0;
            if (_sos.GetServerHeapDetails(address, out _heap))
            {
                LogicalHeap++;
                ProcessHeap(clrHeap, allocationContexts, segments, fqRoots, fqObjects);
            }
        }

        public void AddHeap(ClrHeap clrHeap, List<AllocationContext> allocationContexts, List<ClrSegment> segments,
                            List<FinalizerQueueSegment> fqRoots, List<FinalizerQueueSegment> fqObjects)
        {
            _seenSegments.Clear();
            LogicalHeap = 0;
            if (_sos.GetWksHeapDetails(out _heap))
                ProcessHeap(clrHeap, allocationContexts, segments, fqRoots, fqObjects);
        }

        private void ProcessHeap(ClrHeap clrHeap, List<AllocationContext> allocationContexts, List<ClrSegment> segments,
                                    List<FinalizerQueueSegment> fqRoots, List<FinalizerQueueSegment> fqObjects)
        {
            if (_heap.EphemeralAllocContextPtr != 0 && _heap.EphemeralAllocContextPtr != _heap.EphemeralAllocContextLimit)
                allocationContexts.Add(new AllocationContext(_heap.EphemeralAllocContextPtr, _heap.EphemeralAllocContextLimit));

            fqRoots.Add(new FinalizerQueueSegment(_heap.FQRootsStart, _heap.FQRootsStop));
            fqObjects.Add(new FinalizerQueueSegment(_heap.FQAllObjectsStart, _heap.FQAllObjectsStop));

            IsLargeObjectSegment = true;
            AddSegments(clrHeap, segments, _heap.GenerationTable[3].StartSegment);
            IsLargeObjectSegment = false;
            AddSegments(clrHeap, segments, _heap.EphemeralHeapSegment);
        }

        private void AddSegments(ClrHeap clrHeap, List<ClrSegment> segments, ulong address)
        {
            while (_seenSegments.Add(address) && _sos.GetSegmentData(address, out _segment))
            {
                segments.Add(new ClrmdSegment(clrHeap, this));
                address = _segment.Next;
            }
        }

        public IEnumerable<(ulong, ulong)> EnumerateDependentHandleLinks()
        {
            // TODO use smarter sos enum for only dependent handles
            using SOSHandleEnum handleEnum = _sos.EnumerateHandles();

            HandleData[] handles = new HandleData[32];
            int fetched = 0;
            while ((fetched = handleEnum.ReadHandles(handles, 16)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    if (handles[i].Type == (int)ClrHandleKind.Dependent)
                    {
                        ulong obj = DataReader.ReadPointerUnsafe(handles[i].Handle);
                        if (obj != 0)
                            yield return (obj, handles[i].Secondary);
                    }
                }
            }
        }
    }
}