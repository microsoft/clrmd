// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{

    // This class will not be marked public.
    // This implementation takes a lot of shortcuts to avoid allocations, and as a result the interfaces
    // it implements have very odd constraints around how they can be used.  All Clrmd* types understand
    // these constraints and use it properly, but since this class doesn't behave as a developer would
    // expect, it must stay internal only.
    unsafe sealed class RuntimeBuilder : IRuntimeHelpers, ITypeFactory, ITypeHelpers, ITypeData, IModuleData, IModuleHelpers,
                                         IMethodHelpers, IMethodData, IClrObjectHelpers, IFieldData, IFieldHelpers, IAppDomainData,
                                         IAppDomainHelpers, IThreadData, IThreadHelpers, ICCWData, IRCWData, IExceptionHelpers,
                                         IHeapHelpers

    {
        private readonly ClrInfo _clrinfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6 _sos6;
        private readonly int _threads;
        private readonly ulong _finalizer;
        private readonly ulong _firstThread;

        private readonly ClrType[] _basicTypes = new ClrType[(int)ClrElementType.SZArray];
        private readonly Dictionary<ulong, ClrModule> _modules = new Dictionary<ulong, ClrModule>();
        private readonly Dictionary<ulong, ulong> _moduleSizes = new Dictionary<ulong, ulong>();
        private List<AllocationContext> _threadAllocContexts;

        private ClrRuntime _runtime;
        private ClrHeap _heap;

        private readonly ITypeCache _cache = new DefaultTypeCache();
        private readonly int _typeSize = 8; // todo
        private readonly int _methodSize = 8;
        private readonly int _instFieldSize = 8;
        private readonly int _staticFieldSize = 8;

        private ulong _ptr;
        private AppDomainStoreData _adStore;
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

            _modules[addr] = result = new ClrmdModule(domain, this);
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

        IEnumerable<ClrStackRoot> IThreadHelpers.EnumerateStackRoots(ClrThread thread)
        {
            // TODO: rethink stack roots and this code
            using SOSStackRefEnum stackRefEnum = _sos.EnumerateStackRefs(thread.OSThreadId);
            if (stackRefEnum == null)
                yield break;

            ClrStackFrame[] stack = thread.EnumerateStackTrace().Take(2048).ToArray();

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

                    bool interior = (refs[i].Flags & GCInteriorFlag) == GCInteriorFlag;
                    bool pinned = (refs[i].Flags & GCPinnedFlag) == GCPinnedFlag;

                    ClrObject obj;
                    ClrType type = heap.GetObjectType(refs[i].Object); // Will fail in the interior case

                    if (type != null)
                        obj = new ClrObject(refs[i].Object, type);
                    else
                        obj = new ClrObject();

                    ClrStackFrame frame = stack.SingleOrDefault(f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);
                    if (frame == null)
                        frame = new ClrmdStackFrame(thread, null, refs[i].Source, refs[i].StackPointer, ClrStackFrameKind.Unknown, null, null);

                    if (interior || type != null)
                        yield return new ClrStackRoot(refs[i].Address, obj, frame, interior, pinned);
                }
            }
        }

        IEnumerable<ClrStackFrame> IThreadHelpers.EnumerateStackTrace(ClrThread thread, bool includeContext)
        {
            using ClrStackWalk stackwalk = _dac.CreateStackWalk(thread.OSThreadId, 0xf);
            if (stackwalk == null)
                yield break;

            int ipOffset;
            int spOffset;
            int contextSize;
            uint contextFlags;
            if (IntPtr.Size == 4)
            {
                ipOffset = 184;
                spOffset = 196;
                contextSize = 716;
                contextFlags = 0x1003f;
            }
            else
            {
                ipOffset = 248;
                spOffset = 152;
                contextSize = 1232;
                contextFlags = 0x10003f;
            }

            byte[] context = ArrayPool<byte>.Shared.Rent(contextSize);
            try
            {
                do
                {
                    if (!stackwalk.GetContext(contextFlags, contextSize, out _, context))
                        break;

                    ulong ip, sp;

                    if (IntPtr.Size == 4)
                    {
                        ip = BitConverter.ToUInt32(context, ipOffset);
                        sp = BitConverter.ToUInt32(context, spOffset);
                    }
                    else
                    {
                        ip = BitConverter.ToUInt64(context, ipOffset);
                        sp = BitConverter.ToUInt64(context, spOffset);
                    }

                    ulong frameVtbl = stackwalk.GetFrameVtable();
                    if (frameVtbl != 0)
                    {
                        sp = frameVtbl;
                        frameVtbl = DataReader.ReadPointerUnsafe(sp);
                    }

                    byte[] contextCopy = null;
                    if (includeContext)
                    {
                        contextCopy = new byte[contextSize];
                        Buffer.BlockCopy(context, 0, contextCopy, 0, contextSize);
                    }

                    ClrStackFrame frame = GetStackFrame(thread, contextCopy, ip, sp, frameVtbl);
                    yield return frame;
                } while (stackwalk.Next());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(context);
            }
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
                    innerMethod = CreateMethodFromHandle(md);

                return new ClrmdStackFrame(thread, context, ip, sp, ClrStackFrameKind.Runtime, innerMethod, frameName);
            }
            else
            {
                ClrMethod method = thread?.Runtime?.GetMethodByInstructionPointer(ip);
                return new ClrmdStackFrame(thread, context, ip, sp, ClrStackFrameKind.ManagedMethod, method, null);
            }
        }


        ClrModule IRuntimeHelpers.GetBaseClassLibrary(ClrRuntime runtime)
        {
            if (_sos.GetCommonMethodTables(out CommonMethodTables mts))
            {
                if (_sos.GetMethodTableData(mts.ObjectMethodTable, out MethodTableData mtData))
                {
                    ClrModule result = GetOrCreateModule(null, mtData.Module);
                    if (result != null)
                        return result;
                }
            }


            ClrModule mscorlib = null;
            string moduleName = runtime.ClrInfo.Flavor == ClrFlavor.Core
                ? "SYSTEM.PRIVATE.CORELIB"
                : "MSCORLIB";

            if (runtime.SharedDomain != null)
                foreach (ClrModule module in runtime.SharedDomain.Modules)
                    if (module.Name.ToUpperInvariant().Contains(moduleName))
                        return module;

            foreach (ClrAppDomain domain in runtime.AppDomains)
                foreach (ClrModule module in domain.Modules)
                    if (module.Name.ToUpperInvariant().Contains(moduleName))
                        return module;

            return mscorlib;
        }
    

        IReadOnlyList<ClrThread> IRuntimeHelpers.GetThreads(ClrRuntime runtime)
        {
            ClrThread[] threads = new ClrThread[_threads];

            // Ensure we don't hit a loop due to corrupt data
            HashSet<ulong> seen = new HashSet<ulong>() { 0 };
            ulong addr = _firstThread;
            int i;
            for (i = 0; i < threads.Length && seen.Add(addr); i++)
            {
                if (!_sos.GetThreadData(addr, out _threadData))
                    break;

                _ptr = addr;
                addr = _threadData.NextThread;

                ClrAppDomain domain = GetOrCreateAppDomain(_threadData.Domain);
                threads[i] = new ClrmdThread(this, runtime, domain);
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

            if (!_sos.GetAppDomainStoreData(out _adStore))
                return Array.Empty<ClrAppDomain>();

            if (_adStore.SystemDomain != 0)
                system = GetOrCreateAppDomain(_adStore.SystemDomain);

            if (_adStore.SharedDomain != 0)
                shared = GetOrCreateAppDomain(_adStore.SharedDomain);

            ulong[] domainList = _sos.GetAppDomainList(_adStore.AppDomainCount);
            ClrAppDomain[] result = new ClrAppDomain[domainList.Length];
            int i = 0;
            foreach (ulong domain in domainList)
            {
                ClrAppDomain ad = GetOrCreateAppDomain(domain);
                if (ad != null)
                    result[i++] = ad;
            }

            if (i < result.Length)
                Array.Resize(ref result, i);

            return result;
        }

        private readonly Dictionary<ulong, ClrAppDomain> _domains = new Dictionary<ulong, ClrAppDomain>();
        public ClrAppDomain GetOrCreateAppDomain(ulong domain)
        {
            if (_domains.TryGetValue(domain, out ClrAppDomain result))
                return result;

            if (!_sos.GetAppDomainData(domain, out _appDomainData))
                return null;

            _ptr = domain;
            return _domains[domain] = new ClrmdAppDomain(GetOrCreateRuntime(), this);
        }


        IEnumerable<ClrHandle> IRuntimeHelpers.EnumerateHandleTable(ClrRuntime runtime)
        {
            // Enumerating handles should be sufficiently rare as to not need to use ArrayPool
            HandleData[] handles = new HandleData[128];
            return EnumerateHandleTable(runtime, handles);
        }

        public IEnumerable<(ulong, ulong)> EnumerateDependentHandleLinks()
        {
            // TODO use smarter sos enum for only dependent handles
            using SOSHandleEnum handleEnum = _sos.EnumerateHandles(ClrHandleKind.Dependent);

            HandleData[] handles = new HandleData[32];
            int fetched = 0;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
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

        IEnumerable<ClrHandle> EnumerateHandleTable(ClrRuntime runtime, HandleData[] handles)
        {
            // TODO: Use smarter handle enum overload in _sos
            Dictionary<ulong, ClrAppDomain> domains = new Dictionary<ulong, ClrAppDomain>();
            if (runtime.SharedDomain != null)
                domains[runtime.SharedDomain.Address] = runtime.SharedDomain;

            if (runtime.SystemDomain != null)
                domains[runtime.SystemDomain.Address] = runtime.SystemDomain;

            foreach (ClrAppDomain domain in runtime.AppDomains)
            {
                // Don't use .ToDictionary in case we have bad data
                domains[domain.Address] = domain;
            }

            using SOSHandleEnum handleEnum = _sos.EnumerateHandles();

            int fetched = 0;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    ulong obj = DataReader.ReadPointerUnsafe(handles[i].Handle);
                    ulong mt = 0;
                    if (obj != 0)
                        mt = DataReader.ReadPointerUnsafe(obj);

                    if (mt != 0)
                    {
                        ClrType type = GetOrCreateType(mt, obj);
                        ClrType dependent = null;
                        if (handles[i].Type == (int)ClrHandleKind.Dependent && handles[i].Secondary != 0)
                        {
                            ulong dmt = DataReader.ReadPointerUnsafe(handles[i].Secondary);

                            if (dmt != 0)
                                dependent = GetOrCreateType(dmt, handles[i].Secondary);
                        }

                        domains.TryGetValue(handles[i].AppDomain, out ClrAppDomain domain);

                        ClrObject clrObj = type != null ? new ClrObject(obj, type) : default;
                        ClrHandle handle = new ClrHandle(in handles[i], clrObj, domain, dependent);
                        yield return handle;
                    }
                }
            }
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

        IReadOnlyList<ClrStackFrame> IExceptionHelpers.GetExceptionStackTrace(ClrThread thread, ClrObject obj)
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

                ClrMethod method = CreateMethodFromHandle(md);
                result[i] = new ClrmdStackFrame(thread, null, ip, sp, ClrStackFrameKind.ManagedMethod, method, frameName: null);
                dataPtr += (ulong)elementSize;
            }

            return result;
        }


        IAppDomainHelpers IAppDomainData.Helpers => this;
        string IAppDomainData.Name
        {
            get
            {
                if (_adStore.SharedDomain == _ptr)
                    return "Shared Domain";

                if (_adStore.SystemDomain == _ptr)
                    return "System Domain";

                string name = _sos.GetAppDomainName(_ptr);
                _cache.ReportOrInternString(_ptr, name);
                return name;
            }
        }
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

        public ClrRuntime GetOrCreateRuntime()
        {
            if (_runtime != null)
                return _runtime;

            ClrmdRuntime runtime = new ClrmdRuntime(_clrinfo, _library, this);
            _runtime = runtime;

            runtime.Initialize();
            return _runtime;
         
        }
        public ClrHeap GetOrCreateHeap() => _heap ?? (_heap = new ClrmdHeap(GetOrCreateRuntime(), HeapBuilder));

        public ClrType GetOrCreateBasicType(ClrElementType basicType)
        {
            int index = (int)basicType - 1;
            if (index < 0 || index > _basicTypes.Length)
                throw new ArgumentException($"Cannot create type for ClrElementType {basicType}");

            if (_basicTypes[index] != null)
                return _basicTypes[index];

            return _basicTypes[index] = new ClrmdPrimitiveType(this, GetOrCreateRuntime().BaseClassLibrary, GetOrCreateHeap(), basicType);
        }

        public ClrType GetOrCreateType(ulong mt, ulong obj) => GetOrCreateType(GetOrCreateHeap(), mt, obj);

        public ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj)
        {
            {
                ClrType result = _cache.GetStoredType(mt);
                if (result != null)
                {
                    if (obj != 0  && result.ComponentType == null && result.IsArray && result is ClrmdType type)
                        TryGetComponentType(type, obj);

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
                    TryGetComponentType(result, obj);
                }

                return result;
            }
        }

        public ClrType GetOrCreateTypeFromToken(ClrModule module, uint token) => module.ResolveToken(token);

        public ClrType GetOrCreateArrayType(ClrType innerType, int ranks) => innerType != null ? new ClrmdPointerArrayType(innerType, ranks, pointer: false) : null;
        public ClrType GetOrCreatePointerType(ClrType innerType, int depth) => innerType != null ? new ClrmdPointerArrayType(innerType, depth, pointer: true) : null;


        private void TryGetComponentType(ClrmdType type, ulong obj)
        {
            ClrType result = null;
            if (_sos.GetObjectData(obj, out V45ObjectData data))
            {
                if (data.ElementTypeHandle != 0)
                    result = GetOrCreateType(data.ElementTypeHandle, 0);

                if (result == null && data.ElementType != 0)
                    result = GetOrCreateBasicType((ClrElementType)data.ElementType);

                type.SetComponentType(result);
            }
        }

        ComCallWrapper ITypeFactory.CreateCCWForObject(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.CCW == 0)
                return null;

            if (!_sos.GetCCWData(data.CCW, out _ccwData))
                return null;

            _ptr = data.CCW;
            return new ComCallWrapper(this);
        }

        RuntimeCallableWrapper ITypeFactory.CreateRCWForObject(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data) || data.RCW == 0)
                return null;

            if (!_sos.GetRCWData(data.RCW, out _rcwData))
                return null;

            _ptr = data.RCW;
            return new RuntimeCallableWrapper(GetOrCreateRuntime(), this);
        }

        ClrMethod[] ITypeFactory.CreateMethodsForType(ClrType type)
        {
            ulong mt = type.TypeHandle;
            _ptr = mt;
            if (!_sos.GetMethodTableData(mt, out MethodTableData data))
                return Array.Empty<ClrMethod>();

            ClrMethod[] result = new ClrMethod[data.NumMethods];

            int curr = 0;
            for (int i = 0; i < data.NumMethods; i++)
            {
                ulong slot = _sos.GetMethodTableSlot(mt, i);

                if (_sos.GetCodeHeaderData(slot, out _codeHeaderData))
                {
                    _ptr = _codeHeaderData.MethodDesc;
                    if (_sos.GetMethodDescData(_ptr, 0, out _mdData))
                        result[curr++] = new ClrmdMethod(type, this);
                }
            }

            if (curr < result.Length)
                Array.Resize(ref result, curr);

            _cache.ReportMemory(type.TypeHandle, result.Length * _methodSize);
            return result;
        }

        public ClrMethod CreateMethodFromHandle(ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return null;

            ClrType type = GetOrCreateType(mdData.MethodTable, 0);
            ClrMethod method = type.Methods.FirstOrDefault(m => m.MethodDesc == methodDesc);
            if (method != null)
                return method;

            _ptr = methodDesc;
            _mdData = mdData;
            return new ClrmdMethod(type, this);
        }



        void ITypeFactory.CreateFieldsForType(ClrType type, out IReadOnlyList<ClrInstanceField> fields, out IReadOnlyList<ClrStaticField> statics)
        {
            CreateFieldsForMethodTableWorker(type, out fields, out statics);
            if (fields == null)
                fields = Array.Empty<ClrInstanceField>();

            if (statics == null)
                statics = Array.Empty<ClrStaticField>();
        }


        void CreateFieldsForMethodTableWorker(ClrType type, out IReadOnlyList<ClrInstanceField> fields, out IReadOnlyList<ClrStaticField> statics)
        {
            fields = null;
            statics = null;

            if (type.IsFree)
                return;

            if (!_sos.GetFieldInfo(type.TypeHandle, out V4FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
            {
                if (type.BaseType != null)
                    fields = type.BaseType.Fields;
                return;
            }

            _cache.ReportMemory(type.TypeHandle, fieldInfo.NumInstanceFields * _instFieldSize + fieldInfo.NumStaticFields * _staticFieldSize);
            ClrInstanceField[] fieldOut = new ClrInstanceField[fieldInfo.NumInstanceFields + type.BaseType.Fields.Count];
            ClrStaticField[] staticOut = new ClrStaticField[fieldInfo.NumStaticFields];
            if (fieldInfo.NumStaticFields == 0)
                statics = Array.Empty<ClrStaticField>();
            int fieldNum = 0;
            int staticNum = 0;

            // Add base type's fields.
            if (type.BaseType != null)
            {
                foreach (ClrInstanceField field in type.BaseType.Fields)
                    fieldOut[fieldNum++] = field;
            }

            ulong nextField = fieldInfo.FirstFieldAddress;
            int overflow = 0;
            while (overflow + fieldNum + staticNum < fieldOut.Length + staticOut.Length && nextField != 0)
            {
                if (!_sos.GetFieldData(nextField, out _fieldData))
                    break;

                if (_fieldData.IsContextLocal == 0 && _fieldData.IsThreadLocal == 0)
                {
                    if (_fieldData.IsStatic != 0)
                    {
                        if (staticNum < staticOut.Length)
                            staticOut[staticNum++] = new ClrmdStaticField(type, this);
                        else
                            overflow++;
                    }
                    else
                    {
                        if (fieldNum < fieldOut.Length)
                            fieldOut[fieldNum++] = new ClrmdField(type, this);
                        else
                            overflow++;
                    }
                }

                nextField = _fieldData.NextField;
            }

            if (fieldNum != fieldOut.Length)
                Array.Resize(ref fieldOut, fieldNum);

            if (staticNum != staticOut.Length)
                Array.Resize(ref staticOut, staticNum);

            Array.Sort(fieldOut, (a, b) => a.Offset.CompareTo(b.Offset));

            fields = fieldOut;
            statics = staticOut;
        }

        public MetaDataImport GetMetaDataImport(ClrModule module) => _sos.GetMetadataImport(module.Address);

        ulong IRCWData.Address => _ptr;
        ulong IRCWData.IUnknown => _rcwData.IUnknownPointer;
        ulong IRCWData.VTablePointer => _rcwData.VTablePointer;
        int IRCWData.RefCount => _rcwData.RefCount;
        ulong IRCWData.ManagedObject => _rcwData.ManagedObject;
        bool IRCWData.Disconnected => _rcwData.IsDisconnected != 0;
        ulong IRCWData.CreatorThread => _rcwData.CreatorThread;

        IReadOnlyList<ComInterfaceData> IRCWData.GetInterfaces()
        {
            COMInterfacePointerData[] ifs = _sos.GetRCWInterfaces(_ptr, _rcwData.InterfaceCount);
            return CreateComInterfaces(ifs);
        }

        ulong ICCWData.Address => _ccwData.CCWAddress;
        ulong ICCWData.IUnknown => _ccwData.OuterIUnknown;
        ulong ICCWData.Object => _ccwData.ManagedObject;
        ulong ICCWData.Handle => _ccwData.Handle;
        int ICCWData.RefCount => _ccwData.RefCount + _ccwData.JupiterRefCount;
        int ICCWData.JupiterRefCount => _ccwData.JupiterRefCount;

        IReadOnlyList<ComInterfaceData> ICCWData.GetInterfaces()
        {
            COMInterfacePointerData[] ifs = _sos.GetCCWInterfaces(_ptr, _ccwData.InterfaceCount);
            return CreateComInterfaces(ifs);
        }

        private ComInterfaceData[] CreateComInterfaces(COMInterfacePointerData[] ifs)
        {
            ComInterfaceData[] result = new ComInterfaceData[ifs.Length];

            for (int i = 0; i < ifs.Length; i++)
                result[i] = new ComInterfaceData(GetOrCreateType(ifs[0].MethodTable, 0), ifs[0].InterfacePointer);
            return result;
        }

        bool IFieldHelpers.ReadProperties(ClrType type, uint fieldToken, out string name, out FieldAttributes attributes, out Utilities.SigParser sigParser)
        {
            MetaDataImport import = type?.Module?.MetadataImport;
            if (import == null || !import.GetFieldProps(fieldToken, out name, out attributes, out IntPtr fieldSig, out int sigLen, out _, out _))
            {
                name = null;
                attributes = default;
                sigParser = default;
                return false;
            }

            name = _cache.ReportOrInternString(type.TypeHandle, name);
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


        IClrObjectHelpers ITypeHelpers.ClrObjectHelpers => this;
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

        ulong IMethodData.MethodDesc => _ptr;
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
}