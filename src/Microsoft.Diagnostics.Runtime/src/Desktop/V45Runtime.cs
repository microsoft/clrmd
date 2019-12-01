// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    // TODO: reconsider whether we should carry ClrHeap here some other solution.
    public interface ITypeFactory : IDisposable
    {
        ClrRuntime GetOrCreateRuntime();
        ClrHeap GetOrCreateHeap(ClrRuntime runtime);
        ClrAppDomain GetOrCreateAppDomain(ClrRuntime runtime, ulong domain);
        ClrModule GetOrCreateModule(ClrAppDomain domain, ulong address);
        ClrMethod[] CreateMethodsForType(ClrType type);
        void CreateFieldsForType(ClrType type, out ClrInstanceField[] fields, out ClrStaticField[] staticFields);
        ComCallWrapper CreateCCWForObject(ClrRuntime runtime, ulong obj);
        RuntimeCallableWrapper CreateRCWForObject(ClrRuntime runtime, ulong obj);
        ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj);
        ClrType GetOrCreateBasicType(ClrHeap heap, ClrElementType basicType);
        ClrType GetOrCreateArrayType(ClrHeap heap, ClrType inner, int ranks);
        ClrType GetOrCreateTypeFromToken(ClrHeap heap, ClrModule module, int token);
        ClrType GetOrCreatePointerType(ClrHeap heap, ClrType innerType, int depth);
        ClrMethod CreateMethodFromHandle(ClrHeap heap, ulong methodHandle);
    }

    public interface IFieldHelpers
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }
        bool ReadProperties(ClrType parentType, out string name, out FieldAttributes attributes, out Utilities.SigParser sigParser);
        ulong GetStaticFieldAddress(ClrStaticField field, ClrAppDomain appDomain);
    }

    public interface IFieldData
    {
        IFieldHelpers Helpers { get; }

        ClrElementType ElementType { get; }
        uint Token { get; }
        int Offset { get; }
        ulong TypeMethodTable { get; }
    }

    public interface IThreadHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }
        IExceptionHelpers ExceptionHelpers { get; }

        IEnumerable<ClrRoot> EnumerateStackRoots(ClrThread thread);
        IEnumerable<ClrStackFrame> EnumerateStackTrace(ClrThread thread);
    }

    public interface IThreadData
    {
        IThreadHelpers Helpers { get; }
        ulong Address { get; }
        bool IsFinalizer { get; }
        uint OSThreadID { get; }
        int ManagedThreadID { get; }
        uint LockCount { get; }
        int State { get; }
        ulong ExceptionHandle { get; }
        bool Preemptive { get; }
        ulong StackBase { get; }
        ulong StackLimit { get; }
    }

    public interface IRuntimeHelpers
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }
        IReadOnlyList<ClrThread> GetThreads(ClrRuntime runtime);
        IReadOnlyList<ClrAppDomain> GetAppDomains(ClrRuntime runtime, out ClrAppDomain system, out ClrAppDomain shared);
        IEnumerable<ClrHandle> EnumerateHandleTable(ClrRuntime runtime);
        void ClearCachedData();
        ulong GetMethodDesc(ulong ip);
        string GetJitHelperFunctionName(ulong ip);
    }

    public interface IExceptionHelpers
    {
        IReadOnlyList<ClrStackFrame> GetExceptionStackTrace(ClrObject obj);
    }

    public interface IAppDomainHelpers
    {
        string GetConfigFile(ClrAppDomain domain);
        string GetApplicationBase(ClrAppDomain domain);
        IEnumerable<ClrModule> EnumerateModules(ClrAppDomain domain);
    }

    public interface IAppDomainData
    {
        IAppDomainHelpers Helpers { get; }
        string Name { get; }
        int Id { get; }
        ulong Address { get; }
    }

    public interface IModuleHelpers
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }

        MetaDataImport GetMetaDataImport(ClrModule module);
        IReadOnlyList<(ulong, uint)> GetSortedTypeDefMap(ClrModule module);
        IReadOnlyList<(ulong, uint)> GetSortedTypeRefMap(ClrModule module);
        ClrType TryGetType(ulong mt);
        string GetTypeName(ulong mt);
    }

    public interface IModuleData
    {
        IModuleHelpers Helpers { get; }

        ulong Address { get; }
        bool IsPEFile { get; }
        ulong PEImageBase { get; }
        ulong ILImageBase { get; }
        ulong Size { get; }
        ulong MetadataStart { get; }
        string Name { get; }
        string AssemblyName { get; }
        ulong MetadataLength { get; }
        bool IsReflection { get; }
        ulong AssemblyAddress { get; }
    }

    public interface IClrObjectHelpers
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }
        IExceptionHelpers ExceptionHelpers { get; }
    }

    public interface ITypeData
    {
        bool IsShared { get; }
        bool ContainsPointers { get; }
        uint Token { get; }
        ulong MethodTable { get; }
        // Currently no runtime emits this, but opportunistically I'd like to see it work.
        ulong ComponentMethodTable { get; }
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

        // TODO: Should not expose this:
        IObjectData GetObjectData(ulong objRef);
    }

    public interface IRCWData
    {
        ulong Address { get; }
        ulong IUnknown { get; }
        ulong VTablePointer { get; }
        int RefCount { get; }
        ulong ManagedObject { get; }
        bool Disconnected { get; }
        ulong CreatorThread { get; }

        IReadOnlyList<ComInterfaceData> GetInterfaces(ClrRuntime runtime);
    }

    public interface ICCWData
    {
        ulong Address { get; }
        ulong IUnknown { get; }
        ulong Object { get; }
        ulong Handle { get; }
        int RefCount { get; }
        int JupiterRefCount { get; }

        IReadOnlyList<ComInterfaceData> GetInterfaces(ClrRuntime runtime);
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

        public bool IsServer { get; }
        int LogicalHeapCount { get; }
        IEnumerable<(ulong, ulong)> EnumerateDependentHandleLinks();

        ulong ArrayMethodTable { get; }
        ulong StringMethodTable { get; }
        ulong ObjectMethodTable { get; }
        ulong ExceptionMethodTable { get; }
        ulong FreeMethodTable { get; }

        bool CanWalkHeap { get; }

        IReadOnlyList<ClrSegment> CreateSegments(ClrHeap clrHeap, out IReadOnlyList<AllocationContext> allocationContexts,
                                                 out IReadOnlyList<FinalizerQueueSegment> fqRoots, out IReadOnlyList<FinalizerQueueSegment> fqObjects);
    }

    interface ITypeCache
    {
        public long TotalBytes { get; }
        public long MaxSize { get; }

        ClrType GetStoredType(ulong key);
        bool Store(ulong key, ClrType type);

        string ReportOrInternString(ulong key, string str);
        void ReportMemory(ulong key, long bytes);
        void Clear();
    }

    sealed class TypeCache : ITypeCache
    {
        private readonly Dictionary<ulong, ClrType> _types = new Dictionary<ulong, ClrType>(1024);
        public long TotalBytes { get; private set; }
        public long MaxSize { get; } = IntPtr.Size == 4 ? 500 * 1024 * 1024 : long.MaxValue;

        public void Clear()
        {
            TotalBytes = 0;
            _types.Clear();
        }

        public bool Store(ulong key, ClrType type)
        {
            if (TotalBytes >= MaxSize)
                return false;

            _types[key] = type;
            return true;
        }

        public ClrType GetStoredType(ulong key) => _types.GetOrDefault(key);

        public void ReportMemory(ulong key, long bytes)
        {
            TotalBytes += bytes;
        }

        public string ReportOrInternString(ulong key, string str)
        {
            if (str != null)
                ReportMemory(key, 2 * IntPtr.Size + 2 * str.Length);
            return str;
        }
    }

    // This class will not be marked public.
    // This implementation takes a lot of shortcuts to avoid allocations, and as a result the interfaces
    // it implements have very odd constraints around how they can be used.  All Clrmd* types understand
    // these constraints and use it properly, but since this class doesn't behave as a developer would
    // expect, it must stay internal only.
    unsafe sealed class RuntimeBuilder : IRuntimeHelpers, ITypeFactory, ITypeHelpers, ITypeData, IModuleData, IModuleHelpers,
                                         IMethodHelpers, IMethodData, IClrObjectHelpers, IFieldData, IFieldHelpers, IAppDomainData,
                                         IAppDomainHelpers, IThreadData, IThreadHelpers, ICCWData, IRCWData, IExceptionHelpers

    {
        private readonly ClrInfo _clrinfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6 _sos6;
        private readonly int _threads;
        private readonly ulong _finalizer;
        private readonly ulong _firstThread;

        readonly Dictionary<ulong, ClrModule> _modules = new Dictionary<ulong, ClrModule>();
        private readonly Dictionary<ulong, ulong> _moduleSizes = new Dictionary<ulong, ulong>();
        private List<AllocationContext> _threadAllocContexts;

        private ClrRuntime _runtime;
        private ClrHeap _heap;

        private readonly ITypeCache _cache = new TypeCache();
        private readonly int _typeSize = 8; // todo
        private readonly int _methodSize = 8;
        private readonly int _instFieldSize = 8;
        private readonly int _staticFieldSize = 8;

        private ulong _ptr;
        private AppDomainData _appDomainData;
        private ModuleData _moduleData;
        private MethodTableData _mtData;
        private MethodDescData _mdData;
        private CodeHeaderData _codeHeaderData;
        private FieldData _fieldData;
        private ThreadData _threadData;
        private CCWData _ccwData;
        private RCWData _rcwData;

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

        public ITypeFactory Factory => this;

        public RuntimeBuilder(ClrInfo clr, DacLibrary library)
        {
            _clrinfo = clr;
            _library = library;
            _dac = _library.DacPrivateInterface;
            _sos = _library.SOSDacInterface;
            _sos6 = _library.GetSOSInterface6NoAddRef();
            DataReader = _clrinfo.DataTarget.DataReader;

            int version = 0;
            if (library.DacPrivateInterface.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))) != 0)
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request ThreadStoreData.");

            _threads = data.ThreadCount;
            _firstThread = data.FirstThread;
            _finalizer = data.FinalizerThread;

            foreach (ModuleInfo mi in _clrinfo.DataTarget.EnumerateModules())
                _moduleSizes[mi.ImageBase] = mi.FileSize;
        }

        public void Dispose()
        {
            _library.Dispose();
        }

        public ClrModule GetOrCreateModule(ClrAppDomain domain, ulong addr)
        {
            if (_modules.TryGetValue(addr, out ClrModule result))
                return result;

            _ptr = addr;
            if (!_sos.GetModuleData(addr, out _moduleData))
                return null;

            result = new ClrmdModule(domain, this);
            return result;
        }

        IThreadHelpers IThreadData.Helpers => this;
        ulong IThreadData.Address => _ptr;
        bool IThreadData.IsFinalizer => _finalizer == _ptr;
        uint IThreadData.OSThreadID => _threadData.OSThreadId;
        int IThreadData.ManagedThreadID => (int)_threadData.ManagedThreadId;
        uint IThreadData.LockCount => _threadData.LockCount;
        int IThreadData.State => _threadData.State;
        ulong IThreadData.ExceptionHandle => _threadData.LastThrownObjectHandle;
        bool IThreadData.Preemptive => _threadData.PreemptiveGCDisabled == 0;
        ulong IThreadData.StackBase
        {
            get
            {
                if (_threadData.Teb == 0)
                    return 0;

                ulong ptr = _threadData.Teb + (ulong)IntPtr.Size;
                if (!DataReader.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }
        ulong IThreadData.StackLimit
        {
            get
            {
                if (_threadData.Teb == 0)
                    return 0;

                ulong ptr = _threadData.Teb + (ulong)IntPtr.Size * 2;
                if (!DataReader.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        IEnumerable<ClrRoot> IThreadHelpers.EnumerateStackRoots(ClrThread thread)
        {
            // TODO: rethink stack roots and this code
            using SOSStackRefEnum stackRefEnum = _sos.EnumerateStackRefs(thread.OSThreadId);
            if (stackRefEnum == null)
                yield break;

            ClrStackFrame[] stack = thread.EnumerateStackTrace().Take(1024).ToArray();

            ClrAppDomain domain = thread.CurrentAppDomain;
            ClrHeap heap = thread.Runtime?.Heap;
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

                    ClrStackFrame frame = stack.SingleOrDefault(f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);

                    if (interior || type != null)
                        yield return new LocalVarRoot(refs[i].Address, refs[i].Object, type, domain, thread, pinned, false, interior, frame);
                }
            }
        }

        IEnumerable<ClrStackFrame> IThreadHelpers.EnumerateStackTrace(ClrThread thread)
        {
            using ClrStackWalk stackwalk = _dac.CreateStackWalk(thread.OSThreadId, 0xf);
            if (stackwalk == null)
                yield break;

            byte[] context = ContextHelper.Context;
            do
            {
                if (!stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out _, context))
                    break;

                ulong ip, sp;

                if (IntPtr.Size == 4)
                {
                    ip = BitConverter.ToUInt32(context, ContextHelper.InstructionPointerOffset);
                    sp = BitConverter.ToUInt32(context, ContextHelper.StackPointerOffset);
                }
                else
                {
                    ip = BitConverter.ToUInt64(context, ContextHelper.InstructionPointerOffset);
                    sp = BitConverter.ToUInt64(context, ContextHelper.StackPointerOffset);
                }

                ulong frameVtbl = stackwalk.GetFrameVtable();
                if (frameVtbl != 0)
                {
                    sp = frameVtbl;
                    frameVtbl = DataReader.ReadPointerUnsafe(sp);
                }

                byte[] contextCopy = new byte[context.Length];
                Buffer.BlockCopy(context, 0, contextCopy, 0, context.Length);

                ClrStackFrame frame = GetStackFrame(thread, contextCopy, ip, sp, frameVtbl);
                yield return frame;
            } while (stackwalk.Next());
        }


        private ClrStackFrame GetStackFrame(ClrThread thread, byte[] context, ulong ip, ulong sp, ulong frameVtbl)
        {
            // todo: pull Method from enclosing type, don't generate methods without a parent
            if (frameVtbl != 0)
            {
                ClrMethod innerMethod = null;
                string frameName = _sos.GetFrameName(frameVtbl);

                ulong md = _sos.GetMethodDescPtrFromFrame(sp);
                if (md != 0)
                {
                    V45MethodDescDataWrapper mdData = new V45MethodDescDataWrapper();
                    if (mdData.Init(_sos, md))
                        innerMethod = CreateMethodFromHandle(thread?.Runtime?.Heap, md);
                }

                return new ClrmdStackFrame(context, ip, sp, ClrStackFrameType.Runtime, innerMethod, frameName);
            }
            else
            {
                ClrMethod method = thread?.Runtime?.GetMethodByInstructionPointer(ip);
                return new ClrmdStackFrame(context, ip, sp, ClrStackFrameType.ManagedMethod, method, null);
            }
        }

        IReadOnlyList<ClrThread> IRuntimeHelpers.GetThreads(ClrRuntime runtime)
        {
            ClrThread[] threads = new ClrThread[_threads];

            // Ensure we don't hit a loop due to corrupt data
            HashSet<ulong> seen = new HashSet<ulong>() { 0 };
            ulong addr = _firstThread;
            int i;
            for (i = 0; i < threads.Length && !seen.Contains(addr); i++)
            {
                if (!_sos.GetThreadData(addr, out _threadData))
                    break;

                _ptr = addr;
                addr = _threadData.NextThread;

                ClrAppDomain domain = GetOrCreateAppDomain(runtime, _threadData.Domain);
                threads[i] = new ClrmdThread(this, domain);
            }

            // Shouldn't happen unless we caught the runtime at a really bad place
            if (i < threads.Length)
                Array.Resize(ref threads, i);

            return threads;
        }

        IReadOnlyList<ClrAppDomain> IRuntimeHelpers.GetAppDomains(ClrRuntime runtime, out ClrAppDomain system, out ClrAppDomain shared)
        {
            system = null;
            shared = null;

            if (!_sos.GetAppDomainStoreData(out AppDomainStoreData adstore))
                return Array.Empty<ClrAppDomain>();

            if (adstore.SystemDomain != 0)
                system = GetOrCreateAppDomain(runtime, adstore.SystemDomain);

            if (adstore.SharedDomain != 0)
                shared = GetOrCreateAppDomain(runtime, adstore.SharedDomain);

            ulong[] domainList = _sos.GetAppDomainList(adstore.AppDomainCount);
            ClrAppDomain[] result = new ClrAppDomain[domainList.Length];
            int i = 0;
            foreach (ulong domain in domainList)
            {
                ClrAppDomain ad = GetOrCreateAppDomain(runtime, domain);
                if (ad != null)
                    result[i++] = ad;
            }

            if (i < result.Length)
                Array.Resize(ref result, i);

            return result;
        }

        private readonly Dictionary<ulong, ClrAppDomain> _domains = new Dictionary<ulong, ClrAppDomain>();
        public ClrAppDomain GetOrCreateAppDomain(ClrRuntime runtime, ulong domain)
        {
            if (_domains.TryGetValue(domain, out ClrAppDomain result))
                return result;

            if (!_sos.GetAppDomainData(domain, out _appDomainData))
                return null;

            _ptr = domain;
            return _domains[domain] = new ClrmdAppDomain(runtime, this);
        }


        IEnumerable<ClrHandle> IRuntimeHelpers.EnumerateHandleTable(ClrRuntime runtime)
        {
            Span<HandleData> handles = stackalloc HandleData[16];
            return EnumerateHandleTable(runtime, handles, 16);
        }

        IEnumerable<ClrHandle> EnumerateHandleTable(ClrRuntime runtime, Span<HandleData> handles, int count)
        {
            // TODO: Use smarter handle enum overload in _sos
            // TODO: actually enumerate instead of building giant list

            List<ClrHandle> result = new List<ClrHandle>();

            Dictionary<ulong, ClrAppDomain> domains = new Dictionary<ulong, ClrAppDomain>();
            foreach (ClrAppDomain domain in runtime.AppDomains)
            {
                // Don't use .ToDictionary in case we have bad data;
                domains[domain.Address] = domain;
            }

            using SOSHandleEnum handleEnum = _sos.EnumerateHandles();

            int fetched = 0;
            while ((fetched = handleEnum.ReadHandles(handles, count)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    ulong obj = DataReader.ReadPointerUnsafe(handles[i].Handle);
                    ulong mt = 0;
                    if (obj != 0)
                        mt = DataReader.ReadPointerUnsafe(obj);

                    if (mt != 0)
                    {
                        ClrType type = GetOrCreateType(runtime.Heap, mt, obj);
                        ClrType dependent = null;
                        if (handles[i].Type == (int)HandleType.Dependent && handles[i].Secondary != 0)
                        {
                            ulong dmt = DataReader.ReadPointerUnsafe(handles[i].Secondary);

                            if (dmt != 0)
                                dependent = GetOrCreateType(runtime.Heap, dmt, handles[i].Secondary);
                        }

                        domains.TryGetValue(handles[i].AppDomain, out ClrAppDomain domain);

                        ClrHandle handle = new ClrHandle(in handles[i], obj, type, domain, dependent);
                        result.Add(handle);

                        handle = handle.GetInteriorHandle();
                        if (handle != null)
                            result.Add(handle);
                    }
                }
            }

            return result;
        }

        void IRuntimeHelpers.ClearCachedData()
        {
            _heap = null;
            _dac.Flush();
            _cache.Clear();
        }

        ulong IRuntimeHelpers.GetMethodDesc(ulong ip) => _sos.GetMethodDescPtrFromIP(ip);
        string IRuntimeHelpers.GetJitHelperFunctionName(ulong ip) => _sos.GetJitHelperFunctionName(ip);

        public IExceptionHelpers ExceptionHelpers => this;

        IReadOnlyList<ClrStackFrame> IExceptionHelpers.GetExceptionStackTrace(ClrObject obj)
        {
            ClrObject _stackTrace = obj.GetObjectField("_stackTrace");
            if (_stackTrace.IsNull)
                return Array.Empty<ClrStackFrame>();
            
            int len = _stackTrace.Length;
            if (len == 0)
                return Array.Empty<ClrStackFrame>();

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!DataReader.ReadPointer(dataPtr, out ulong count))
                return Array.Empty<ClrStackFrame>();

            ClrStackFrame[] result = new ClrStackFrame[count];

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            for (int i = 0; i < (int)count; ++i)
            {
                ulong ip = DataReader.ReadPointerUnsafe(dataPtr);
                ulong sp = DataReader.ReadPointerUnsafe(dataPtr + (ulong)IntPtr.Size);
                ulong md = DataReader.ReadPointerUnsafe(dataPtr + (ulong)IntPtr.Size + (ulong)IntPtr.Size);

                ClrMethod method = CreateMethodFromHandle(obj.Type?.Heap, md);
                result[i] = new ClrmdStackFrame(null, ip, sp, ClrStackFrameType.ManagedMethod, method, frameName: null);
                dataPtr += (ulong)elementSize;
            }

            return result;
        }


        IAppDomainHelpers IAppDomainData.Helpers => this;
        string IAppDomainData.Name => _cache.ReportOrInternString(_ptr, _sos.GetAppDomainName(_ptr));
        int IAppDomainData.Id => _appDomainData.Id;
        ulong IAppDomainData.Address => _appDomainData.Address;

        string IAppDomainHelpers.GetConfigFile(ClrAppDomain domain) => _sos.GetConfigFile(domain.Address);
        string IAppDomainHelpers.GetApplicationBase(ClrAppDomain domain) => _sos.GetAppBase(domain.Address);
        IEnumerable<ClrModule> IAppDomainHelpers.EnumerateModules(ClrAppDomain domain)
        {
            foreach (ulong assembly in _sos.GetAssemblyList(domain.Address))
                foreach (ulong module in _sos.GetModuleList(assembly))
                    yield return GetOrCreateModule(domain, module);
        }

        IModuleHelpers IModuleData.Helpers => this;
        ulong IModuleData.Address => _ptr;
        bool IModuleData.IsPEFile => _moduleData.IsPEFile != 0;
        ulong IModuleData.PEImageBase => _moduleData.PEFile;
        ulong IModuleData.ILImageBase => _moduleData.ILBase;
        ulong IModuleData.Size => _moduleSizes.GetOrDefault(_ptr);
        ulong IModuleData.MetadataStart => _moduleData.MetadataStart;
        ulong IModuleData.MetadataLength => _moduleData.MetadataSize;
        string IModuleData.Name
        {
            get
            {
                if (_moduleData.PEFile != 0)
                {
                    string name = _sos.GetPEFileName(_moduleData.PEFile);
                    if (name != null)
                        return _cache.ReportOrInternString(_ptr, name);
                }

                return null;
            }
        }

        string IModuleData.AssemblyName
        {
            get
            {
                if (_moduleData.Assembly != 0)
                {
                    string name = _sos.GetAssemblyName(_moduleData.Assembly);
                    if (name != null)
                        return _cache.ReportOrInternString(_ptr, name);
                }

                return null;
            }
        }


        bool IModuleData.IsReflection => _moduleData.IsReflection != 0;

        ulong IModuleData.AssemblyAddress => _moduleData.Assembly;


        ClrType IModuleHelpers.TryGetType(ulong mt) => _cache.GetStoredType(mt);
        IReadOnlyList<(ulong, uint)> IModuleHelpers.GetSortedTypeDefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable);
        IReadOnlyList<(ulong, uint)> IModuleHelpers.GetSortedTypeRefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeRefToMethodTable);

        private IReadOnlyList<(ulong, uint)> GetSortedMap(ClrModule module, SOSDac.ModuleMapTraverseKind kind)
        {
            List<(ulong, uint)> result = new List<(ulong, uint)>();
            uint lastToken = 0;
            bool sorted = true;
            _sos.TraverseModuleMap(kind, module.Address, (token, mt, _) =>
            {
                result.Add(ValueTuple.Create(mt, token));
                if (sorted && lastToken > token)
                    sorted = false;
            });

            if (!sorted)
                result.Sort((x, y) => x.Item2.CompareTo(y.Item2));

            return result;
        }

        public ClrRuntime GetOrCreateRuntime() => _runtime ?? (_runtime = new ClrmdRuntime(_clrinfo, _library, this));
        public ClrHeap GetOrCreateHeap(ClrRuntime runtime) => _heap ?? (_heap = new ClrmdHeap(GetOrCreateRuntime(), HeapBuilder));

        public ClrType GetOrCreateBasicType(ClrHeap heap, ClrElementType basicType) => throw new NotImplementedException();


        public ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj)
        {
            if (heap is null)
                throw new ArgumentNullException(nameof(heap));

            {
                ClrType result = _cache.GetStoredType(mt);
                if (result != null)
                {
                    if (obj != 0  && result.ComponentType == null && result.IsArray && result is ClrmdType type)
                        TryGetComponentType(type, heap, obj);

                    return result;
                }
            }

            {
                if (!_sos.GetMethodTableData(mt, out _mtData))
                    return null;

                ClrModule module = GetOrCreateModule(null, _mtData.Module);
                _ptr = mt;
                ClrmdType result = new ClrmdType(heap, module, this);

                if (_cache.Store(mt, result))
                    _cache.ReportMemory(mt, _typeSize);

                if (obj != 0 && result.IsArray)
                {
                    Debug.Assert(result.ComponentType == null);
                    TryGetComponentType(result, heap, obj);
                }

                return result;
            }
        }

        public ClrType GetOrCreateTypeFromToken(ClrHeap heap, ClrModule module, int token) => throw new NotImplementedException();
        public ClrType GetOrCreateArrayType(ClrHeap heap, ClrType innerType, int ranks) => innerType != null ? new ClrmdPointerArrayType(innerType, ranks, pointer: false) : null;
        public ClrType GetOrCreatePointerType(ClrHeap heap, ClrType innerType, int depth) => innerType != null ? new ClrmdPointerArrayType(innerType, depth, pointer: true) : null;


        private void TryGetComponentType(ClrmdType type, ClrHeap heap, ulong obj)
        {
            ClrType result = null;
            if (_sos.GetObjectData(obj, out V45ObjectData data))
            {
                if (data.ElementTypeHandle != 0)
                    result = GetOrCreateType(heap, data.ElementTypeHandle, 0);

                if (result == null && data.ElementType != 0)
                    result = GetOrCreateBasicType(heap, (ClrElementType)data.ElementType);

                type.SetComponentType(result);
            }
        }

        ComCallWrapper ITypeFactory.CreateCCWForObject(ClrRuntime runtime, ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.CCW == 0)
                return null;

            if (!_sos.GetCCWData(data.CCW, out _ccwData))
                return null;

            _ptr = data.CCW;
            return new ComCallWrapper(runtime,  this);
        }

        RuntimeCallableWrapper ITypeFactory.CreateRCWForObject(ClrRuntime runtime, ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.RCW == 0)
                return null;

            if (!_sos.GetRCWData(data.RCW, out _rcwData))
                return null;

            _ptr = data.RCW;
            return new RuntimeCallableWrapper(runtime, this);
        }

        ClrMethod[] ITypeFactory.CreateMethodsForType(ClrType type)
        {
            List<ClrMethod> methods = new List<ClrMethod>(32);

            ulong mt = type.MethodTable;
            _ptr = mt;
            if (_sos.GetMethodTableData(mt, out MethodTableData data))
            {
                for (int i = 0; i < data.NumMethods; i++)
                {
                    ulong slot = _sos.GetMethodTableSlot(mt, i);

                    if (_sos.GetCodeHeaderData(slot, out CodeHeaderData codeHeader))
                    {
                        ulong md = codeHeader.MethodDesc;
                        if (_sos.GetMethodDescData(md, 0, out _mdData) && _sos.GetCodeHeaderData(_mdData.NativeCodeAddr, out _codeHeaderData))
                            methods.Add(new ClrmdMethod(type, this));
                    }
                }
            }

            _cache.ReportMemory(type.MethodTable, methods.Count * _methodSize);
            return methods.ToArray();
        }

        public ClrMethod CreateMethodFromHandle(ClrHeap heap, ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return null;

            ClrType type = GetOrCreateType(heap, mdData.MethodTable, 0);

            _ptr = methodDesc;
            _mdData = mdData;
            return new ClrmdMethod(type, this);
        }



        void ITypeFactory.CreateFieldsForType(ClrType type, out ClrInstanceField[] fields, out ClrStaticField[] statics)
        {
            CreateFieldsForMethodTableWorker(type, out fields, out statics);
            if (fields == null)
                fields = Array.Empty<ClrInstanceField>();

            if (statics == null)
                statics = Array.Empty<ClrStaticField>();
        }


        void CreateFieldsForMethodTableWorker(ClrType type, out ClrInstanceField[] fields, out ClrStaticField[] statics)
        {
            fields = null;
            statics = null;

            if (type.IsFree)
                return;

            if (!_sos.GetFieldInfo(type.MethodTable, out V4FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
                return;

            _cache.ReportMemory(type.MethodTable, fieldInfo.NumInstanceFields * _instFieldSize + fieldInfo.NumStaticFields * _staticFieldSize);
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
            while (fieldNum + staticNum < fields.Length + statics.Length && nextField != 0)
            {
                if (!_sos.GetFieldData(nextField, out _fieldData))
                    break;

                if (_fieldData.IsStatic != 0)
                    statics[staticNum++] = new ClrmdStaticField(type, this);
                else if (_fieldData.IsContextLocal == 0 && _fieldData.IsThreadLocal == 0)
                    fields[fieldNum++] = new ClrmdField(type, this);

                nextField = _fieldData.NextField;
            }

            if (fieldNum != fields.Length)
                Array.Resize(ref fields, fieldNum);

            if (staticNum != statics.Length)
                Array.Resize(ref statics, staticNum);

            Array.Sort(fields, (a, b) => a.Offset.CompareTo(b.Offset));
        }

        public MetaDataImport GetMetaDataImport(ClrModule module) => _sos.GetMetadataImport(module.Address);

        ulong IRCWData.Address => _ptr;
        ulong IRCWData.IUnknown => _rcwData.IUnknownPointer;
        ulong IRCWData.VTablePointer => _rcwData.VTablePointer;
        int IRCWData.RefCount => _rcwData.RefCount;
        ulong IRCWData.ManagedObject => _rcwData.ManagedObject;
        bool IRCWData.Disconnected => _rcwData.IsDisconnected != 0;
        ulong IRCWData.CreatorThread => _rcwData.CreatorThread;

        IReadOnlyList<ComInterfaceData> IRCWData.GetInterfaces(ClrRuntime runtime)
        {
            COMInterfacePointerData[] ifs = _sos.GetRCWInterfaces(_ptr, _rcwData.InterfaceCount);
            return CreateComInterfaces(runtime, ifs);
        }

        ulong ICCWData.Address => _ccwData.CCWAddress;
        ulong ICCWData.IUnknown => _ccwData.OuterIUnknown;
        ulong ICCWData.Object => _ccwData.ManagedObject;
        ulong ICCWData.Handle => _ccwData.Handle;
        int ICCWData.RefCount => _ccwData.RefCount + _ccwData.JupiterRefCount;
        int ICCWData.JupiterRefCount => _ccwData.JupiterRefCount;

        IReadOnlyList<ComInterfaceData> ICCWData.GetInterfaces(ClrRuntime runtime)
        {
            COMInterfacePointerData[] ifs = _sos.GetCCWInterfaces(_ptr, _ccwData.InterfaceCount);
            return CreateComInterfaces(runtime, ifs);
        }

        private ComInterfaceData[] CreateComInterfaces(ClrRuntime runtime, COMInterfacePointerData[] ifs)
        {
            ComInterfaceData[] result = new ComInterfaceData[ifs.Length];

            for (int i = 0; i < ifs.Length; i++)
                result[i] = new ComInterfaceData(GetOrCreateType(GetOrCreateHeap(runtime), ifs[0].MethodTable, 0), ifs[0].InterfacePointer);
            return result;
        }

        bool IFieldHelpers.ReadProperties(ClrType type, out string name, out FieldAttributes attributes, out Utilities.SigParser sigParser)
        {
            MetaDataImport import = type?.Module?.MetadataImport;
            if (import == null || !import.GetFieldProps((int)_fieldData.FieldToken, out name, out attributes, out IntPtr fieldSig, out int sigLen, out _, out _))
            {
                name = null;
                attributes = default;
                sigParser = default;
                return false;
            }

            name = _cache.ReportOrInternString(type.MethodTable, name);
            sigParser = new Utilities.SigParser(fieldSig, sigLen);
            return true;
        }

        ulong IFieldHelpers.GetStaticFieldAddress(ClrStaticField field, ClrAppDomain appDomain)
        {
            ClrType type = field.Parent;
            ClrModule module = type?.Module;
            if (module == null)
                return 0;

            bool shared = type.IsShared;


            // TODO: Perf and testing
            if (shared)
            {
                if (!_sos.GetModuleData(module.Address, out ModuleData data))
                    return 0;

                if (!_sos.GetDomainLocalModuleDataFromAppDomain(appDomain.Address, (int)data.ModuleID, out DomainLocalModuleData dlmd))
                    return 0;

                if (!shared && !IsInitialized(in dlmd, type.MetadataToken))
                    return 0;

                if (field.ElementType.IsPrimitive())
                    return dlmd.NonGCStaticDataStart + (uint)field.Offset;
                else
                    return dlmd.GCStaticDataStart + (uint)field.Offset;
            }
            else
            {
                if (!_sos.GetDomainLocalModuleDataFromModule(module.Address, out DomainLocalModuleData dlmd))
                    return 0;

                if (field.ElementType.IsPrimitive())
                    return dlmd.NonGCStaticDataStart + (uint)field.Offset;
                else
                    return dlmd.GCStaticDataStart + (uint)field.Offset;
            }
        }


        private bool IsInitialized(in DomainLocalModuleData data, uint token)
        {
            ulong flagsAddr = data.ClassData + (token & ~0x02000000u) - 1;
            if (!DataReader.Read(flagsAddr, out byte flags))
                return false;

            return (flags & 1) != 0;
        }

        public string GetTypeName(ulong mt) => _cache.ReportOrInternString(mt, _sos.GetMethodTableName(mt));

        ulong ITypeHelpers.GetLoaderAllocatorHandle(ulong mt)
        {
            if (_sos6 != null && _sos6.GetMethodTableCollectibleData(mt, out MethodTableCollectibleData data) && data.Collectible != 0)
                return data.LoaderAllocatorObjectHandle;

            return 0;
        }


        IObjectData ITypeHelpers.GetObjectData(ulong objRef)
        {
            // todo remove
            _sos.GetObjectData(objRef, out V45ObjectData data);
            return data;
        }

        ITypeHelpers ITypeData.Helpers => this;

        bool ITypeData.IsShared => _mtData.Shared != 0;
        uint ITypeData.Token => _mtData.Token;
        ulong ITypeData.MethodTable => _ptr;
        ulong ITypeData.ParentMethodTable => _mtData.ParentMethodTable;
        ulong ITypeData.ComponentMethodTable => 0;
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

        IFieldHelpers IFieldData.Helpers => this;

        ClrElementType IFieldData.ElementType => (ClrElementType)_fieldData.ElementType;

        uint IFieldData.Token => _fieldData.FieldToken;

        int IFieldData.Offset => (int)_fieldData.Offset;

        ulong IFieldData.TypeMethodTable => _fieldData.TypeMethodTable;

        string IMethodHelpers.GetSignature(ulong methodDesc) => _cache.ReportOrInternString(methodDesc, _sos.GetMethodDescName(methodDesc));

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
            _sos = sos;
            DataReader = reader;
            TypeFactory = factory;
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
                    if (next == 0 || seen.Add(next))
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
                segments.Add(new HeapSegment(clrHeap, this));
                address = _segment.Next;
            }
        }

        public IEnumerable<(ulong, ulong)> EnumerateDependentHandleLinks()
        {
            List<(ulong, ulong)> result = new List<(ulong, ulong)>();
            // TODO use smarter sos enum for only dependent handles
            using SOSHandleEnum handleEnum = _sos.EnumerateHandles();

            Span<HandleData> handles = stackalloc HandleData[16];
            int fetched = 0;
            while ((fetched = handleEnum.ReadHandles(handles, 16)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    if (handles[i].Type == (int)HandleType.Dependent)
                    {
                        ulong obj = DataReader.ReadPointerUnsafe(handles[i].Handle);
                        if (obj != 0)
                            result.Add((obj, handles[i].Secondary));
                    }
                }
            }

            return result;
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

            Debug.Assert(Start <= End);
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
}