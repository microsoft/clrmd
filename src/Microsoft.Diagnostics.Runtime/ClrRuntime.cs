// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacImplementation;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a single runtime in a target process or crash dump.  This serves as the primary
    /// entry point for getting diagnostic information.
    /// </summary>
    public sealed class ClrRuntime : IClrRuntime
    {
        private readonly IServiceProvider _services;
        private volatile ClrHeap? _heap;
        private ImmutableArray<ClrThread> _threads;
        private volatile DomainAndModules? _domainAndModules;

        private IAbstractRuntime? _runtime;
        private IAbstractComHelpers? _comHelpers;
        private IAbstractMethodLocator? _methodLocator;
        private IAbstractDacController? _controller;

        internal ClrRuntime(ClrInfo clrInfo, IServiceProvider services)
        {
            ClrInfo = clrInfo;
            DataTarget = clrInfo.DataTarget;
            _services = services;
        }

        /// <summary>
        /// Flushes the DAC cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.  After calling this function, you must discard ALL ClrMD objects
        /// you have cached other than DataTarget and ClrRuntime and re-request the objects and data you need.
        /// (e.g. if you want to use the ClrHeap object after calling flush, you must call ClrRuntime.GetHeap
        /// again after Flush to get a new instance.)
        /// </summary>
        public void FlushCachedData()
        {
            _controller ??= GetService<IAbstractDacController>();
            if (_controller is null || !_controller.CanFlush)
                 throw new NotSupportedException($"This version of CLR debugging does not support flushing the runtime.");

            _controller.Flush();
            _domainAndModules = null;
            _threads = default;
            _heap = null;
        }

        /// <summary>
        /// Returns whether this version of CLR diagnostics supports flushing data.
        /// </summary>
        public bool CanFlushData => GetService<IAbstractDacController>() is not null;

        internal T? GetService<T>() where T: class => (T?)_services.GetService(typeof(T));
        internal T GetServiceOrThrow<T>() where T : class => (T?)_services.GetService(typeof(T)) ?? throw new NotSupportedException($"This version of ClrMD does not support {typeof(T).FullName}");

        private IAbstractRuntime GetDacRuntime() => _runtime ??= (IAbstractRuntime?)_services.GetService(typeof(IAbstractRuntime)) ?? throw new NotSupportedException($"Could not construct an {nameof(IAbstractRuntime)} for this runtime.");

        private IAbstractComHelpers? TryGetComHelpers() => _comHelpers ??= GetServiceOrThrow<IAbstractComHelpers>();

        private IAbstractMethodLocator? TryGetMethodLocator() => _methodLocator ??= GetService<IAbstractMethodLocator>();

        /// <summary>
        /// Gets the <see cref="ClrInfo"/> of the current runtime.
        /// </summary>
        public ClrInfo ClrInfo { get; }

        /// <summary>
        /// Gets the <see cref="DataTarget"/> associated with this runtime.
        /// </summary>
        public DataTarget DataTarget { get; }

        /// <summary>
        /// Returns whether you are allowed to call into the transitive closure of ClrMD objects created from
        /// this runtime on multiple threads.
        /// </summary>
        public bool IsThreadSafe => DataTarget.DataReader.IsThreadSafe;

        /// <summary>
        /// Gets the list of appdomains in the process.
        /// </summary>
        public ImmutableArray<ClrAppDomain> AppDomains => GetAppDomainData().AppDomains;

        /// <summary>
        /// Gets the System AppDomain for Desktop CLR (<see langword="null"/> on .NET Core).
        /// </summary>
        public ClrAppDomain? SystemDomain => GetAppDomainData().SystemDomain;

        /// <summary>
        /// Gets the Shared AppDomain for Desktop CLR (<see langword="null"/> on .NET Core).
        /// </summary>
        public ClrAppDomain? SharedDomain => GetAppDomainData().SharedDomain;

        public ClrModule BaseClassLibrary => GetAppDomainData().BaseClassLibrary!;

        /// <summary>
        /// Gets information about CLR's ThreadPool.  May return null if we could not obtain
        /// ThreadPool data from the target process or dump.
        /// </summary>
        public ClrThreadPool? ThreadPool
        {
            get
            {
                ClrThreadPool result = new(this, GetService<IAbstractLegacyThreadPool>());
                return result.Initialized ? result : null;
            }
        }

        /// <summary>
        /// Gets all managed threads in the process.  Only threads which have previously run managed
        /// code will be enumerated.
        /// </summary>
        public ImmutableArray<ClrThread> Threads
        {
            get
            {
                if (!_threads.IsDefault)
                    return _threads;

                IAbstractThreadHelpers? threadHelpers = GetService<IAbstractThreadHelpers>();
                ImmutableArray<ClrThread>.Builder builder = ImmutableArray.CreateBuilder<ClrThread>();

                int max = 20000;
                foreach (ClrThreadInfo data in GetDacRuntime().EnumerateThreads())
                {
                    builder.Add(new ClrThread(DataTarget.DataReader, this, threadHelpers, data));

                    if (max-- == 0)
                        break;
                }

                ImmutableArray<ClrThread> threads = builder.MoveOrCopyToImmutable();
                ImmutableInterlocked.InterlockedCompareExchange(ref _threads, threads, _threads);

                return _threads;
            }
        }

        /// <summary>
        /// Returns a ClrAppDomain by its address.
        /// </summary>
        /// <param name="appDomain">The address of an AppDomain.  This is the pointer to CLR's internal runtime
        /// structure.</param>
        /// <returns>The ClrAppDomain corresponding to this address, or null if none were found.</returns>
        public ClrAppDomain? GetAppDomainByAddress(ulong appDomain) => GetAppDomainData().GetDomainByAddress(appDomain);

        /// <summary>
        /// Returns a ClrMethod by its internal runtime handle (on desktop CLR this is a MethodDesc).
        /// </summary>
        /// <param name="methodHandle">The method handle (MethodDesc) to look up.</param>
        /// <returns>The ClrMethod for the given method handle, or <see langword="null"/> if no method was found.</returns>
        public ClrMethod? GetMethodByHandle(ulong methodHandle)
        {
            if (methodHandle == 0)
                return null;

            IAbstractMethodLocator? locator = TryGetMethodLocator();
            if (locator is null)
                return null;

            if (!locator.GetMethodInfo(methodHandle, out MethodInfo mi))
                return null;

            ulong mt = locator.GetMethodHandleContainingType(methodHandle);
            if (mt == 0)
                return null;

            ClrType? type = Heap.GetTypeByMethodTable(mt);
            if (type is null)
                return null;

            return type.GetOrCreateMethod(mi);
        }

        /// <summary>
        /// Gets the <see cref="ClrType"/> corresponding to the given MethodTable.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or <see langword="null"/> if no such type exists.</returns>
        public ClrType? GetTypeByMethodTable(ulong methodTable) => Heap.GetTypeByMethodTable(methodTable);

        /// <summary>
        /// Enumerates a list of GC handles currently in the process.  Note that this list may be incomplete
        /// depending on the state of the process when we attempt to walk the handle table.
        /// </summary>
        /// <returns>An enumeration of GC handles in the process.</returns>
        public IEnumerable<ClrHandle> EnumerateHandles() => GetDacRuntime().EnumerateHandles().Select(r => new ClrHandle(this, r));

        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        public ClrHeap Heap
        {
            get
            {
                ClrHeap? heap = _heap;
                while (heap is null) // Flush can cause a race.
                {
                    IAbstractHeap? heapHelpers = GetService<IAbstractHeap>();
                    IAbstractTypeHelpers? typeHelpers = GetService<IAbstractTypeHelpers>();

                    // These are defined as non-nullable but just in case, double check we have a non-null instance.
                    if (heapHelpers is null || typeHelpers is null)
                        throw new NotSupportedException("Unable to create a ClrHeap for this runtime.");

                    heap = new(this, DataTarget.DataReader, heapHelpers, typeHelpers);
                    Interlocked.CompareExchange(ref _heap, heap, null);
                    heap = _heap;
                }

                return heap;
            }
        }

        IClrThreadPool? IClrRuntime.ThreadPool => ThreadPool;

        IClrHeap IClrRuntime.Heap => Heap;

        ImmutableArray<IClrAppDomain> IClrRuntime.AppDomains => AppDomains.CastArray<IClrAppDomain>();

        IClrAppDomain? IClrRuntime.SharedDomain => SharedDomain;

        IClrAppDomain? IClrRuntime.SystemDomain => SystemDomain;

        ImmutableArray<IClrThread> IClrRuntime.Threads => Threads.CastArray<IClrThread>();

        IClrInfo IClrRuntime.ClrInfo => ClrInfo;

        IDataTarget IClrRuntime.DataTarget => DataTarget;

        IClrModule IClrRuntime.BaseClassLibrary => BaseClassLibrary;

        /// <summary>
        /// Attempts to get a ClrMethod for the given instruction pointer.  This will return NULL if the
        /// given instruction pointer is not within any managed method.
        /// </summary>
        public ClrMethod? GetMethodByInstructionPointer(ulong ip)
        {
            IAbstractMethodLocator? locator = TryGetMethodLocator();
            if (locator is null)
                return null;

            ulong md = locator.GetMethodHandleByInstructionPointer(ip);
            return GetMethodByHandle(md);
        }

        /// <summary>
        /// Enumerate all managed modules in the runtime.
        /// </summary>
        public IEnumerable<ClrModule> EnumerateModules() => GetAppDomainData().Modules;

        /// <summary>
        /// Enumerates all native heaps that CLR has allocated.  This method is used to give insights into
        /// what native memory ranges are owned by CLR.  For example, this is the information enumerated
        /// by SOS's !eeheap and "!ext maddress".
        /// </summary>
        /// <returns>An enumeration of heaps.</returns>
        public IEnumerable<ClrNativeHeapInfo> EnumerateClrNativeHeaps()
        {
            // Enumerate the JIT code heaps.
            foreach (ClrJitManager jitMgr in EnumerateJitManagers())
                foreach (ClrNativeHeapInfo heap in jitMgr.EnumerateNativeHeaps())
                    yield return heap;

            HashSet<ulong> visited = new();

            // Ensure we are working on a consistent set of domains/modules
            DomainAndModules domainData = GetAppDomainData();

            // Walk domains
            if (domainData.SystemDomain is not null)
            {
                visited.Add(domainData.SystemDomain.LoaderAllocator);
                foreach (ClrNativeHeapInfo heap in domainData.SystemDomain.EnumerateLoaderAllocatorHeaps())
                    yield return heap;
            }

            if (domainData.SharedDomain is not null)
            {
                visited.Add(domainData.SharedDomain.LoaderAllocator);
                foreach (ClrNativeHeapInfo heap in domainData.SharedDomain.EnumerateLoaderAllocatorHeaps())
                    yield return heap;
            }

            foreach (ClrAppDomain domain in domainData.AppDomains)
            {
                if (domain.LoaderAllocator == 0 || visited.Add(domain.LoaderAllocator))
                    foreach (ClrNativeHeapInfo heap in domain.EnumerateLoaderAllocatorHeaps())
                        yield return heap;
            }

            // Walk modules.  We do this after domains to ensure we don't enumerate
            // previously enumerated LoaderAllocators.
            foreach (ClrModule module in domainData.Modules)
            {
                // We don't want to skip modules with no address, as we might have
                // multiple of those with unique heaps.
                if (module.Address == 0 || visited.Add(module.Address))
                {
                    if (module.ThunkHeap != 0 && visited.Add(module.ThunkHeap))
                        foreach (ClrNativeHeapInfo heap in module.EnumerateThunkHeap())
                            yield return heap;

                    // LoaderAllocator may be shared with its parent domain.  We only have a
                    // unique LoaderAllocator in the case of collectable assemblies.
                    if (module.LoaderAllocator != 0 && visited.Add(module.LoaderAllocator))
                        foreach (ClrNativeHeapInfo heap in module.EnumerateLoaderAllocatorHeaps())
                            yield return heap;
                }
            }

            IAbstractClrNativeHeaps? nativeHeaps = GetService<IAbstractClrNativeHeaps>();
            if (nativeHeaps is not null)
            {
                foreach (ClrNativeHeapInfo gcFreeRegion in nativeHeaps.EnumerateGCFreeRegions())
                {
                    yield return gcFreeRegion;
                }

                foreach (ClrNativeHeapInfo handleHeap in nativeHeaps.EnumerateHandleTableRegions())
                {
                    yield return handleHeap;
                }

                foreach (ClrNativeHeapInfo bkRegions in nativeHeaps.EnumerateGCBookkeepingRegions())
                {
                    yield return bkRegions;
                }
            }
        }

        public IEnumerable<ClrSyncBlockCleanupData> EnumerateSyncBlockCleanupData()
        {
            IAbstractClrNativeHeaps? nativeHeaps = GetService<IAbstractClrNativeHeaps>();
            if (nativeHeaps is not null)
                return nativeHeaps.EnumerateSyncBlockCleanupData();

            return Enumerable.Empty<ClrSyncBlockCleanupData>();
        }

        public IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData()
        {
            IAbstractComHelpers? helpers = TryGetComHelpers();
            if (helpers is not null)
                return helpers.EnumerateRcwCleanupData();

            return Enumerable.Empty<ClrRcwCleanupData>();
        }

        internal RuntimeCallableWrapper? CreateRCWForObject(ulong obj)
        {
            IAbstractComHelpers? helpers = TryGetComHelpers();
            if (helpers is not null && helpers.GetRcwInfo(obj, out RcwInfo info))
                return new(this, info);

            return null;
        }

        internal ComCallableWrapper? CreateCCWForObject(ulong obj)
        {
            IAbstractComHelpers? helpers = TryGetComHelpers();
            if (helpers is not null && helpers.GetCcwInfo(obj, out CcwInfo info))
                return new(this, info);

            return null;
        }

        /// <summary>
        /// Enumerates native heaps that the JIT has allocated.
        /// </summary>
        /// <returns>An enumeration of heaps.</returns>
        public IEnumerable<ClrJitManager> EnumerateJitManagers()
        {
            return GetDacRuntime().EnumerateClrJitManagers().Select(info => new ClrJitManager(this, info, GetService<IAbstractClrNativeHeaps>()));
        }

        /// <summary>
        /// Gets the name of a JIT helper function.
        /// </summary>
        /// <param name="address">Address of a possible JIT helper function.</param>
        /// <returns>The name of the JIT helper function or <see langword="null"/> if <paramref name="address"/> isn't a JIT helper function.</returns>
        public string? GetJitHelperFunctionName(ulong address) => GetDacRuntime().GetJitHelperFunctionName(address);

        /// <summary>
        /// Cleans up all resources and releases them.  You may not use this ClrRuntime or any object it transitively
        /// created after calling this method.
        /// </summary>
        public void Dispose()
        {
            FlushCachedData();
            if (_services is IDisposable disposable)
                disposable.Dispose();
        }

        private DomainAndModules GetAppDomainData()
        {
            DomainAndModules? data = _domainAndModules;
            if (data is null)
            {
                data = InitAppDomainData();
                _domainAndModules = data;
            }

            return data;
        }

        private DomainAndModules InitAppDomainData()
        {
            Dictionary<ulong, ClrModule> modules = new();
            string bclName = ClrInfo.Flavor == ClrFlavor.Core ? "SYSTEM.PRIVATE.CORELIB" : "MSCORLIB";

            ClrAppDomain? system = null, shared = null;
            ClrModule? bcl = null;

            ImmutableArray<ClrAppDomain>.Builder builder = ImmutableArray.CreateBuilder<ClrAppDomain>();
            foreach (AppDomainInfo domainInfo in GetDacRuntime().EnumerateAppDomains())
            {
                ClrAppDomain domain = new(this, domainInfo, GetService<IAbstractClrNativeHeaps>());

                switch (domainInfo.Kind)
                {
                    case AppDomainKind.Normal:
                        builder.Add(domain);
                        break;

                    case AppDomainKind.System:
                        system = domain;
                        break;

                    case AppDomainKind.Shared:
                        shared = domain;
                        break;

                    default:
                        throw new InvalidDataException($"Unknown domain kind: {domainInfo.Kind}");
                }

                IAbstractModuleHelpers moduleHelpers = GetServiceOrThrow<IAbstractModuleHelpers>();
                IAbstractClrNativeHeaps? nativeHeaps = GetService<IAbstractClrNativeHeaps>();
                ImmutableArray<ClrModule>.Builder moduleBuilder = ImmutableArray.CreateBuilder<ClrModule>();
                foreach (ulong moduleAddress in GetDacRuntime().GetModuleList(domain.Address))
                {
                    if (!modules.TryGetValue(moduleAddress, out ClrModule? module))
                    {
                        module = new(domain, moduleAddress, moduleHelpers, nativeHeaps, DataTarget.DataReader);
                        modules.Add(moduleAddress, module);
                    }

                    moduleBuilder.Add(module);
                    if (bcl is null && module.Name is not null)
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(module.Name);
                            if (fileName.Equals(bclName, StringComparison.OrdinalIgnoreCase))
                                bcl = module;
                        }
                        catch
                        {
                        }
                    }
                }

                domain.Modules = moduleBuilder.MoveOrCopyToImmutable();
            }

            return new(system, shared, builder.MoveOrCopyToImmutable(), modules.Values.ToArray(), bcl);
        }

        private sealed class DomainAndModules
        {
            public ClrAppDomain? SystemDomain { get; }
            public ClrAppDomain? SharedDomain { get; }
            public ImmutableArray<ClrAppDomain> AppDomains { get; }
            public ReadOnlyCollection<ClrModule> Modules { get; }
            public ClrModule? BaseClassLibrary { get; }

            internal ClrAppDomain? GetDomainByAddress(ulong address)
            {
                if (SystemDomain is not null && SystemDomain.Address == address)
                    return SystemDomain;

                if (SharedDomain is not null && SharedDomain.Address == address)
                    return SharedDomain;

                return AppDomains.FirstOrDefault(x => x.Address == address);
            }

            public DomainAndModules(ClrAppDomain? system, ClrAppDomain? shared, ImmutableArray<ClrAppDomain> domains, ClrModule[] modules, ClrModule? bcl)
            {
                SystemDomain = system;
                SharedDomain = shared;
                AppDomains = domains;
                Modules = Array.AsReadOnly(modules.ToArray());
                BaseClassLibrary = bcl;
            }
        }


        IEnumerable<IClrRoot> IClrRuntime.EnumerateHandles() => EnumerateHandles().Cast<IClrRoot>();

        IEnumerable<IClrJitManager> IClrRuntime.EnumerateJitManagers() => EnumerateJitManagers().Cast<IClrJitManager>();

        IEnumerable<IClrModule> IClrRuntime.EnumerateModules() => EnumerateModules().Cast<IClrModule>();

        IClrMethod? IClrRuntime.GetMethodByHandle(ulong methodHandle) => GetMethodByHandle(methodHandle);

        IClrMethod? IClrRuntime.GetMethodByInstructionPointer(ulong ip) => GetMethodByInstructionPointer(ip);

        IClrType? IClrRuntime.GetTypeByMethodTable(ulong methodTable) => GetTypeByMethodTable(methodTable);
    }
}