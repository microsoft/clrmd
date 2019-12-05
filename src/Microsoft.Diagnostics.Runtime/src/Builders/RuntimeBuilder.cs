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
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    unsafe sealed class RuntimeBuilder : IRuntimeHelpers, ITypeFactory, ITypeHelpers, IModuleHelpers, IMethodHelpers, IClrObjectHelpers, IFieldHelpers,
                                         IAppDomainHelpers, IThreadHelpers, IExceptionHelpers, IHeapHelpers

    {
        private bool _disposed;
        private readonly ClrInfo _clrinfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6 _sos6;
        private readonly int _threads;
        private readonly ulong _finalizer;
        private readonly ulong _firstThread;

        private volatile ClrType[] _basicTypes;
        private readonly Dictionary<ulong, ClrAppDomain> _domains = new Dictionary<ulong, ClrAppDomain>();
        private readonly Dictionary<ulong, ClrModule> _modules = new Dictionary<ulong, ClrModule>();

        private ClrRuntime _runtime;
        private ClrHeap _heap;

        private readonly Dictionary<ulong, ClrType> _cache = new Dictionary<ulong, ClrType>();

        private readonly ObjectPool<TypeBuilder> _typeBuilders;
        private readonly ObjectPool<MethodBuilder> _methodBuilders;
        private readonly ObjectPool<FieldBuilder> _fieldBuilders;
        private ModuleBuilder _moduleBuilder;

        public IDataReader DataReader { get; }
        public IHeapBuilder HeapBuilder => new HeapBuilder(this, _sos, DataReader, _firstThread);

        public ITypeFactory Factory => this;

        public RuntimeBuilder(ClrInfo clr, DacLibrary library)
        {
            _clrinfo = clr;
            _library = library;
            _dac = _library.DacPrivateInterface;
            _sos = _library.SOSDacInterface;
            _sos6 = _library.SOSDacInterface6;
            DataReader = _clrinfo.DataTarget.DataReader;

            int version = 0;
            if (_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))) != 0)
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request ThreadStoreData.");

            _threads = data.ThreadCount;
            _firstThread = data.FirstThread;
            _finalizer = data.FinalizerThread;

            _typeBuilders = new ObjectPool<TypeBuilder>(CreateTypeBuilder, (owner, obj) => obj.Owner = owner);
            _methodBuilders = new ObjectPool<MethodBuilder>(CreateMethodBuilder, (owner, obj) => obj.Owner = owner);
            _fieldBuilders = new ObjectPool<FieldBuilder>(CreateFieldBuilder, (owner, obj) => obj.Owner = owner);

            Dictionary<ulong, ulong> moduleSizes = new Dictionary<ulong, ulong>();
            foreach (ModuleInfo mi in _clrinfo.DataTarget.EnumerateModules())
                moduleSizes[mi.ImageBase] = mi.FileSize;

            _moduleBuilder = new ModuleBuilder(this, _sos, moduleSizes);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _runtime.Dispose();
                _dac.Dispose();
                _sos.Dispose();
                _sos6?.Dispose();
                _library.Dispose();
            }
        }

        private TypeBuilder CreateTypeBuilder() => new TypeBuilder(_sos, this);
        private MethodBuilder CreateMethodBuilder() => new MethodBuilder(_sos, this);
        private FieldBuilder CreateFieldBuilder() => new FieldBuilder(_sos, this);

        public ClrModule GetOrCreateModule(ClrAppDomain domain, ulong addr)
        {
            CheckDisposed();
            lock (_modules)
            {
                if (_modules.TryGetValue(addr, out ClrModule result))
                    return result;

                if (!_moduleBuilder.Init(addr))
                    return null;

                _modules[addr] = result = new ClrmdModule(domain, _moduleBuilder);
                return result;
            }
        }

        private void CheckDisposed()
        {
            // We will blame the runtime for being disposed if it's there because that will be more meaningful to the user.
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClrRuntime));
        }

        IEnumerable<ClrStackRoot> IThreadHelpers.EnumerateStackRoots(ClrThread thread)
        {
            CheckDisposed();

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
            CheckDisposed();

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
            CheckDisposed();

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
            CheckDisposed();

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
            CheckDisposed();

            ClrThread[] threads = new ClrThread[_threads];

            // Ensure we don't hit a loop due to corrupt data

            ThreadBuilder threadBuilder = new ThreadBuilder(_sos, _finalizer, this);

            HashSet<ulong> seen = new HashSet<ulong>() { 0 };
            ulong addr = _firstThread;
            int i;
            for (i = 0; i < threads.Length && seen.Add(addr); i++)
            {
                if (!threadBuilder.Init(addr))
                    break;

                addr = threadBuilder.NextThread;

                ClrAppDomain domain = GetOrCreateAppDomain(null, threadBuilder.Domain);
                threads[i] = new ClrmdThread(threadBuilder, runtime, domain);
            }

            // Shouldn't happen unless we caught the runtime at a really bad place
            if (i < threads.Length)
                Array.Resize(ref threads, i);

            return threads;
        }

        IReadOnlyList<ClrAppDomain> IRuntimeHelpers.GetAppDomains(ClrRuntime runtime, out ClrAppDomain system, out ClrAppDomain shared)
        {
            CheckDisposed();

            system = null;
            shared = null;

            AppDomainBuilder builder = new AppDomainBuilder(_sos, this);

            if (builder.SystemDomain != 0)
                system = GetOrCreateAppDomain(builder, builder.SystemDomain);

            if (builder.SharedDomain != 0)
                shared = GetOrCreateAppDomain(builder, builder.SharedDomain);

            ulong[] domainList = _sos.GetAppDomainList(builder.AppDomainCount);
            ClrAppDomain[] result = new ClrAppDomain[domainList.Length];
            int i = 0;
            foreach (ulong domain in domainList)
            {
                ClrAppDomain ad = GetOrCreateAppDomain(builder, domain);
                if (ad != null)
                    result[i++] = ad;
            }

            if (i < result.Length)
                Array.Resize(ref result, i);

            return result;
        }

        public ClrAppDomain GetOrCreateAppDomain(AppDomainBuilder builder, ulong domain)
        {
            CheckDisposed();

            lock (_domains)
            {
                if (_domains.TryGetValue(domain, out ClrAppDomain result))
                    return result;

                if (builder == null)
                    builder = new AppDomainBuilder(_sos, this);

                if (!builder.Init(domain))
                    return null;

                return _domains[domain] = new ClrmdAppDomain(GetOrCreateRuntime(), builder);
            }
        }


        IEnumerable<ClrHandle> IRuntimeHelpers.EnumerateHandleTable(ClrRuntime runtime)
        {
            CheckDisposed();

            // Enumerating handles should be sufficiently rare as to not need to use ArrayPool
            HandleData[] handles = new HandleData[128];
            return EnumerateHandleTable(runtime, handles);
        }

        IEnumerable<(ulong, ulong)> IHeapHelpers.EnumerateDependentHandleLinks()
        {
            CheckDisposed();

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
            CheckDisposed();

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

        void IRuntimeHelpers.FlushCachedData()
        {
            _heap = null;
            _dac.Flush();
            _cache.Clear();

            lock (_domains)
                _domains.Clear();

            _basicTypes = null;

            lock (_modules)
            {
                _modules.Clear();
                Dictionary<ulong, ulong> moduleSizes = new Dictionary<ulong, ulong>();
                foreach (ModuleInfo mi in _clrinfo.DataTarget.EnumerateModules())
                    moduleSizes[mi.ImageBase] = mi.FileSize;

                _moduleBuilder = new ModuleBuilder(this, _sos, moduleSizes);
            }
        }

        ulong IRuntimeHelpers.GetMethodDesc(ulong ip) => _sos.GetMethodDescPtrFromIP(ip);
        string IRuntimeHelpers.GetJitHelperFunctionName(ulong ip) => _sos.GetJitHelperFunctionName(ip);

        public IExceptionHelpers ExceptionHelpers => this;

        IReadOnlyList<ClrStackFrame> IExceptionHelpers.GetExceptionStackTrace(ClrThread thread, ClrObject obj)
        {
            CheckDisposed();

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


        string IAppDomainHelpers.GetConfigFile(ClrAppDomain domain) => _sos.GetConfigFile(domain.Address);
        string IAppDomainHelpers.GetApplicationBase(ClrAppDomain domain) => _sos.GetAppBase(domain.Address);
        IEnumerable<ClrModule> IAppDomainHelpers.EnumerateModules(ClrAppDomain domain)
        {
            CheckDisposed();

            foreach (ulong assembly in _sos.GetAssemblyList(domain.Address))
                foreach (ulong module in _sos.GetModuleList(assembly))
                    yield return GetOrCreateModule(domain, module);
        }


        public ClrType TryGetType(ulong mt)
        {
            lock (_cache)
            {
                _cache.TryGetValue(mt, out ClrType result);
                return result;
            }
        }

        IReadOnlyList<(ulong, uint)> IModuleHelpers.GetSortedTypeDefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable);
        IReadOnlyList<(ulong, uint)> IModuleHelpers.GetSortedTypeRefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeRefToMethodTable);

        private IReadOnlyList<(ulong, uint)> GetSortedMap(ClrModule module, SOSDac.ModuleMapTraverseKind kind)
        {
            CheckDisposed();

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
            CheckDisposed();

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
            CheckDisposed();

            if (_basicTypes == null)
            {
                ClrType[] basicTypes = new ClrType[(int)ClrElementType.SZArray];
                int count = 0;
                ClrModule bcl = GetOrCreateRuntime().BaseClassLibrary;
                if (bcl != null && bcl.MetadataImport != null)
                {
                    foreach ((ulong mt, uint token) in bcl.EnumerateTypeDefToMethodTableMap())
                    {
                        string name = _sos.GetMethodTableName(mt);
                        ClrElementType type = name switch
                        {
                            "System.Boolean" => ClrElementType.Boolean,
                            "System.Char" => ClrElementType.Char,
                            "System.SByte" => ClrElementType.Int8,
                            "System.Byte" => ClrElementType.UInt8,
                            "System.Int16" => ClrElementType.Int16,
                            "System.UInt16" => ClrElementType.UInt16,
                            "System.Int32" => ClrElementType.Int32,
                            "System.UInt32" => ClrElementType.UInt32,
                            "System.Int64" => ClrElementType.Int64,
                            "System.UInt64" => ClrElementType.UInt64,
                            "System.Single" => ClrElementType.Float,
                            "System.Double" => ClrElementType.Double,
                            "System.IntPtr" => ClrElementType.NativeInt,
                            "System.UIntPtr" => ClrElementType.NativeUInt,
                            "System.ValueType" => ClrElementType.Struct,
                            "System.Array" => ClrElementType.SZArray,
                            _ => ClrElementType.Unknown,
                        };

                        if (type != ClrElementType.Unknown)
                        {
                            basicTypes[(int)type - 1] = GetOrCreateType(mt, 0);
                            count++;

                            if (count == 16)
                                break;
                        }
                    }
                }

                Interlocked.CompareExchange(ref _basicTypes, basicTypes, null);
            }

            int index = (int)basicType - 1;
            if (index < 0 || index > _basicTypes.Length)
                throw new ArgumentException($"Cannot create type for ClrElementType {basicType}");

            if (_basicTypes[index] != null)
                return _basicTypes[index];

            return _basicTypes[index] = new ClrmdPrimitiveType(this, GetOrCreateRuntime().BaseClassLibrary, GetOrCreateHeap(), basicType);
        }

        public ClrType GetOrCreateType(ulong mt, ulong obj) => mt == 0 ? null : GetOrCreateType(GetOrCreateHeap(), mt, obj);

        public ClrType GetOrCreateType(ClrHeap heap, ulong mt, ulong obj)
        {
            CheckDisposed();

            if (mt == 0)
                return null;

            {
                ClrType result = TryGetType(mt);
                if (result != null)
                {
                    if (obj != 0  && result.ComponentType == null && result.IsArray && result is ClrmdArrayType type)
                        TryGetComponentType(type, obj);

                    return result;
                }
            }

            {
                using TypeBuilder typeData = _typeBuilders.Rent();
                if (!typeData.Init(mt))
                    return null;

                ClrType baseType = GetOrCreateType(heap, typeData.ParentMethodTable, 0);

                ClrModule module = GetOrCreateModule(null, typeData.Module);
                if (typeData.ComponentSize == 0)
                {
                    ClrmdType result = new ClrmdType(heap, baseType, module, typeData);

                    lock (_cache)
                        _cache[mt] = result;

                    return result;
                }
                else
                {
                    ClrmdArrayType result = new ClrmdArrayType(heap, baseType, module, typeData);

                    lock (_cache)
                        _cache[mt] = result;

                    if (obj != 0 && result.IsArray)
                    {
                        Debug.Assert(result.ComponentType == null);
                        TryGetComponentType(result, obj);
                    }
                    return result;
                }
            }
        }

        public ClrType GetOrCreateTypeFromToken(ClrModule module, uint token) => module.ResolveToken(token);

        public ClrType GetOrCreateArrayType(ClrType innerType, int ranks) => innerType != null ? new ClrmdConstructedType(innerType, ranks, pointer: false) : null;
        public ClrType GetOrCreatePointerType(ClrType innerType, int depth) => innerType != null ? new ClrmdConstructedType(innerType, depth, pointer: true) : null;


        private void TryGetComponentType(ClrmdArrayType type, ulong obj)
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
            CheckDisposed();

            CCWBuilder builder = new CCWBuilder(_sos, this);
            if (!builder.Init(obj))
                return null;

            return new ComCallWrapper(builder);
        }

        RuntimeCallableWrapper ITypeFactory.CreateRCWForObject(ulong obj)
        {
            CheckDisposed();

            RCWBuilder builder = new RCWBuilder(_sos, this);
            if (!builder.Init(obj))
                return null;

            return new RuntimeCallableWrapper(GetOrCreateRuntime(), builder);
        }

        ClrMethod[] ITypeFactory.CreateMethodsForType(ClrType type)
        {
            CheckDisposed();

            ulong mt = type.MethodTable;
            if (!_sos.GetMethodTableData(mt, out MethodTableData data))
                return Array.Empty<ClrMethod>();

            if (data.NumMethods == 0)
                return Array.Empty<ClrMethod>();

            using MethodBuilder builder = _methodBuilders.Rent();


            ClrMethod[] result = new ClrMethod[data.NumMethods];

            int curr = 0;
            for (int i = 0; i < data.NumMethods; i++)
            {
                if (builder.Init(mt, i))
                    result[curr++] = new ClrmdMethod(type, builder);
            }

            if (curr < result.Length)
                Array.Resize(ref result, curr);

            return result;
        }

        public ClrMethod CreateMethodFromHandle(ulong methodDesc)
        {
            CheckDisposed();

            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return null;

            ClrType type = GetOrCreateType(mdData.MethodTable, 0);
            if (type == null)
                return null;

            ClrMethod method = type.Methods.FirstOrDefault(m => m.MethodDesc == methodDesc);
            if (method != null)
                return method;

            using MethodBuilder builder = _methodBuilders.Rent();
            if (!builder.Init(methodDesc))
                return null;

            return new ClrmdMethod(type, builder);
        }



        void ITypeFactory.CreateFieldsForType(ClrType type, out IReadOnlyList<ClrInstanceField> fields, out IReadOnlyList<ClrStaticField> statics)
        {
            CheckDisposed();

            CreateFieldsForMethodTableWorker(type, out fields, out statics);
            if (fields == null)
                fields = Array.Empty<ClrInstanceField>();

            if (statics == null)
                statics = Array.Empty<ClrStaticField>();
        }


        void CreateFieldsForMethodTableWorker(ClrType type, out IReadOnlyList<ClrInstanceField> fields, out IReadOnlyList<ClrStaticField> statics)
        {
            CheckDisposed();

            fields = null;
            statics = null;

            if (type.IsFree)
                return;

            if (!_sos.GetFieldInfo(type.MethodTable, out V4FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
            {
                if (type.BaseType != null)
                    fields = type.BaseType.Fields;
                return;
            }

            ClrInstanceField[] fieldOut = new ClrInstanceField[fieldInfo.NumInstanceFields];
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

            using FieldBuilder fieldData = _fieldBuilders.Rent();

            ulong nextField = fieldInfo.FirstFieldAddress;
            int other = 0;
            while (other + fieldNum + staticNum < fieldOut.Length + staticOut.Length && nextField != 0)
            {
                if (!fieldData.Init(nextField))
                    break;

                if (fieldData.IsContextLocal || fieldData.IsThreadLocal)
                {
                    other++;
                }
                else if (fieldData.IsStatic)
                {
                    staticOut[staticNum++] = new ClrmdStaticField(type, fieldData);
                }
                else
                {
                    fieldOut[fieldNum++] = new ClrmdField(type, fieldData);
                }

                nextField = fieldData.NextField;
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


        public ComInterfaceData[] CreateComInterfaces(COMInterfacePointerData[] ifs)
        {
            CheckDisposed();

            ComInterfaceData[] result = new ComInterfaceData[ifs.Length];

            for (int i = 0; i < ifs.Length; i++)
                result[i] = new ComInterfaceData(GetOrCreateType(ifs[0].MethodTable, 0), ifs[0].InterfacePointer);
            return result;
        }

        bool IFieldHelpers.ReadProperties(ClrType type, uint fieldToken, out string name, out FieldAttributes attributes, out Utilities.SigParser sigParser)
        {
            CheckDisposed();

            MetaDataImport import = type?.Module?.MetadataImport;
            if (import == null || !import.GetFieldProps(fieldToken, out name, out attributes, out IntPtr fieldSig, out int sigLen, out _, out _))
            {
                name = null;
                attributes = default;
                sigParser = default;
                return false;
            }

            sigParser = new Utilities.SigParser(fieldSig, sigLen);
            return true;
        }

        ulong IFieldHelpers.GetStaticFieldAddress(ClrStaticField field, ClrAppDomain appDomain)
        {
            CheckDisposed();

            if (appDomain is null)
                return 0;

            ClrType type = field.Parent;
            ClrModule module = type?.Module;
            if (module is null)
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

        public string GetTypeName(ulong mt) => _sos.GetMethodTableName(mt);


        IClrObjectHelpers ITypeHelpers.ClrObjectHelpers => this;
        ulong ITypeHelpers.GetLoaderAllocatorHandle(ulong mt)
        {
            CheckDisposed();

            if (_sos6 != null && _sos6.GetMethodTableCollectibleData(mt, out MethodTableCollectibleData data) && data.Collectible != 0)
                return data.LoaderAllocatorObjectHandle;

            return 0;
        }


        IObjectData ITypeHelpers.GetObjectData(ulong objRef)
        {
            CheckDisposed();

            // todo remove
            _sos.GetObjectData(objRef, out V45ObjectData data);
            return data;
        }


        string IMethodHelpers.GetSignature(ulong methodDesc) => _sos.GetMethodDescName(methodDesc);

        ulong IMethodHelpers.GetILForModule(ulong address, uint rva) => _sos.GetILForModule(address, rva);

        IReadOnlyList<ILToNativeMap> IMethodHelpers.GetILMap(ulong ip, in HotColdRegions hotColdInfo)
        {
            CheckDisposed();

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