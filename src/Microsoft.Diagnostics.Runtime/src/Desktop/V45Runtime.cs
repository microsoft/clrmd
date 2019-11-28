// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    public interface ITypeFactory
    {
        ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj);
        ClrType GetOrCreateBasicType(ClrHeap heap, ClrElementType basicType);
        ClrModule GetOrCreateModule(ClrRuntime runtime, ulong address);
        void CreateFieldsForMethodTable(ClrType type, out ClrInstanceField[] fields, out ClrStaticField[] staticFields);
        CcwData CreateCCWForObject(ulong obj);
        RcwData CreateRCWForObject(ulong obj);
    }

    public interface ITypeData
    {
        bool IsShared { get; }
        bool ContainsPointers { get; }
        uint Token { get; }
        ulong MethodTable { get; }
        ulong ParentMethodTable { get; }
        int BaseSize { get; }
        int ComponentSize { get; }
        int MethodCount { get; }

        ITypeHelpers Helpers { get; }
    }

    public interface IObjectData
    {
        ulong DataPointer { get; }
        ulong ElementTypeHandle { get; }
        ClrElementType ElementType { get; }
        ulong RCW { get; }
        ulong CCW { get; }
    }

    public interface ITypeHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }

        string GetTypeName(ulong mt);
        ulong GetLoaderAllocatorHandle(ulong mt);
        IEnumerable<IMethodData> EnumerateMethods(ulong mt);

        // TODO: Should not expose this:
        IObjectData GetObjectData(ulong objRef);
    }

    public interface IMethodHelpers
    {
        IDataReader DataReader { get; }

        string GetSignature(ulong methodDesc);
        IReadOnlyList<ILToNativeMap> GetILMap(ulong nativeCode, in HotColdRegions hotColdInfo);
        ulong GetILForModule(ulong address, uint rva);
    }

    public interface IMethodData
    {
        IMethodHelpers Helpers { get; }

        uint Token { get; }
        MethodCompilationType CompilationType { get; }
        ulong GCInfo { get; }
        ulong HotStart { get; }
        uint HotSize { get; }
        ulong ColdStart { get; }
        uint ColdSize { get; }
    }

    public interface IHeapBuilder
    {
        IDataReader DataReader { get; }
        ITypeFactory TypeFactory { get; }

        bool ServerGC { get; }
        int LogicalHeapCount { get; }
        IEnumerable<Tuple<ulong, ulong>> EnumerateDependentHandleLinks();

        ulong ArrayMethodTable { get; }
        ulong StringMethodTable { get; }
        ulong ObjectMethodTable { get; }
        ulong ExceptionMethodTable { get; }
        ulong FreeMethodTable { get; }

        bool CanWalkHeap { get; }

        IReadOnlyList<ClrSegment> CreateSegments(ClrHeap clrHeap, out IReadOnlyList<AllocationContext> allocationContexts,
                                                 out IReadOnlyList<FinalizerQueueSegment> fqRoots, out IReadOnlyList<FinalizerQueueSegment> fqObjects);
    }

    public interface IRuntimeBuilder
    {
        IDataReader DataReader { get; }
        IHeapBuilder HeapBuilder { get; }
    }

    unsafe sealed class RuntimeBuilder : IRuntimeBuilder, ITypeFactory, ITypeHelpers, ITypeData, IMethodHelpers, IMethodData
    {
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6 _sos6;
        private readonly int _threads;
        private readonly ulong _firstThread;
        private List<AllocationContext> _threadAllocContexts;

        private ulong _lastMethodTable;
        private MethodTableData _mtData;
        private MethodDescData _mdData;
        private CodeHeaderData _codeHeaderData;

        // TODO: Move to another class:
        private readonly Dictionary<ulong, ClrType> _types = new Dictionary<ulong, ClrType>(1024);

        public IDataReader DataReader { get; }
        public IHeapBuilder HeapBuilder
        {
            get
            {
                // HeapBuilder will consume these allocation and fill the List, don't reuse it here
                var ctx = _threadAllocContexts;
                _threadAllocContexts = null;
                return new HeapBuilder(this, _sos, DataReader, ctx, _firstThread);
            }
        }
        public RuntimeBuilder(DacLibrary library, IDataReader reader)
        {
            _dac = library.DacPrivateInterface;
            _sos = library.SOSDacInterface;
            _sos6 = library.GetSOSInterface6NoAddRef();
            DataReader = reader;

            int version = 0;
            if (library.DacPrivateInterface.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))) != 0)
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request ThreadStoreData.");

            _threads = data.ThreadCount;
            _firstThread = data.FirstThread;
        }

        public ClrModule GetOrCreateModule(ClrRuntime runtime, ulong addr) => throw new NotImplementedException();
        public ClrType GetOrCreateBasicType(ClrHeap heap, ClrElementType basicType) => throw new NotImplementedException();

        public ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj)
        {
            if (_types.TryGetValue(mt, out ClrType found))
                return found;

            _lastMethodTable = mt;
            if (!_sos.GetMethodTableData(mt, out _mtData))
                return null;

            ClrModule module = GetOrCreateModule(heap?.Runtime, _mtData.Module);
            ClrmdType result = new ClrmdType(heap, module, this);
            _types.Add(mt, result);

            if (obj != 0 && result.ComponentType == null && result.IsArray)
                result.ComponentType = TryGetComponentType(heap, obj);

            return result;
        }


        private ClrType TryGetComponentType(ClrHeap heap, ulong obj)
        {
            ClrType result = null;
            if (_sos.GetObjectData(obj, out V45ObjectData data))
            {
                if (data.ElementTypeHandle != 0)
                    result = GetOrCreateType(heap, data.ElementTypeHandle, 0);

                if (result == null && data.ElementType != 0)
                    result = GetOrCreateBasicType(heap, (ClrElementType)data.ElementType);
            }

            return result;
        }

        CcwData ITypeFactory.CreateCCWForObject(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.CCW == 0)
                return null;

            if (!_sos.GetCCWData(data.CCW, out CCWData ccw))
                return null;

            return new DesktopCCWData(data.CCW, ccw);
        }

        RcwData ITypeFactory.CreateRCWForObject(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.RCW == 0)
                return null;

            if (!_sos.GetRCWData(data.RCW, out RCWData rcw))
                return null;

            return new DesktopRCWData(data.CCW, rcw);
        }

        void ITypeFactory.CreateFieldsForMethodTable(ClrType type, out ClrInstanceField[] fields, out ClrStaticField[] statics)
        {
            if (type.IsFree)
            {
                fields = Array.Empty<ClrInstanceField>();
                statics = Array.Empty<ClrStaticField>();
                return;
            }

            if (!_sos.GetFieldInfo(type.MethodTable, out V4FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
            {
                // Fill fields so we don't repeatedly try to init these fields on error.
                fields = Array.Empty<ClrInstanceField>();
                statics = Array.Empty<ClrStaticField>();
                return;
            }

            fields = new ClrInstanceField[fieldInfo.NumInstanceFields + type.BaseType.Fields.Count];
            statics = new ClrStaticField[fieldInfo.NumStaticFields];
            if (fieldInfo.NumStaticFields == 0)
                statics = Array.Empty<ClrStaticField>();
            int fieldNum = 0;
            int staticNum = 0;

            // Add base type's fields.
            if (type.BaseType != null)
            {
                foreach (ClrInstanceField field in type.BaseType.Fields)
                    fields[fieldNum++] = field;
            }

            ulong nextField = fieldInfo.FirstFieldAddress;
            int i = 0;

            MetaDataImport import = null;
            if (nextField != 0 && type.Module != null)
                import = type.Module.MetadataImport;

            while (fieldNum + staticNum < fields.Length + statics.Length && nextField != 0)
            {
                if (!_sos.GetFieldData(nextField, out FieldData field))
                    break;

                // We don't handle context statics.
                if (field.IsContextLocal != 0)
                {
                    nextField = field.NextField;
                    continue;
                }

                // Get the name of the field.
                string name = null;
                FieldAttributes attr = FieldAttributes.PrivateScope;
                int sigLen = 0;
                IntPtr ppValue = IntPtr.Zero;
                IntPtr fieldSig = IntPtr.Zero;

                if (import != null)
                    import.GetFieldProps((int)field.FieldToken, out name, out attr, out fieldSig, out sigLen, out int pdwCPlusTypeFlab, out ppValue);

                // If we couldn't figure out the name, at least give the token.
                if (import == null || name == null)
                {
                    name = $"<ERROR:{field.FieldToken:X}>";
                }

                //TODO: fix field init
                if (field.IsStatic != 0)
                {
                    statics[staticNum++] = new DesktopStaticField((ClrHeapImpl)type.Heap, ref field, type, name, attr, null, fieldSig, sigLen);
                }
                else if (field.IsThreadLocal == 0)
                {
                    fields[fieldNum++] = new DesktopInstanceField((ClrHeapImpl)type.Heap, ref field, name, attr, fieldSig, sigLen);
                }

                i++;
                nextField = field.NextField;
            }

            if (fieldNum != fields.Length)
                Array.Resize(ref fields, fieldNum);

            if (staticNum != statics.Length)
                Array.Resize(ref statics, staticNum);

            Array.Sort(fields, (a, b) => a.Offset.CompareTo(b.Offset));
        }


        string ITypeHelpers.GetTypeName(ulong mt) => _sos.GetMethodTableName(mt);

        ulong ITypeHelpers.GetLoaderAllocatorHandle(ulong mt)
        {
            if (_sos6 != null && _sos6.GetMethodTableCollectibleData(mt, out MethodTableCollectibleData data) && data.Collectible != 0)
                return data.LoaderAllocatorObjectHandle;

            return 0;
        }

        ITypeFactory ITypeHelpers.Factory => this;

        IObjectData ITypeHelpers.GetObjectData(ulong objRef)
        {
            // todo remove
            _sos.GetObjectData(objRef, out V45ObjectData data);
            return data;
        }

        IEnumerable<IMethodData> ITypeHelpers.EnumerateMethods(ulong mt)
        {
            _lastMethodTable = mt;
            if (_sos.GetMethodTableData(mt, out MethodTableData data))
            {
                for (int i = 0; i < data.NumMethods; i++)
                {
                    ulong slot = _sos.GetMethodTableSlot(mt, i);

                    if (_sos.GetCodeHeaderData(slot, out CodeHeaderData codeHeader))
                    {
                        ulong md = codeHeader.MethodDesc;
                        if (_sos.GetMethodDescData(md, 0, out _mdData) && _sos.GetCodeHeaderData(_mdData.NativeCodeAddr, out _codeHeaderData))
                            yield return this;
                    }
                }
            }
        }

        ITypeHelpers ITypeData.Helpers => this;

        bool ITypeData.IsShared => _mtData.Shared != 0;

        uint ITypeData.Token => _mtData.Token;

        ulong ITypeData.MethodTable => _lastMethodTable;

        ulong ITypeData.ParentMethodTable => _mtData.ParentMethodTable;

        int ITypeData.BaseSize => (int)_mtData.BaseSize;

        int ITypeData.ComponentSize => (int)_mtData.ComponentSize;

        int ITypeData.MethodCount => _mtData.NumMethods;
        bool ITypeData.ContainsPointers => _mtData.ContainsPointers != 0;

        IMethodHelpers IMethodData.Helpers => this;

        uint IMethodData.Token => _mdData.MDToken;

        MethodCompilationType IMethodData.CompilationType => (MethodCompilationType)_codeHeaderData.JITType;

        ulong IMethodData.GCInfo => _codeHeaderData.GCInfo;

        ulong IMethodData.HotStart => _mdData.NativeCodeAddr;

        uint IMethodData.HotSize => _codeHeaderData.HotRegionSize;

        ulong IMethodData.ColdStart => _codeHeaderData.ColdRegionStart;

        uint IMethodData.ColdSize => _codeHeaderData.ColdRegionSize;

        string IMethodHelpers.GetSignature(ulong methodDesc) => _sos.GetMethodDescName(methodDesc);
        ulong IMethodHelpers.GetILForModule(ulong address, uint rva) => _sos.GetILForModule(address, rva);

        IReadOnlyList<ILToNativeMap> IMethodHelpers.GetILMap(ulong ip, in HotColdRegions hotColdInfo)
        {
            List<ILToNativeMap> list = new List<ILToNativeMap>();

            foreach (ClrDataMethod method in _dac.EnumerateMethodInstancesByAddress(ip))
            {
                ILToNativeMap[] map = method.GetILToNativeMap();
                if (map != null)
                {
                    for (int i = 0; i < map.Length; i++)
                    {
                        if (map[i].StartAddress > map[i].EndAddress)
                        {
                            if (i + 1 == map.Length)
                                map[i].EndAddress = FindEnd(hotColdInfo, map[i].StartAddress);
                            else
                                map[i].EndAddress = map[i + 1].StartAddress - 1;
                        }
                    }

                    list.AddRange(map);
                }

                method.Dispose();
            }

            return list.ToArray();
        }

        private static ulong FindEnd(HotColdRegions reg, ulong address)
        {
            ulong hotEnd = reg.HotStart + reg.HotSize;
            if (reg.HotStart <= address && address < hotEnd)
                return hotEnd;

            ulong coldEnd = reg.ColdStart + reg.ColdSize;
            if (reg.ColdStart <= address && address < coldEnd)
                return coldEnd;

            // Shouldn't reach here, but give a sensible answer if we do.
            return address + 0x20;
        }
    }


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

        public bool ServerGC { get; }

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

        public IEnumerable<Tuple<ulong, ulong>> EnumerateDependentHandleLinks()
        {
            throw new NotImplementedException();
        }

        public HeapBuilder(ITypeFactory factory, SOSDac sos, IDataReader reader, List<AllocationContext> allocationContexts, ulong firstThread)
        {
            _sos = sos;
            DataReader = reader;
            TypeFactory = factory;
            _firstThread = firstThread;
            _threadAllocContexts = allocationContexts;

            if (_sos.GetCommonMethodTables(out _mts))
                CanWalkHeap = ArrayMethodTable != 0 && StringMethodTable != 0 && ExceptionMethodTable != 0 && FreeMethodTable != 0 && ObjectMethodTable != 0;

            if (_sos.GetGcHeapData(out GCInfo gcdata))
            {
                if (gcdata.MaxGeneration != 3)
                    throw new NotSupportedException($"The GC reported a max generation of {gcdata.MaxGeneration} which this build of ClrMD does not support.");

                ServerGC = gcdata.ServerMode != 0;
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
                    if (next == 0 || seen.Add(next))
                        break;
                }
            }

            if (ServerGC)
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
            if (_seenSegments.Add(address))
                return;

            while (_sos.GetSegmentData(address, out _segment))
            {
                segments.Add(new HeapSegment(clrHeap, this));
                if (_seenSegments.Add(_segment.Next))
                    break;
            }
        }
    }
    
    public struct FinalizerQueueSegment
    {
        public ulong Start { get; }
        public ulong End { get; }

        public FinalizerQueueSegment(ulong start, ulong end)
        {
            Start = start;
            End = end;

            Debug.Assert(Start < End);
            Debug.Assert(End != 0);
        }
    }
    public struct AllocationContext
    {
        public ulong Pointer { get; }
        public ulong Limit { get; }

        public AllocationContext(ulong pointer, ulong limit)
        {
            Pointer = pointer;
            Limit = limit;

            Debug.Assert(Pointer < Limit);
            Debug.Assert(Limit != 0);
        }
    }

    interface ISegmentBuilder
    {
        int LogicalHeap { get; }
        ulong Start { get; }
        ulong End { get; }
        ulong ReservedEnd { get; }
        ulong CommitedEnd { get; }
        ulong Gen0Start { get; }
        ulong Gen0Length { get; }
        ulong Gen1Start { get; }
        ulong Gen1Length { get; }
        ulong Gen2Start { get; }
        ulong Gen2Length { get; }


        bool IsLargeObjectSegment { get; }
        bool IsEphemeralSegment { get; }
    }

    internal class V45Runtime : ClrRuntimeImpl
    {
        private List<ClrHandle> _handles;
        private SOSDac _sos;
        private SOSDac6 _sos6;

        public unsafe V45Runtime(ClrInfo info, DataTarget dt, DacLibrary lib)
            : base(info, dt, lib)
        {
            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            if (_handles != null)
                return _handles;

            return EnumerateHandleWorker();
        }

        private IEnumerable<ClrHandle> EnumerateHandleWorker()
        {
            Debug.Assert(_handles == null);
            List<ClrHandle> result = new List<ClrHandle>();

            using (SOSHandleEnum handleEnum = _sos.EnumerateHandles())
            {
                HandleData[] handles = new HandleData[8];
                int fetched = 0;

                while ((fetched = handleEnum.ReadHandles(handles)) != 0)
                {
                    for (int i = 0; i < fetched; i++)
                    {
                        ClrHandle handle = new ClrHandle(this, Heap, handles[i]);
                        result.Add(handle);
                        yield return handle;

                        handle = handle.GetInteriorHandle();
                        if (handle != null)
                        {
                            result.Add(handle);
                            yield return handle;
                        }
                    }
                }
            }

            _handles = result;
        }

        internal override Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken)
        {
            Dictionary<ulong, List<ulong>> result = new Dictionary<ulong, List<ulong>>();

            using SOSHandleEnum handleEnum = _sos.EnumerateHandles();
            if (handleEnum == null)
                return result;

            HandleData[] handles = new HandleData[32];

            int fetched;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    HandleType type = (HandleType)handles[i].Type;
                    if (type != HandleType.Dependent)
                        continue;

                    if (ReadPointer(handles[i].Handle, out ulong address))
                    {
                        if (!result.TryGetValue(address, out List<ulong> value))
                            result[address] = value = new List<ulong>();

                        value.Add(handles[i].Secondary);
                    }
                }
            }

            return result;
        }

        internal override IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
        {
            if (includeDead)
                return base.EnumerateStackReferences(thread, includeDead);

            return EnumerateStackReferencesWorker(thread);
        }

        private IEnumerable<ClrRoot> EnumerateStackReferencesWorker(ClrThread thread)
        {
            using SOSStackRefEnum stackRefEnum = _sos.EnumerateStackRefs(thread.OSThreadId);
            if (stackRefEnum == null)
                yield break;

            ClrAppDomain domain = GetAppDomainByAddress(thread.AppDomain);
            ClrHeap heap = Heap;
            StackRefData[] refs = new StackRefData[1024];

            const int GCInteriorFlag = 1;
            const int GCPinnedFlag = 2;
            int fetched = 0;
            while ((fetched = stackRefEnum.ReadStackReferences(refs)) != 0)
            {
                for (uint i = 0; i < fetched && i < refs.Length; ++i)
                {
                    if (refs[i].Object == 0)
                        continue;

                    bool pinned = (refs[i].Flags & GCPinnedFlag) == GCPinnedFlag;
                    bool interior = (refs[i].Flags & GCInteriorFlag) == GCInteriorFlag;

                    ClrType type = null;

                    if (!interior)
                        type = heap.GetObjectType(refs[i].Object);

                    ClrStackFrame frame = thread.StackTrace.SingleOrDefault(
                        f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);

                    if (interior || type != null)
                        yield return new LocalVarRoot(refs[i].Address, refs[i].Object, type, domain, thread, pinned, false, interior, frame);
                }
            }
        }

        internal override ulong GetFirstThread()
        {
            IThreadStoreData threadStore = GetThreadStoreData();
            return threadStore != null ? threadStore.FirstThread : 0;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (_sos.GetThreadData(addr, out ThreadData data))
                return data;

            return null;
        }


        internal override ulong[] GetAppDomainList(int count)
        {
            return _sos.GetAppDomainList(count);
        }

        internal override ulong[] GetAssemblyList(ulong appDomain, int count)
        {
            return _sos.GetAssemblyList(appDomain, count);
        }

        internal override ulong[] GetModuleList(ulong assembly, int count)
        {
            return _sos.GetModuleList(assembly, count);
        }

        internal override IAssemblyData GetAssemblyData(ulong domain, ulong assembly)
        {
            if (_sos.GetAssemblyData(domain, assembly, out AssemblyData data))
                return data;

            return null;
        }

        internal override IAppDomainStoreData GetAppDomainStoreData()
        {
            if (_sos.GetAppDomainStoreData(out AppDomainStoreData data))
                return data;

            return null;
        }

        internal override IMethodTableData GetMethodTableData(ulong addr)
        {
            if (_sos.GetMethodTableData(addr, out MethodTableData data))
                return data;

            return null;
        }

        internal override ulong GetMethodTableByEEClass(ulong eeclass)
        {
            return _sos.GetMethodTableByEEClass(eeclass);
        }

        public override string GetMethodTableName(ulong mt)
        {
            return _sos.GetMethodTableName(mt);
        }

        internal override string GetPEFileName(ulong addr)
        {
            return _sos.GetPEFileName(addr);
        }

        internal override IModuleData GetModuleData(ulong addr)
        {
            if (_sos.GetModuleData(addr, out ModuleData data))
                return data;

            return null;
        }

        internal override ISegmentData GetSegmentData(ulong addr)
        {
            if (_sos.GetSegmentData(addr, out SegmentData data))
                return data;

            return null;
        }

        internal override IAppDomainData GetAppDomainData(ulong addr)
        {
            if (_sos.GetAppDomainData(addr, out AppDomainData data))
                return data;

            return null;
        }

        internal override string GetAppDomaminName(ulong addr)
        {
            return _sos.GetAppDomainName(addr);
        }

        internal override string GetAssemblyName(ulong addr)
        {
            return _sos.GetAssemblyName(addr);
        }

        internal override bool TraverseHeap(ulong heap, SOSDac.LoaderHeapTraverse callback)
        {
            return _sos.TraverseLoaderHeap(heap, callback);
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, SOSDac.LoaderHeapTraverse callback)
        {
            return _sos.TraverseStubHeap(appDomain, type, callback);
        }

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            JitManagerInfo[] jitManagers = _sos.GetJitManagers();
            for (int i = 0; i < jitManagers.Length; ++i)
            {
                if (jitManagers[i].Type != CodeHeapType.Unknown)
                    continue;

                JitCodeHeapInfo[] heapInfo = _sos.GetCodeHeapList(jitManagers[i].Address);

                for (int j = 0; j < heapInfo.Length; ++j)
                    yield return heapInfo[i];
            }
        }

        internal override MetaDataImport GetMetadataImport(ulong module)
        {
            return _sos.GetMetadataImport(module);
        }


        internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
        {
            List<MethodTableTokenPair> mts = new List<MethodTableTokenPair>();
            _sos.TraverseModuleMap(
                SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable,
                module,
                delegate (uint index, ulong mt, IntPtr token) { mts.Add(new MethodTableTokenPair(mt, index)); });

            return mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModuleById(ulong appDomain, ulong id)
        {
            if (_sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)id, out DomainLocalModuleData data))
                return data;

            return null;
        }

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            return _sos.GetCCWInterfaces(ccw, count);
        }

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
        {
            return _sos.GetRCWInterfaces(rcw, count);
        }

        internal override ICCWData GetCCWData(ulong ccw)
        {
            if (_sos.GetCCWData(ccw, out CCWData data))
                return data;

            return null;
        }

        internal override IRCWData GetRCWData(ulong rcw)
        {
            if (_sos.GetRCWData(rcw, out RCWData data))
                return data;

            return null;
        }

        internal override ulong GetILForModule(ClrModule module, uint rva)
        {
            return _sos.GetILForModule(module.Address, rva);
        }

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            ulong addr = offset;

            if (!_sos.GetThreadLocalModuleData(thread, moduleId, out ThreadLocalModuleData data))
                return 0;

            if (type.IsObjectReference() || type.IsValueClass())
                addr += data.GCStaticDataStart;
            else
                addr += data.NonGCStaticDataStart;

            return addr;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong module)
        {
            if (_sos.GetDomainLocalModuleDataFromModule(module, out DomainLocalModuleData data))
                return data;

            return null;
        }

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            if (!_sos.GetMethodTableData(methodTable, out MethodTableData mtData))
                return null;

            uint numMethods = mtData.NumMethods;
            ulong[] mds = new ulong[numMethods];

            for (int i = 0; i < numMethods; ++i)
                if (_sos.GetCodeHeaderData(_sos.GetMethodTableSlot(methodTable, i), out CodeHeaderData header))
                    mds[i] = header.MethodDesc;

            return mds;
        }

        internal override string GetNameForMD(ulong md)
        {
            return _sos.GetMethodDescName(md);
        }

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            V45MethodDescDataWrapper wrapper = new V45MethodDescDataWrapper();
            if (!wrapper.Init(_sos, md))
                return null;

            return wrapper;
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            if (!_sos.GetMethodTableData(mt, out MethodTableData data))
                return uint.MaxValue;

            return data.Token;
        }

        protected override DesktopStackFrame GetStackFrame(DesktopThread thread, byte[] context, ulong ip, ulong framePtr, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            if (frameVtbl != 0)
            {
                ClrMethod innerMethod = null;
                string frameName = _sos.GetFrameName(frameVtbl);

                ulong md = _sos.GetMethodDescPtrFromFrame(framePtr);
                if (md != 0)
                {
                    V45MethodDescDataWrapper mdData = new V45MethodDescDataWrapper();
                    if (mdData.Init(_sos, md))
                        innerMethod = ClrmdMethod.Create(this, mdData);
                }

                frame = new DesktopStackFrame(this, thread, context, framePtr, frameName, innerMethod);
            }
            else
            {
                frame = new DesktopStackFrame(this, thread, context, ip, framePtr, _sos.GetMethodDescPtrFromIP(ip));
            }

            return frame;
        }

        private bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
        {
            stackTrace = 0;
            ClrInstanceField field = type.GetFieldByName("_stackTrace");
            if (field == null)
                return false;

            object tmp = field.GetValue(obj);
            if (tmp == null || !(tmp is ulong))
                return false;

            stackTrace = (ulong)tmp;
            return true;
        }

        internal override IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type)
        {
            // TODO: Review this and if it works on v4.5, merge the two implementations back into RuntimeBase.
            List<ClrStackFrame> result = new List<ClrStackFrame>();
            if (type == null)
                return result;

            if (!GetStackTraceFromField(type, obj, out ulong _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            ClrHeap heap = Heap;
            ClrType stackTraceType = heap.GetObjectType(_stackTrace);
            if (stackTraceType == null || !stackTraceType.IsArray)
                return result;

            int len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!ReadPointer(dataPtr, out ulong count))
                return result;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            DesktopThread thread = null;
            for (int i = 0; i < (int)count; ++i)
            {
                if (!ReadPointer(dataPtr, out ulong ip))
                    break;
                if (!ReadPointer(dataPtr + (ulong)IntPtr.Size, out ulong sp))
                    break;
                if (!ReadPointer(dataPtr + (ulong)(2 * IntPtr.Size), out ulong md))
                    break;

                if (i == 0 && sp != 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                // it seems that the first frame often has 0 for IP and SP.  Try the 2nd frame as well
                if (i == 1 && thread == null && sp != 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                result.Add(new DesktopStackFrame(this, thread, null, ip, sp, md));

                dataPtr += (ulong)elementSize;
            }

            return result;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                return null;

            return data;
        }

        internal override string GetAppBase(ulong appDomain)
        {
            return _sos.GetAppBase(appDomain);
        }

        internal override string GetConfigFile(ulong appDomain)
        {
            return _sos.GetConfigFile(appDomain);
        }

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (!_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    return null;

                if ((md = codeHeaderData.MethodDesc) == 0)
                    return null;
            }

            V45MethodDescDataWrapper mdWrapper = new V45MethodDescDataWrapper();
            if (!mdWrapper.Init(_sos, md))
                return null;

            return mdWrapper;
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            return _sos.GetThreadFromThinlockId(threadId);
        }

        internal override int GetSyncblkCount()
        {
            if (_sos.GetSyncBlockData(1, out SyncBlockData data))
                return (int)data.TotalSyncBlockCount;

            return 0;
        }

        internal override ISyncBlkData GetSyncblkData(int index)
        {
            if (_sos.GetSyncBlockData(index + 1, out SyncBlockData data))
                return data;

            return null;
        }

        internal override IThreadPoolData GetThreadPoolData()
        {
            if (_sos.GetThreadPoolData(out ThreadPoolData data))
                return data;

            return null;
        }

        internal override uint GetTlsSlot()
        {
            return _sos.GetTlsIndex();
        }

        internal override uint GetThreadTypeIndex()
        {
            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (IntPtr.Size == 8)
                return 0x30;

            return 0x18;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            IThreadPoolData data = GetThreadPoolData();
            ulong request = data.FirstWorkRequest;
            while (request != 0)
            {
                if (!_sos.GetWorkRequestData(request, out WorkRequestData requestData))
                    break;

                yield return new DesktopNativeWorkItem(requestData);

                request = requestData.NextWorkRequest;
            }
        }

        internal override uint GetStringFirstCharOffset()
        {
            if (IntPtr.Size == 8)
                return 0xc;

            return 8;
        }

        internal override uint GetStringLengthOffset()
        {
            if (IntPtr.Size == 8)
                return 0x8;

            return 0x4;
        }

        internal override uint GetExceptionHROffset()
        {
            return IntPtr.Size == 8 ? 0x8cu : 0x40u;
        }

        public override string GetJitHelperFunctionName(ulong addr)
        {
            return _sos.GetJitHelperFunctionName(addr);
        }
    }
}