// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed unsafe class RuntimeBuilder : IRuntimeHelpers, ITypeFactory, ITypeHelpers, IModuleHelpers, IMethodHelpers, IClrObjectHelpers, IFieldHelpers,
                                         IAppDomainHelpers, IThreadHelpers, IExceptionHelpers, IHeapHelpers
    {
        private bool _disposed;
        private readonly ClrInfo _clrInfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly CacheOptions _options;
        private readonly SOSDac6? _sos6;
        private readonly int _threads;
        private readonly ulong _finalizer;
        private readonly ulong _firstThread;

        private volatile ClrType?[]? _basicTypes;
        private readonly Dictionary<ulong, ClrAppDomain> _domains = new Dictionary<ulong, ClrAppDomain>();
        private readonly Dictionary<ulong, ClrModule> _modules = new Dictionary<ulong, ClrModule>();

        private readonly ClrmdRuntime _runtime;
        private readonly ClrmdHeap _heap;

        private volatile StringReader? _stringReader;

        private readonly Dictionary<ulong, ClrType> _types = new Dictionary<ulong, ClrType>();

        private readonly ObjectPool<TypeBuilder> _typeBuilders;
        private readonly ObjectPool<MethodBuilder> _methodBuilders;
        private readonly ObjectPool<FieldBuilder> _fieldBuilders;
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

            _typeBuilders = new ObjectPool<TypeBuilder>((owner, obj) => obj.Owner = owner);
            _methodBuilders = new ObjectPool<MethodBuilder>((owner, obj) => obj.Owner = owner);
            _fieldBuilders = new ObjectPool<FieldBuilder>((owner, obj) => obj.Owner = owner);

            _moduleBuilder = new ModuleBuilder(this, _sos);

            _runtime = new ClrmdRuntime(clr, library, this);
            _runtime.Initialize();

            _heap = new ClrmdHeap(_runtime, new HeapBuilder(this, _sos));

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
                _library.Dispose();
            }
        }

        bool IHeapHelpers.CreateSegments(ClrHeap clrHeap, out ImmutableArray<ClrSegment> segments, out ImmutableArray<MemoryRange> allocationContexts,
                                         out ImmutableArray<FinalizerQueueSegment> fqRoots, out ImmutableArray<FinalizerQueueSegment> fqObjects)
        {
            var segs = ImmutableArray.CreateBuilder<ClrSegment>();
            var allocContexts = ImmutableArray.CreateBuilder<MemoryRange>();
            var finalizerRoots = ImmutableArray.CreateBuilder<FinalizerQueueSegment>();
            var finalizerObjects = ImmutableArray.CreateBuilder<FinalizerQueueSegment>();

            ulong next = _firstThread;
            HashSet<ulong> seen = new HashSet<ulong>() { next };  // Ensure we don't hit an infinite loop
            while (_sos.GetThreadData(next, out ThreadData thread))
            {
                if (thread.AllocationContextPointer != 0 && thread.AllocationContextPointer != thread.AllocationContextLimit)
                    allocContexts.Add(new MemoryRange(thread.AllocationContextPointer, thread.AllocationContextLimit));

                next = thread.NextThread;
                if (next == 0 || !seen.Add(next))
                    break;
            }

            bool result = false;
            SegmentBuilder segBuilder = new SegmentBuilder(_sos);
            if (clrHeap.IsServer)
            {
                ClrDataAddress[] heapList = _sos.GetHeapList(clrHeap.LogicalHeapCount);
                for (int i = 0; i < heapList.Length; i++)
                {
                    segBuilder.LogicalHeap = i;
                    if (_sos.GetServerHeapDetails(heapList[i], out HeapDetails heap))
                    {
                        // As long as we got at least one heap we'll count that as success
                        result = true;
                        ProcessHeap(segBuilder, clrHeap, heap, allocContexts, segs, finalizerRoots, finalizerObjects);
                    }
                }
            }
            else if (_sos.GetWksHeapDetails(out HeapDetails heap))
            {
                ProcessHeap(segBuilder, clrHeap, heap, allocContexts, segs, finalizerRoots, finalizerObjects);
                result = true;
            }

            segs.Sort((x, y) => x.Start.CompareTo(y.Start));

            allocationContexts = allocContexts.ToImmutable();
            fqRoots = finalizerRoots.ToImmutable();
            fqObjects = finalizerObjects.ToImmutable();
            segments = segs.ToImmutable();
            return result;
        }

        private void ProcessHeap(
            SegmentBuilder segBuilder,
            ClrHeap clrHeap,
            in HeapDetails heap,
            ImmutableArray<MemoryRange>.Builder allocationContexts,
            ImmutableArray<ClrSegment>.Builder segments,
            ImmutableArray<FinalizerQueueSegment>.Builder fqRoots,
            ImmutableArray<FinalizerQueueSegment>.Builder fqObjects)
        {
            if (heap.EphemeralAllocContextPtr != 0 && heap.EphemeralAllocContextPtr != heap.EphemeralAllocContextLimit)
                allocationContexts.Add(new MemoryRange(heap.EphemeralAllocContextPtr, heap.EphemeralAllocContextLimit));

            fqRoots.Add(new FinalizerQueueSegment(heap.FQRootsStart, heap.FQRootsStop));
            fqObjects.Add(new FinalizerQueueSegment(heap.FQAllObjectsStart, heap.FQAllObjectsStop));

            AddSegments(segBuilder, clrHeap, large: true, heap, segments, heap.GenerationTable[3].StartSegment);
            AddSegments(segBuilder, clrHeap, large: false, heap, segments, heap.GenerationTable[2].StartSegment);
        }

        private void AddSegments(SegmentBuilder segBuilder, ClrHeap clrHeap, bool large, in HeapDetails heap, ImmutableArray<ClrSegment>.Builder segments, ulong address)
        {
            HashSet<ulong> seenSegments = new HashSet<ulong> { 0 };
            segBuilder.IsLargeObjectSegment = large;

            while (seenSegments.Add(address) && segBuilder.Initialize(address, heap))
            {
                // Unfortunately ClrmdSegment is tightly coupled to ClrmdHeap to make implementation vastly simpler and it can't
                // be used with any generic ClrHeap.  There should be no way that this runtime builder ever mismatches the two
                // so this cast will always succeed.
                segments.Add(new ClrmdSegment((ClrmdHeap)clrHeap, this, segBuilder));
                address = segBuilder.Next;
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

        IEnumerable<IClrStackRoot> IThreadHelpers.EnumerateStackRoots(ClrThread thread)
        {
            CheckDisposed();

            using SOSStackRefEnum? stackRefEnum = _sos.EnumerateStackRefs(thread.OSThreadId);
            if (stackRefEnum is null)
                yield break;

            ClrStackFrame[] stack = thread.EnumerateStackTrace().Take(2048).ToArray();

            ClrAppDomain domain = thread.CurrentAppDomain;
            ClrHeap heap = thread.Runtime?.Heap ?? GetOrCreateHeap();
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

                    ClrStackFrame? frame = stack.SingleOrDefault(f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);
                    frame ??= new ClrmdStackFrame(thread, null, refs[i].Source, refs[i].StackPointer, ClrStackFrameKind.Unknown, null, null);

                    if (interior)
                    {
                        // Check if the value lives on the heap.
                        ulong obj = refs[i].Object;
                        ClrSegment? segment = heap.GetSegmentByAddress(obj);

                        // If not, this may be a pointer to an object.
                        if (segment is null && DataReader.ReadPointer(obj, out obj))
                            segment = heap.GetSegmentByAddress(obj);

                        // Only yield return if we find a valid object on the heap
                        if (!(segment is null))
                            yield return new ClrStackInteriorRoot(segment, refs[i].Address, obj, frame, pinned);
                    }
                    else
                    {
                        // It's possible that heap.GetObjectType could return null and we construct a bad ClrObject, but this should
                        // only happen in the case of heap corruption and obj.IsValidObject will return null, so this is fine.
                        ClrObject obj = new ClrObject(refs[i].Object, heap.GetObjectType(refs[i].Object));
                        yield return new ClrStackRoot(refs[i].Address, obj, frame, pinned);
                    }
                }
            }
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
            else // Architecture.Amd64
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
                    if (!(module.Name is null) && module.Name.ToUpperInvariant().Contains(moduleName))
                        return module;

            foreach (ClrAppDomain domain in runtime.AppDomains)
                foreach (ClrModule module in domain.Modules)
                    if (!(module.Name is null) && module.Name.ToUpperInvariant().Contains(moduleName))
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

            // Enumerating handles should be sufficiently rare as to not need to use ArrayPool
            HandleData[] handles = new HandleData[128];
            return EnumerateHandleTable(runtime, handles);
        }

        IEnumerable<(ulong Source, ulong Target)> IHeapHelpers.EnumerateDependentHandleLinks()
        {
            CheckDisposed();

            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles(ClrHandleKind.Dependent);
            if (handleEnum is null)
                yield break;

            HandleData[] handles = new HandleData[32];
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
                                    ComCallableWrapper? ccw = clrObj.AsComCallableWrapper();
                                    if (ccw != null && refCount < ccw.RefCount)
                                    {
                                        refCount = (uint)ccw.RefCount;
                                    }
                                    else
                                    {
                                        RuntimeCallableWrapper? rcw = clrObj.AsRuntimeCallableWrapper();
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

        void IRuntimeHelpers.FlushCachedData()
        {
            FlushDac();

            _stringReader = null;
            _heap.ClearCachedData();

            lock (_types)
                _types.Clear();

            lock (_domains)
                _domains.Clear();

            _basicTypes = null;

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
                _sos.GetWorkRequestData(DacDataTargetWrapper.MagicCallbackConstant, out _);
            }
            finally
            {
                _library.DacDataTarget.ExitMagicCallbackContext();
            }
        }

        ulong IRuntimeHelpers.GetMethodDesc(ulong ip) => _sos.GetMethodDescPtrFromIP(ip);
        string? IRuntimeHelpers.GetJitHelperFunctionName(ulong ip) => _sos.GetJitHelperFunctionName(ip);
        
        public IExceptionHelpers ExceptionHelpers => this;

        uint IExceptionHelpers.GetInnerExceptionOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_innerException");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => DataReader.PointerSize switch
                {
                    4 => 0xc,
                    8 => 0x18,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => DataReader.PointerSize switch
                {
                    4 => 0x14,
                    8 => 0x28,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        uint IExceptionHelpers.GetHResultOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_HResult");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => DataReader.PointerSize switch
                {
                    4 => 0x38,
                    8 => 0x6c,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => DataReader.PointerSize switch
                {
                    4 => 0x3c,
                    8 => 0x84,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        uint IExceptionHelpers.GetMessageOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_message");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => DataReader.PointerSize switch
                {
                    4 => 4,
                    8 => 8,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => DataReader.PointerSize switch
                {
                    4 => 0xc,
                    8 => 0x18,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        private uint GetStackTraceOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_stackTrace");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => DataReader.PointerSize switch
                {
                    4 => 0x14,
                    8 => 0x28,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => DataReader.PointerSize switch
                {
                    4 => 0x1c,
                    8 => 0x38,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        ImmutableArray<ClrStackFrame> IExceptionHelpers.GetExceptionStackTrace(ClrThread? thread, ClrObject obj)
        {
            CheckDisposed();

            uint offset = GetStackTraceOffset(obj.Type);
            DebugOnly.Assert(offset != uint.MaxValue);
            if (offset == 0)
                return ImmutableArray<ClrStackFrame>.Empty;

            ulong address = DataReader.ReadPointer(obj.Address + offset);
            ClrObject _stackTrace = GetOrCreateHeap().GetObject(address);

            if (_stackTrace.IsNull)
                return ImmutableArray<ClrStackFrame>.Empty;

            int len = _stackTrace.AsArray().Length;
            if (len == 0)
                return ImmutableArray<ClrStackFrame>.Empty;

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!DataReader.ReadPointer(dataPtr, out ulong count))
                return ImmutableArray<ClrStackFrame>.Empty;

            ImmutableArray<ClrStackFrame>.Builder result = ImmutableArray.CreateBuilder<ClrStackFrame>((int)count);
            result.Count = result.Capacity;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            for (int i = 0; i < (int)count; ++i)
            {
                ulong ip = DataReader.ReadPointer(dataPtr);
                ulong sp = DataReader.ReadPointer(dataPtr + (ulong)IntPtr.Size);
                ulong md = DataReader.ReadPointer(dataPtr + (ulong)IntPtr.Size + (ulong)IntPtr.Size);

                ClrMethod? method = CreateMethodFromHandle(md);
                result[i] = new ClrmdStackFrame(thread, null, ip, sp, ClrStackFrameKind.ManagedMethod, method, frameName: null);
                dataPtr += (ulong)elementSize;
            }

            return result.MoveToImmutable();
        }

        string? IAppDomainHelpers.GetConfigFile(ClrAppDomain domain) => _sos.GetConfigFile(domain.Address);
        string? IAppDomainHelpers.GetApplicationBase(ClrAppDomain domain) => _sos.GetAppBase(domain.Address);
        IEnumerable<ClrModule> IAppDomainHelpers.EnumerateModules(ClrAppDomain domain)
        {
            CheckDisposed();

            foreach (ulong assembly in _sos.GetAssemblyList(domain.Address))
                foreach (ulong module in _sos.GetModuleList(assembly))
                    yield return GetOrCreateModule(domain, module);
        }

        public ClrType? TryGetType(ulong mt)
        {
            lock (_types)
            {
                _types.TryGetValue(mt, out ClrType? result);
                return result;
            }
        }

        // When searching for a type, we don't want to actually cache or intern the name until we completely
        // construct the type.  This will alleviate a lot of needless memory usage when we do something like
        // search all modules for a named type we never find.
        string? IModuleHelpers.GetTypeName(ulong mt) => FixGenerics(_sos.GetMethodTableName(mt));
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

        public ClrHeap GetOrCreateHeap() => _heap;

        public ClrType GetOrCreateBasicType(ClrElementType basicType)
        {
            CheckDisposed();

            ClrType?[]? basicTypes = _basicTypes;
            if (basicTypes is null)
            {
                basicTypes = new ClrType[(int)ClrElementType.SZArray];
                int count = 0;
                ClrModule bcl = GetOrCreateRuntime().BaseClassLibrary;
                if (bcl != null && bcl.MetadataImport != null)
                {
                    foreach ((ulong mt, int _) in bcl.EnumerateTypeDefToMethodTableMap())
                    {
                        string? name = _sos.GetMethodTableName(mt);
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
            if (index < 0 || index > basicTypes.Length)
                throw new ArgumentException($"Cannot create type for ClrElementType {basicType}");

            ClrType? result = basicTypes[index];
            if (!(result is null))
                return result;

            return basicTypes[index] = new ClrmdPrimitiveType(this, GetOrCreateRuntime().BaseClassLibrary, GetOrCreateHeap(), basicType);
        }

        public ClrType? GetOrCreateType(ulong mt, ulong obj) => mt == 0 ? null : GetOrCreateType(GetOrCreateHeap(), mt, obj);

        public ClrType CreateSystemType(ClrHeap heap, ulong mt, string typeName)
        {
            using TypeBuilder typeData = _typeBuilders.Rent();
            if (!typeData.Init(_sos, mt, this))
                throw new InvalidDataException($"Could not create well known type '{typeName}' from MethodTable {mt:x}.");

            ClrType? baseType = null;

            if (typeData.ParentMethodTable != 0 && !_types.TryGetValue(typeData.ParentMethodTable, out baseType))
                throw new InvalidOperationException($"Base type for '{typeName}' was not pre-created from MethodTable {typeData.ParentMethodTable:x}.");

            ClrModule? module = GetModule(typeData.Module);
            ClrmdType result;
            if (typeData.ComponentSize == 0)
                result = new ClrmdType(heap, baseType, module, typeData, typeName);
            else
                result = new ClrmdArrayType(heap, baseType, module, typeData, typeName);

            // Regardless of caching options, we always cache important system types and basic types
            lock (_types)
                _types[mt] = result;

            return result;
        }

        public ClrType? GetOrCreateType(ClrHeap heap, ulong mt, ulong obj)
        {
            CheckDisposed();

            if (mt == 0)
                return null;

            {
                ClrType? result = TryGetType(mt);
                if (result != null)
                {
                    if (obj != 0 && result.ComponentType is null && result.IsArray && result is ClrmdArrayType type)
                        TryGetComponentType(type, obj);

                    return result;
                }
            }

            {
                using TypeBuilder typeData = _typeBuilders.Rent();
                if (!typeData.Init(_sos, mt, this))
                    return null;

                ClrType? baseType = GetOrCreateType(heap, typeData.ParentMethodTable, 0);

                ClrModule? module = GetModule(typeData.Module);
                if (typeData.ComponentSize == 0)
                {
                    ClrmdType result = new ClrmdType(heap, baseType, module, typeData);

                    if (_options.CacheTypes)
                    {
                        lock (_types)
                            _types[mt] = result;
                    }

                    return result;
                }
                else
                {
                    ClrmdArrayType result = new ClrmdArrayType(heap, baseType, module, typeData);

                    if (_options.CacheTypes)
                    {
                        lock (_types)
                            _types[mt] = result;
                    }

                    if (obj != 0 && result.IsArray && result.ComponentType is null)
                    {
                        TryGetComponentType(result, obj);
                    }

                    return result;
                }
            }
        }

        public ClrType? GetOrCreateTypeFromToken(ClrModule module, int token) => module.ResolveToken(token);

        public ClrType? GetOrCreateArrayType(ClrType innerType, int ranks) => innerType != null ? new ClrmdConstructedType(innerType, ranks, pointer: false) : null;
        public ClrType? GetOrCreatePointerType(ClrType innerType, int depth) => innerType != null ? new ClrmdConstructedType(innerType, depth, pointer: true) : null;

        private void TryGetComponentType(ClrmdArrayType type, ulong obj)
        {
            ClrType? result = null;
            if (_sos.GetObjectData(obj, out V45ObjectData data))
            {
                if (data.ElementTypeHandle != 0)
                    result = GetOrCreateType(data.ElementTypeHandle, 0);

                if (result is null && data.ElementType != 0)
                    result = GetOrCreateBasicType((ClrElementType)data.ElementType);

                type.SetComponentType(result);
            }
        }

        ComCallableWrapper? ITypeFactory.CreateCCWForObject(ulong obj)
        {
            CheckDisposed();

            CcwBuilder builder = new CcwBuilder(_sos, this);
            if (!builder.Init(obj))
                return null;

            return new ComCallableWrapper(builder);
        }

        RuntimeCallableWrapper? ITypeFactory.CreateRCWForObject(ulong obj)
        {
            CheckDisposed();

            RcwBuilder builder = new RcwBuilder(_sos, this);
            if (!builder.Init(obj))
                return null;

            return new RuntimeCallableWrapper(GetOrCreateRuntime(), builder);
        }

        bool ITypeFactory.CreateMethodsForType(ClrType type, out ImmutableArray<ClrMethod> methods)
        {
            CheckDisposed();

            ulong mt = type.MethodTable;
            if (!_sos.GetMethodTableData(mt, out MethodTableData data) || data.NumMethods == 0)
            {
                methods = ImmutableArray<ClrMethod>.Empty;
                return true;
            }

            using MethodBuilder builder = _methodBuilders.Rent();
            ImmutableArray<ClrMethod>.Builder result = ImmutableArray.CreateBuilder<ClrMethod>(data.NumMethods);
            result.Count = result.Capacity;

            int curr = 0;
            for (uint i = 0; i < data.NumMethods; i++)
            {
                if (builder.Init(_sos, mt, i, this))
                    result[curr++] = new ClrmdMethod(type, builder);
            }

            if (curr == 0)
            {
                methods = ImmutableArray<ClrMethod>.Empty;
                return true;
            }

            result.Capacity = result.Count = curr;
            methods = result.MoveToImmutable();
            return _options.CacheMethods;
        }

        public ClrMethod? CreateMethodFromHandle(ulong methodDesc)
        {
            CheckDisposed();

            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return null;

            ClrType? type = GetOrCreateType(mdData.MethodTable, 0);
            if (type is null)
                return null;

            ClrMethod? method = type.Methods.FirstOrDefault(m => m.MethodDesc == methodDesc);
            if (method != null)
                return method;

            using MethodBuilder builder = _methodBuilders.Rent();
            if (!builder.Init(_sos, methodDesc, this))
                return null;

            return new ClrmdMethod(type, builder);
        }

        bool ITypeFactory.CreateFieldsForType(ClrType type, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> statics)
        {
            CheckDisposed();

            CreateFieldsForMethodTableWorker(type, out fields, out statics);

            if (fields.IsDefault)
                fields = ImmutableArray<ClrInstanceField>.Empty;

            if (statics.IsDefault)
                statics = ImmutableArray<ClrStaticField>.Empty;

            return _options.CacheFields;
        }

        private void CreateFieldsForMethodTableWorker(ClrType type, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> statics)
        {
            CheckDisposed();

            fields = default;
            statics = default;

            if (type.IsFree)
                return;

            if (!_sos.GetFieldInfo(type.MethodTable, out V4FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
            {
                if (type.BaseType != null)
                    fields = type.BaseType.Fields;
                return;
            }

            ImmutableArray<ClrInstanceField>.Builder fieldsBuilder = ImmutableArray.CreateBuilder<ClrInstanceField>(fieldInfo.NumInstanceFields);
            fieldsBuilder.Count = fieldsBuilder.Capacity;

            ImmutableArray<ClrStaticField>.Builder staticsBuilder = ImmutableArray.CreateBuilder<ClrStaticField>(fieldInfo.NumStaticFields);
            staticsBuilder.Count = staticsBuilder.Capacity;

            if (fieldInfo.NumStaticFields == 0)
                statics = ImmutableArray<ClrStaticField>.Empty;

            int fieldNum = 0;
            int staticNum = 0;

            // Add base type's fields.
            if (type.BaseType != null)
            {
                ImmutableArray<ClrInstanceField> baseFields = type.BaseType.Fields;
                foreach (ClrInstanceField field in baseFields)
                    fieldsBuilder[fieldNum++] = field;
            }

            using FieldBuilder fieldData = _fieldBuilders.Rent();

            ulong nextField = fieldInfo.FirstFieldAddress;
            int other = 0;
            while (other + fieldNum + staticNum < fieldsBuilder.Capacity + staticsBuilder.Capacity && nextField != 0)
            {
                if (!fieldData.Init(_sos, nextField, this))
                    break;

                if (fieldData.IsContextLocal || fieldData.IsThreadLocal)
                {
                    other++;
                }
                else if (fieldData.IsStatic)
                {
                    staticsBuilder[staticNum++] = new ClrmdStaticField(type, fieldData);
                }
                else
                {
                    fieldsBuilder[fieldNum++] = new ClrmdField(type, fieldData);
                }

                nextField = fieldData.NextField;
            }

            fieldsBuilder.Capacity = fieldsBuilder.Count = fieldNum;
            staticsBuilder.Capacity = staticsBuilder.Count = staticNum;

            fieldsBuilder.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            fields = fieldsBuilder.MoveOrCopyToImmutable();
            statics = staticsBuilder.MoveOrCopyToImmutable();
        }

        public MetadataImport? GetMetadataImport(ClrModule module) => _sos.GetMetadataImport(module.Address);

        public ImmutableArray<ComInterfaceData> CreateComInterfaces(COMInterfacePointerData[] ifs)
        {
            CheckDisposed();

            ImmutableArray<ComInterfaceData>.Builder result = ImmutableArray.CreateBuilder<ComInterfaceData>(ifs.Length);
            result.Count = result.Capacity;

            for (int i = 0; i < ifs.Length; i++)
                result[i] = new ComInterfaceData(GetOrCreateType(ifs[i].MethodTable, 0), ifs[i].InterfacePointer);

            return result.MoveToImmutable();
        }

        bool IFieldHelpers.ReadProperties(ClrType type, int fieldToken, out string? name, out FieldAttributes attributes, out Utilities.SigParser sigParser)
        {
            CheckDisposed();

            MetadataImport? import = type?.Module?.MetadataImport;
            if (import is null || !import.GetFieldProps(fieldToken, out name, out attributes, out IntPtr fieldSig, out int sigLen, out _, out _))
            {
                name = null;
                attributes = default;
                sigParser = default;
                return false;
            }

            sigParser = new Utilities.SigParser(fieldSig, sigLen);
            return true;
        }

        ulong IFieldHelpers.GetStaticFieldAddress(ClrStaticField field, ClrAppDomain? appDomain)
        {
            CheckDisposed();

            if (appDomain is null)
                return 0;

            ClrType type = field.Parent;
            ClrModule? module = type.Module;
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

                if (!shared && !IsInitialized(dlmd, type.MetadataToken))
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

        private bool IsInitialized(in DomainLocalModuleData data, int token)
        {
            ulong flagsAddr = data.ClassData + (uint)(token & ~0x02000000u) - 1;
            if (!DataReader.Read(flagsAddr, out byte flags))
                return false;

            return (flags & 1) != 0;
        }

        bool ITypeHelpers.GetTypeName(ulong mt, out string? name)
        {
            name = _sos.GetMethodTableName(mt);
            if (string.IsNullOrWhiteSpace(name))
                return true;

            name = FixGenerics(name);
            if (_options.CacheTypeNames == StringCaching.Intern)
                name = string.Intern(name);

            return _options.CacheTypeNames != StringCaching.None;
        }

        private static void FixGenerics(StringBuilder result, string name, int start, int len, ref int maxDepth, out int finish)
        {
            if (--maxDepth < 0)
            {
                finish = 0;
                return;
            }

            int i = start;
            while (i < len)
            {
                if (name[i] == '`')
                    while (i < len && name[i] != '[')
                        i++;

                if (name[i] == ',')
                {
                    finish = i;
                    return;
                }

                if (name[i] == '[')
                {
                    int end = FindEnd(name, i);

                    if (IsArraySubstring(name, i, end))
                    {
                        result.Append(name, i, end - i + 1);
                        i = end + 1;
                    }
                    else
                    {
                        result.Append('<');

                        int curr = i;
                        do
                        {
                            FixGenerics(result, name, curr + 2, end - 1, ref maxDepth, out int currEnd);
                            if (maxDepth < 0)
                            {
                                finish = 0;
                                return;
                            }

                            curr = FindEnd(name, currEnd) + 1;

                            if (curr >= end)
                                break;

                            if (name[curr] == ',')
                                result.Append(", ");
                        }
                        while (curr < end);

                        result.Append('>');

                        i = curr + 1;
                    }
                }
                else
                {
                    result.Append(name[i]);
                    i++;
                }
            }

            finish = i;
        }

        private static int FindEnd(string name, int start)
        {
            int parenCount = 1;
            for (int i = start + 1; i < name.Length; i++)
            {
                if (name[i] == '[')
                    parenCount++;
                if (name[i] == ']' && --parenCount == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsArraySubstring(string name, int start, int end)
        {
            start++;
            end--;
            while (start < end)
                if (name[start++] != ',')
                    return false;

            return true;
        }

        [return: NotNullIfNotNull("name")]
        public static string? FixGenerics(string? name)
        {
            if (name == null || name.IndexOf("[[") == -1)
                return name;

            int maxDepth = 64;
            StringBuilder sb = new StringBuilder();
            FixGenerics(sb, name, 0, name.Length, ref maxDepth, out _);

            if (maxDepth < 0)
                return null;

            return sb.ToString();
        }

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


        string? IClrObjectHelpers.ReadString(ulong addr, int maxLength)
        {
            if (addr == 0)
                return null;

            StringReader? reader = _stringReader;
            if (reader == null)
            {
                reader = new StringReader(DataReader, GetOrCreateHeap().StringType);
                _stringReader = reader;
            }

            return reader.ReadString(DataReader, addr, maxLength);
        }

        bool IMethodHelpers.GetSignature(ulong methodDesc, out string? signature)
        {
            signature = _sos.GetMethodDescName(methodDesc);

            // Always cache an empty name, no reason to keep requesting it.
            // Implementations may ignore this (ClrmdMethod doesn't cache null signatures).
            if (string.IsNullOrWhiteSpace(signature))
                return true;

            if (_options.CacheMethodNames == StringCaching.Intern)
                signature = string.Intern(signature);

            return _options.CacheMethodNames != StringCaching.None;
        }

        ulong IMethodHelpers.GetILForModule(ulong address, uint rva) => _sos.GetILForModule(address, rva);

        ImmutableArray<ILToNativeMap> IMethodHelpers.GetILMap(ulong ip, in HotColdRegions hotColdInfo)
        {
            CheckDisposed();

            ImmutableArray<ILToNativeMap>.Builder result = ImmutableArray.CreateBuilder<ILToNativeMap>();

            foreach (ClrDataMethod method in _dac.EnumerateMethodInstancesByAddress(ip))
            {
                ILToNativeMap[]? map = method.GetILToNativeMap();
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

                    result.AddRange(map);
                }

                method.Dispose();
            }

            return result.MoveOrCopyToImmutable();
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