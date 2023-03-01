// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac13;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed unsafe class RuntimeBuilder : IRuntimeHelpers, ITypeFactory, ITypeHelpers, IModuleHelpers, IMethodHelpers, IFieldHelpers,
                                         IAppDomainHelpers, IThreadHelpers, IHeapHelpers
    {
        private bool _disposed;
        private readonly ClrInfo _clrInfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly CacheOptions _options;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SOSDac12? _sos12;
        private readonly SOSDac13? _sos13;
        private readonly int _threads;
        private readonly ulong _finalizer;
        private readonly ulong _firstThread;

        private readonly Dictionary<ulong, ClrAppDomain> _domains = new();
        private readonly Dictionary<ulong, ClrModule> _modules = new();

        private readonly ClrmdRuntime _runtime;
        private ClrHeap? _heap;

        private volatile StringReader? _stringReader;


        private readonly ObjectPool<MethodBuilder> _methodBuilders;
        private ModuleBuilder _moduleBuilder;

        public bool IsThreadSafe => true;

        public IDataReader DataReader { get; }

        public ITypeFactory Factory => this;

        public RuntimeBuilder(ClrInfo clr, DacLibrary library, SOSDac sos)
        {
            _clrInfo = clr;
            _library = library;
            _sos = sos;
            _options = clr.DataTarget.CacheOptions;

            _dac = _library.DacPrivateInterface;
            _sos6 = _library.SOSDacInterface6;
            _sos8 = _library.SOSDacInterface8;
            _sos12 = library.SOSDacInterface12;
            _sos13 = library.SOSDacInterface13;

            DataReader = _clrInfo.DataTarget.DataReader;

            int version = 0;
            if (!_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request ThreadStoreData.");

            _threads = data.ThreadCount;
            _firstThread = data.FirstThread;
            _finalizer = data.FinalizerThread;

            _methodBuilders = new ObjectPool<MethodBuilder>((owner, obj) => obj.Owner = owner);

            _moduleBuilder = new ModuleBuilder(this, _sos);

            _runtime = new ClrmdRuntime(clr, library, this);
            _runtime.Initialize();

            library.DacDataTarget.SetMagicCallback(_dac.Flush);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _runtime?.Dispose();
                _dac.Dispose();
                _sos.Dispose();
                _sos6?.Dispose();
                _sos8?.Dispose();
                _library.Dispose();
            }
        }

        private ClrModule? GetModule(ulong addr)
        {
            lock (_modules)
            {
                _modules.TryGetValue(addr, out ClrModule? module);
                return module;
            }
        }

        public ClrModule GetOrCreateModule(ClrAppDomain domain, ulong addr)
        {
            CheckDisposed();
            lock (_modules)
            {
                if (_modules.TryGetValue(addr, out ClrModule? result))
                    return result;

                if (_moduleBuilder.Init(addr))
                    result = _modules[addr] = new ClrmdModule(domain, _moduleBuilder);
                else
                    result = _modules[addr] = new ClrmdModule(domain, this, addr);

                return result;
            }
        }

        private void CheckDisposed()
        {
            // We will blame the runtime for being disposed if it's there because that will be more meaningful to the user.
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClrRuntime));
        }


        IEnumerable<ClrStackFrame> IThreadHelpers.EnumerateStackTrace(ClrThread thread, bool includeContext)
        {
            CheckDisposed();

            using ClrStackWalk? stackwalk = _dac.CreateStackWalk(thread.OSThreadId, 0xf);
            if (stackwalk is null)
                yield break;

            int ipOffset;
            int spOffset;
            int contextSize;
            uint contextFlags = 0;
            if (DataReader.Architecture == Architecture.Arm)
            {
                ipOffset = 64;
                spOffset = 56;
                contextSize = 416;
            }
            else if (DataReader.Architecture == Architecture.Arm64)
            {
                ipOffset = 264;
                spOffset = 256;
                contextSize = 912;
            }
            else if (DataReader.Architecture == Architecture.X86)
            {
                ipOffset = 184;
                spOffset = 196;
                contextSize = 716;
                contextFlags = 0x1003f;
            }
            else // Architecture.X64
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

                    ulong ip = context.AsSpan().AsPointer(ipOffset);
                    ulong sp = context.AsSpan().AsPointer(spOffset);

                    ulong frameVtbl = stackwalk.GetFrameVtable();
                    if (frameVtbl != 0)
                    {
                        sp = frameVtbl;
                        frameVtbl = DataReader.ReadPointer(sp);
                    }

                    byte[]? contextCopy = null;
                    if (includeContext)
                    {
                        contextCopy = context.AsSpan(0, contextSize).ToArray();
                    }

                    ClrStackFrame frame = GetStackFrame(thread, contextCopy, ip, sp, frameVtbl);
                    yield return frame;
                } while (stackwalk.Next().IsOK);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(context);
            }
        }

        private ClrStackFrame GetStackFrame(ClrThread thread, byte[]? context, ulong ip, ulong sp, ulong frameVtbl)
        {
            CheckDisposed();

            // todo: pull Method from enclosing type, don't generate methods without a parent
            if (frameVtbl != 0)
            {
                ClrMethod? innerMethod = null;
                string frameName = _sos.GetFrameName(frameVtbl);

                ulong md = _sos.GetMethodDescPtrFromFrame(sp);
                if (md != 0)
                    innerMethod = CreateMethodFromHandle(md);

                return new ClrmdStackFrame(thread, context, ip, sp, ClrStackFrameKind.Runtime, innerMethod, frameName);
            }
            else
            {
                ClrMethod? method = thread.Runtime?.GetMethodByInstructionPointer(ip);
                return new ClrmdStackFrame(thread, context, ip, sp, ClrStackFrameKind.ManagedMethod, method, null);
            }
        }

        ClrModule? IRuntimeHelpers.GetBaseClassLibrary(ClrRuntime runtime)
        {
            CheckDisposed();

            if (_sos.GetCommonMethodTables(out CommonMethodTables mts))
            {
                if (_sos.GetMethodTableData(mts.ObjectMethodTable, out MethodTableData mtData))
                {
                    return GetModule(mtData.Module);
                }
            }

            string moduleName = runtime.ClrInfo.Flavor == ClrFlavor.Core
                ? "SYSTEM.PRIVATE.CORELIB"
                : "MSCORLIB";

            if (runtime.SharedDomain != null)
                foreach (ClrModule module in runtime.SharedDomain.Modules)
                    if (!(module.Name is null) && module.Name.Contains(moduleName, StringComparison.OrdinalIgnoreCase))
                        return module;

            foreach (ClrAppDomain domain in runtime.AppDomains)
                foreach (ClrModule module in domain.Modules)
                    if (!(module.Name is null) && module.Name.Contains(moduleName, StringComparison.OrdinalIgnoreCase))
                        return module;

            return null;
        }

        ImmutableArray<ClrThread> IRuntimeHelpers.GetThreads(ClrRuntime runtime)
        {
            CheckDisposed();

            ImmutableArray<ClrThread>.Builder threads = ImmutableArray.CreateBuilder<ClrThread>(_threads);
            threads.Count = threads.Capacity;

            // Ensure we don't hit a loop due to corrupt data

            ThreadBuilder threadBuilder = new ThreadBuilder(_sos, _finalizer, this);

            HashSet<ulong> seen = new HashSet<ulong>() { 0 };
            ulong addr = _firstThread;
            int i;
            for (i = 0; i < threads.Count && seen.Add(addr); i++)
            {
                if (!threadBuilder.Init(addr))
                    break;

                addr = threadBuilder.NextThread;

                ClrAppDomain domain = GetOrCreateAppDomain(null, threadBuilder.Domain);
                threads[i] = new ClrmdThread(threadBuilder, runtime, domain);
            }

            // Shouldn't happen unless we caught the runtime at a really bad place
            if (i < threads.Count)
                threads.Capacity = threads.Count = i;

            return threads.MoveToImmutable();
        }

        ImmutableArray<ClrAppDomain> IRuntimeHelpers.GetAppDomains(ClrRuntime runtime, out ClrAppDomain? system, out ClrAppDomain? shared)
        {
            CheckDisposed();

            system = null;
            shared = null;

            AppDomainBuilder builder = new AppDomainBuilder(_sos, this);

            if (builder.SystemDomain != 0)
                system = GetOrCreateAppDomain(builder, builder.SystemDomain);

            if (builder.SharedDomain != 0)
                shared = GetOrCreateAppDomain(builder, builder.SharedDomain);

            ClrDataAddress[] domainList = _sos.GetAppDomainList(builder.AppDomainCount);
            ImmutableArray<ClrAppDomain>.Builder result = ImmutableArray.CreateBuilder<ClrAppDomain>(domainList.Length);
            result.Count = result.Capacity;

            for (int i = 0; i < domainList.Length; i++)
                result[i] = GetOrCreateAppDomain(builder, domainList[i]);

            return result.MoveToImmutable();
        }

        public ClrAppDomain GetOrCreateAppDomain(AppDomainBuilder? builder, ulong domain)
        {
            CheckDisposed();

            lock (_domains)
            {
                if (_domains.TryGetValue(domain, out ClrAppDomain? result))
                    return result;

                builder ??= new AppDomainBuilder(_sos, this);

                if (builder.Init(domain))
                    return _domains[domain] = new ClrmdAppDomain(GetOrCreateRuntime(), builder);

                return _domains[domain] = new ClrmdAppDomain(GetOrCreateRuntime(), this, domain);
            }
        }

        IEnumerable<ClrHandle> IRuntimeHelpers.EnumerateHandleTable(ClrRuntime runtime)
        {
            CheckDisposed();

            // Yes this is a huge array.  Older versions of ISOSHandleEnum have a memory leak when
            // we loop below.  If we can fill the array without having to call back into
            // SOSHandleEnum.ReadHandles then we avoid that leak entirely.
            HandleData[] handles = new HandleData[0xc0000];
            return EnumerateHandleTable(runtime, handles);
        }

        IEnumerable<(ulong Source, ulong Target)> IHeapHelpers.EnumerateDependentHandleLinks()
        {
            CheckDisposed();

            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles(ClrHandleKind.Dependent);
            if (handleEnum is null)
                yield break;

            // See note above in EnumerateHandleTable about why this array is so large.
            HandleData[] handles = new HandleData[0x18000];
            int fetched;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    if (handles[i].Type == (int)ClrHandleKind.Dependent)
                    {
                        ulong obj = DataReader.ReadPointer(handles[i].Handle);
                        if (obj != 0)
                            yield return (obj, handles[i].Secondary);
                    }
                }
            }
        }

        private IEnumerable<ClrHandle> EnumerateHandleTable(ClrRuntime runtime, HandleData[] handles)
        {
            CheckDisposed();

            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles();
            if (handleEnum is null)
                yield break;

            ClrHeap heap = runtime.Heap;
            ClrAppDomain? domain = heap.Runtime.AppDomains.Length > 0 ? heap.Runtime.AppDomains[0] : null;

            int fetched;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    ulong objAddress = DataReader.ReadPointer(handles[i].Handle);
                    ClrObject clrObj = heap.GetObject(objAddress);

                    if (!clrObj.IsNull)
                    {
                        if (domain == null || domain.Address != handles[i].AppDomain)
                            domain = GetOrCreateAppDomain(null, handles[i].AppDomain);

                        ClrHandleKind handleKind = (ClrHandleKind)handles[i].Type;
                        switch (handleKind)
                        {
                            default:
                                yield return new ClrmdHandle(domain, handles[i].Handle, clrObj, handleKind);
                                break;

                            case ClrHandleKind.Dependent:
                                ClrObject dependent = heap.GetObject(handles[i].Secondary);
                                yield return new ClrmdDependentHandle(domain, handles[i].Handle, clrObj, dependent);
                                break;

                            case ClrHandleKind.RefCounted:
                                uint refCount = 0;

                                if (handles[i].IsPegged != 0)
                                    refCount = handles[i].JupiterRefCount;

                                if (refCount < handles[i].RefCount)
                                    refCount = handles[i].RefCount;

                                if (!clrObj.IsNull)
                                {
                                    ComCallableWrapper? ccw = clrObj.GetComCallableWrapper();
                                    if (ccw != null && refCount < ccw.RefCount)
                                    {
                                        refCount = (uint)ccw.RefCount;
                                    }
                                    else
                                    {
                                        RuntimeCallableWrapper? rcw = clrObj.GetRuntimeCallableWrapper();
                                        if (rcw != null && refCount < rcw.RefCount)
                                            refCount = (uint)rcw.RefCount;
                                    }
                                }

                                yield return new ClrmdRefCountedHandle(domain, handles[i].Handle, clrObj, refCount);
                                break;
                        }
                    }
                }
            }
        }

        public IEnumerable<ClrJitManager> EnumerateClrJitManagers()
        {
            JitManagerHelpers helpers = new(_sos, _sos13);
            foreach (JitManagerInfo jitMgr in _sos.GetJitManagers())
                yield return new ClrJitManager(_runtime, jitMgr, helpers);
        }

        void IRuntimeHelpers.FlushCachedData()
        {
            FlushDac();

            _stringReader = null;
            Interlocked.Exchange(ref _heap, null);

            lock (_domains)
                _domains.Clear();

            lock (_modules)
            {
                _modules.Clear();

                _moduleBuilder = new ModuleBuilder(this, _sos);
            }

            if (_runtime is ClrmdRuntime runtime)
                lock (runtime)
                    runtime.Initialize();
        }

        private void FlushDac()
        {
            // IXClrDataProcess::Flush is unfortunately not wrapped with DAC_ENTER.  This means that
            // when it starts deleting memory, it's completely unsynchronized with parallel reads
            // and writes, leading to heap corruption and other issues.  This means that in order to
            // properly clear dac data structures, we need to trick the dac into entering the critical
            // section for us so we can call Flush safely then.

            // To accomplish this, we set a hook in our implementation of IDacDataTarget::ReadVirtual
            // which will call IXClrDataProcess::Flush if the dac tries to read the address set by
            // MagicCallbackConstant.  Additionally we make sure this doesn't interfere with other
            // reads by 1) Ensuring that the address is in kernel space, 2) only calling when we've
            // entered a special context.

            _library.DacDataTarget.EnterMagicCallbackContext();
            try
            {
                _sos.GetWorkRequestData(DacDataTarget.MagicCallbackConstant, out _);
            }
            finally
            {
                _library.DacDataTarget.ExitMagicCallbackContext();
            }
        }

        ulong IRuntimeHelpers.GetMethodDesc(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (!_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    return 0;

                if ((md = codeHeaderData.MethodDesc) == 0)
                    return 0;
            }

            return md;
        }
        string? IRuntimeHelpers.GetJitHelperFunctionName(ulong ip) => _sos.GetJitHelperFunctionName(ip);

        IEnumerable<ClrModule> IAppDomainHelpers.EnumerateModules(ClrAppDomain domain)
        {
            CheckDisposed();

            foreach (ulong assembly in _sos.GetAssemblyList(domain.Address))
                foreach (ulong module in _sos.GetModuleList(assembly))
                    yield return GetOrCreateModule(domain, module);
        }


        // When searching for a type, we don't want to actually cache or intern the name until we completely
        // construct the type.  This will alleviate a lot of needless memory usage when we do something like
        // search all modules for a named type we never find.
        string? IModuleHelpers.GetTypeName(ulong mt) => DACNameParser.Parse(_sos.GetMethodTableName(mt));
        (ulong MethodTable, int Token)[] IModuleHelpers.GetSortedTypeDefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable);
        (ulong MethodTable, int Token)[] IModuleHelpers.GetSortedTypeRefMap(ClrModule module) => GetSortedMap(module, SOSDac.ModuleMapTraverseKind.TypeRefToMethodTable);

        private (ulong MethodTable, int Token)[] GetSortedMap(ClrModule module, SOSDac.ModuleMapTraverseKind kind)
        {
            CheckDisposed();

            List<(ulong MethodTable, int Token)> result = new List<(ulong MethodTable, int Token)>();
            uint lastToken = 0;
            bool sorted = true;
            _sos.TraverseModuleMap(kind, module.Address, (token, mt, _) =>
            {
                result.Add((mt, token));
                if (sorted && lastToken > token)
                    sorted = false;
            });

            if (!sorted)
                result.Sort((x, y) => x.Token.CompareTo(y.Token));

            return result.ToArray();
        }

        public ClrRuntime GetOrCreateRuntime() => _runtime;

        public ClrHeap GetOrCreateHeap()
        {
            ClrHeap? heap = _heap;
            if (heap is not null)
                return heap;

            heap = new(GetOrCreateRuntime(), DataReader, new ClrHeapHelpers(_sos, _sos8, _sos12, DataReader));

            // We can race with Flush.
            while (Interlocked.CompareExchange(ref _heap, heap, null) != null)
            {
                ClrHeap? newHeap = _heap;
                if (newHeap is not null)
                    return newHeap;
            }
            
            return heap;
        }

        public MetadataImport? GetMetadataImport(ClrModule module) => _sos.GetMetadataImport(module.Address);


    }
}