// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac13;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal sealed class DacNativeHeaps : IAbstractClrNativeHeaps
    {
        private NativeHeapKind[]? _heapNativeTypes;
        private readonly ClrInfo _clrInfo;
        private readonly SOSDac _sos;
        private readonly ISOSDac13? _sos13;
        private readonly IDataReader _dataReader;
        private readonly TargetProperties _target;

        public DacNativeHeaps(ClrInfo clrInfo, SOSDac sos, ISOSDac13? sos13, IDataReader dataReader, TargetProperties target)
        {
            _clrInfo = clrInfo;
            _sos = sos;
            _sos13 = sos13;
            _dataReader = dataReader;
            _target = target;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateGCFreeRegions()
        {
            List<ClrNativeHeapInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                using SosMemoryEnum? memoryEnum = _sos13?.GetGCFreeRegions();
                if (memoryEnum is null)
                    return Array.Empty<ClrNativeHeapInfo>();

                foreach (SosMemoryRegion mem in memoryEnum)
                {
                    NativeHeapKind kind = mem.ExtraData.ToAddress(_target) switch
                    {
                        1 => NativeHeapKind.GCFreeGlobalHugeRegion,
                        2 => NativeHeapKind.GCFreeGlobalRegion,
                        3 => NativeHeapKind.GCFreeRegion,
                        4 => NativeHeapKind.GCFreeSohSegment,
                        5 => NativeHeapKind.GCFreeUohSegment,
                        _ => NativeHeapKind.GCFreeRegion
                    };

                    ulong raw = mem.Start.ToAddress(_target);
                    ulong start = raw & ~0xfful;
                    ulong diff = raw - start;
                    ulong len = mem.Length.ToAddress(_target) + diff;

                    results ??= new();
                    results.Add(new ClrNativeHeapInfo(MemoryRange.CreateFromLength(start, len), kind, ClrNativeHeapState.Inactive, mem.Heap));
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateHandleTableRegions()
        {
            List<ClrNativeHeapInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                using SosMemoryEnum? memoryEnum = _sos13?.GetHandleTableRegions();
                if (memoryEnum is null)
                    return Array.Empty<ClrNativeHeapInfo>();

                foreach (SosMemoryRegion mem in memoryEnum)
                {
                    results ??= new();
                    results.Add(new ClrNativeHeapInfo(MemoryRange.CreateFromLength(mem.Start.ToAddress(_target), mem.Length.ToAddress(_target)), NativeHeapKind.HandleTable, ClrNativeHeapState.Active, mem.Heap));
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateGCBookkeepingRegions()
        {
            List<ClrNativeHeapInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                using SosMemoryEnum? memoryEnum = _sos13?.GetGCBookkeepingMemoryRegions();
                if (memoryEnum is null)
                    return Array.Empty<ClrNativeHeapInfo>();

                foreach (SosMemoryRegion mem in memoryEnum)
                {
                    results ??= new();
                    results.Add(new ClrNativeHeapInfo(MemoryRange.CreateFromLength(mem.Start.ToAddress(_target), mem.Length.ToAddress(_target)), NativeHeapKind.GCBookkeeping, ClrNativeHeapState.RegionOfRegions));
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        public IEnumerable<ClrSyncBlockCleanupData> EnumerateSyncBlockCleanupData()
        {
            List<ClrSyncBlockCleanupData>? results = null;
            lock (_sos.SyncRoot)
            {
                HashSet<ulong> seen = [0];
                ulong syncBlock = 0;
                HResult hr = default;
                // For back-compat with older versions of CLRMD, SOS returns E_INVALIDARG if you pass in 0 for the sync block.
                // So we will continue on E_INVALIDARG if it is this special case of passing 0 to start enumerating.
                while ((hr = _sos.GetSyncBlockCleanupData(ClrDataAddress.FromTargetAddress(syncBlock, _target), out SyncBlockCleanupData data)) || (hr == HResult.E_INVALIDARG && syncBlock == 0))
                {
                    results ??= new();
                    results.Add(new(data.SyncBlockPointer.ToAddress(_target), data.BlockRCW.ToAddress(_target), data.BlockCCW.ToAddress(_target), data.BlockClassFactory.ToAddress(_target)));

                    if (!seen.Add(data.NextSyncBlock.ToAddress(_target)))
                        break;

                    syncBlock = data.NextSyncBlock.ToAddress(_target);
                }
            }

            return (IEnumerable<ClrSyncBlockCleanupData>?)results ?? Array.Empty<ClrSyncBlockCleanupData>();
        }

        private NativeHeapKind[] GetNativeHeaps()
        {
            NativeHeapKind[]? heapNativeTypes = _heapNativeTypes;
            if (heapNativeTypes is not null)
                return heapNativeTypes;

            if (_sos13 is null)
                heapNativeTypes = Array.Empty<NativeHeapKind>();
            else
                heapNativeTypes = _sos13.GetLoaderAllocatorHeapNames().Select(r => r switch {
                    "LowFrequencyHeap" => NativeHeapKind.LowFrequencyHeap,
                    "HighFrequencyHeap" => NativeHeapKind.HighFrequencyHeap,
                    "StubHeap" => NativeHeapKind.StubHeap,
                    "ExecutableHeap" => NativeHeapKind.ExecutableHeap,
                    "FixupPrecodeHeap" => NativeHeapKind.FixupPrecodeHeap,
                    "NewStubPrecodeHeap" => NativeHeapKind.NewStubPrecodeHeap,
                    "IndcellHeap" => NativeHeapKind.IndirectionCellHeap,
                    "LookupHeap" => NativeHeapKind.LookupHeap,
                    "ResolveHeap" => NativeHeapKind.ResolveHeap,
                    "DispatchHeap" => NativeHeapKind.DispatchHeap,
                    "CacheEntryHeap" => NativeHeapKind.CacheEntryHeap,
                    "VtableHeap" => NativeHeapKind.VtableHeap,
                    _ => NativeHeapKind.Unknown
                }).ToArray();

            _heapNativeTypes = heapNativeTypes;
            return heapNativeTypes;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateJitManagerHeaps(ulong jitManager)
        {
            List<ClrNativeHeapInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                foreach (JitCodeHeapInfo mem in _sos.GetCodeHeapList(ClrDataAddress.FromTargetAddress(jitManager, _target)))
                {
                    if (mem.Kind == CodeHeapKind.Loader)
                    {
                        foreach (ClrNativeHeapInfo heap in LegacyEnumerateLoaderAllocatorHeaps(mem.Address.ToAddress(_target), LoaderHeapKind.LoaderHeapKindExplicitControl, NativeHeapKind.LoaderCodeHeap))
                        {
                            results ??= new();
                            results.Add(heap);
                        }
                    }
                    else if (mem.Kind == CodeHeapKind.Host)
                    {
                        results ??= new();
                        results.Add(new ClrNativeHeapInfo(new(mem.Address.ToAddress(_target), mem.CurrentAddress.ToAddress(_target)), NativeHeapKind.HostCodeHeap, ClrNativeHeapState.Active));
                    }
                    else
                    {
                        ulong addr = mem.Address.ToAddress(_target);
                        results ??= new();
                        results.Add(new ClrNativeHeapInfo(new(addr, addr), NativeHeapKind.Unknown, ClrNativeHeapState.None));
                    }
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateDomainHeaps(ulong domain)
        {
            if (domain == 0)
                return Array.Empty<ClrNativeHeapInfo>();

            List<ClrNativeHeapInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                ulong loaderAllocator;
                if (_sos13 is not null
                    && (loaderAllocator = _sos13.GetDomainLoaderAllocator(ClrDataAddress.FromTargetAddress(domain, _target)).ToAddress(_target)) != 0
                    && GetNativeHeaps().Length > 0)
                {
                    foreach (ClrNativeHeapInfo heap in EnumerateLoaderAllocatorNativeHeapsLocked(loaderAllocator))
                    {
                        results ??= new();
                        results.Add(heap);
                    }
                }
                else if (_sos.GetAppDomainData(ClrDataAddress.FromTargetAddress(domain, _target), out AppDomainData data))
                {
                    foreach (ClrNativeHeapInfo heapInfo in LegacyEnumerateLoaderAllocatorHeaps(data.StubHeap.ToAddress(_target), LoaderHeapKind.LoaderHeapKindNormal, NativeHeapKind.StubHeap))
                    {
                        results ??= new();
                        results.Add(heapInfo);
                    }

                    foreach (ClrNativeHeapInfo heapInfo in LegacyEnumerateLoaderAllocatorHeaps(data.HighFrequencyHeap.ToAddress(_target), LoaderHeapKind.LoaderHeapKindNormal, NativeHeapKind.HighFrequencyHeap))
                    {
                        results ??= new();
                        results.Add(heapInfo);
                    }

                    foreach (ClrNativeHeapInfo heapInfo in LegacyEnumerateLoaderAllocatorHeaps(data.LowFrequencyHeap.ToAddress(_target), LoaderHeapKind.LoaderHeapKindNormal, NativeHeapKind.LowFrequencyHeap))
                    {
                        results ??= new();
                        results.Add(heapInfo);
                    }

                    foreach (ClrNativeHeapInfo heapInfo in LegacyEnumerateStubHeaps(domain))
                    {
                        results ??= new();
                        results.Add(heapInfo);
                    }
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateLoaderAllocatorNativeHeaps(ulong loaderAllocator)
        {
            lock (_sos.SyncRoot)
                return EnumerateLoaderAllocatorNativeHeapsLocked(loaderAllocator);
        }

        private IEnumerable<ClrNativeHeapInfo> EnumerateLoaderAllocatorNativeHeapsLocked(ulong loaderAllocator)
        {
            NativeHeapKind[] heapNativeTypes;
            if (loaderAllocator == 0
                || _sos13 is null
                || (heapNativeTypes = GetNativeHeaps()).Length == 0)
            {
                return Array.Empty<ClrNativeHeapInfo>();
            }

            List<ClrNativeHeapInfo>? results = null;
            List<ClrNativeHeapInfo>? current = null;
            (ClrDataAddress Address, LoaderHeapKind Kind)[] heaps = _sos13.GetLoaderAllocatorHeaps(ClrDataAddress.FromTargetAddress(loaderAllocator, _target));
            for (int i = 0; i < heaps.Length; i++)
            {
                current?.Clear();
                HResult hr = _sos13.TraverseLoaderHeap(heaps[i].Address, heaps[i].Kind, (address, size, currentSegment) => {
                    current ??= new(16);
                    current.Add(new(MemoryRange.CreateFromLength(address, SanitizeSize(size)), heapNativeTypes[i], currentSegment != 0 ? ClrNativeHeapState.Active : ClrNativeHeapState.Inactive));
                });

                if (hr && current is not null)
                {
                    results ??= new();
                    results.AddRange(current);
                }
            }

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        private IEnumerable<ClrNativeHeapInfo> LegacyEnumerateStubHeaps(ulong domain)
        {
            List<ClrNativeHeapInfo>? results = null;
            List<ClrNativeHeapInfo> current = new(16);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.IndcellHeap, NativeHeapKind.IndirectionCellHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.LookupHeap, NativeHeapKind.LookupHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.ResolveHeap, NativeHeapKind.ResolveHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.DispatchHeap, NativeHeapKind.DispatchHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.CacheEntryHeap, NativeHeapKind.CacheEntryHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            TraverseOneStubKind(domain, current, SOSDac.VCSHeapType.VtableHeap, NativeHeapKind.VtableHeap);
            if (current.Count > 0)
                (results ??= new()).AddRange(current);

            return (IEnumerable<ClrNativeHeapInfo>?)results ?? Array.Empty<ClrNativeHeapInfo>();
        }

        private void TraverseOneStubKind(ulong domain, List<ClrNativeHeapInfo> result, SOSDac.VCSHeapType vcsType, NativeHeapKind heapKind)
        {
            result.Clear();
            HResult hr = _sos.TraverseStubHeap(ClrDataAddress.FromTargetAddress(domain, _target), vcsType, (address, size, current) => {
                result.Add(new(MemoryRange.CreateFromLength(address, SanitizeSize(size)), heapKind, current != 0 ? ClrNativeHeapState.Active : ClrNativeHeapState.Inactive));
            });

            if (!hr)
                result.Clear();
        }

        private IEnumerable<ClrNativeHeapInfo> LegacyEnumerateLoaderAllocatorHeaps(ulong loaderHeap, LoaderHeapKind loaderHeapKind, NativeHeapKind nativeHeapKind)
        {
            if (loaderHeap != 0)
            {
                List<ClrNativeHeapInfo>? result = null;

                // The basic ISOSDacInterface doesn't understand the difference between the different kinds of runtime
                // loader heaps.  We have to adjust certain loader heap kinds based on the version of dac we are
                // targeting.  This includes .Net 7, and .Net 8 before ISOSDacInterface13 was implemented.  Additionally,
                // we don't know the version info for a lot of versions of single-file compilation.  In all of those
                // cases, we need to adjust the pointer. In .NET 11, we stabilize the layouts such that none of this is necessary.

                bool normalNeedsAdjustment = false;
                bool explicitDoesNotNeedAdjustment = false;
                if (_clrInfo.Flavor == ClrFlavor.Core)
                {
                    int versionMajor = _clrInfo.Version.Major;
                    normalNeedsAdjustment = versionMajor == 7 || versionMajor == 8 && _sos13 is null || versionMajor == 0;
                    explicitDoesNotNeedAdjustment = versionMajor >= 11 || versionMajor == 0;
                }

                ulong fixedHeapAddress = FixupHeapAddress(loaderHeap, loaderHeapKind, normalNeedsAdjustment, explicitDoesNotNeedAdjustment);

                HResult hr = _sos.TraverseLoaderHeap(ClrDataAddress.FromTargetAddress(fixedHeapAddress, _target), (address, size, current) => {
                    result ??= new(8);
                    result.Add(new(MemoryRange.CreateFromLength(address, SanitizeSize(size)), nativeHeapKind, current != 0 ? ClrNativeHeapState.Active : ClrNativeHeapState.Inactive));
                });

                bool retry = !hr;

                if (result is not null && result.Count > 0 && normalNeedsAdjustment)
                {
                    // If we adjusted the pointer and we can't read the resulting addresses, try again with the
                    // opposite setting.
                    byte[] buffer = new byte[1];

                    if (result.Any(entry => _dataReader.Read(entry.MemoryRange.Start, buffer) == 0))
                    {
                        result.Clear();
                        retry = true;
                    }
                }

                if (retry)
                {
                    fixedHeapAddress = FixupHeapAddress(loaderHeap, loaderHeapKind, !normalNeedsAdjustment, !explicitDoesNotNeedAdjustment);
                    hr = _sos.TraverseLoaderHeap(ClrDataAddress.FromTargetAddress(fixedHeapAddress, _target), (address, size, current) => {
                        result ??= new(8);
                        result.Add(new(MemoryRange.CreateFromLength(address, SanitizeSize(size)), nativeHeapKind, current != 0 ? ClrNativeHeapState.Active : ClrNativeHeapState.Inactive));
                    });
                }

                // If TraverseLoaderHeap returns a failing HRESULT, it means that it encountered a bad block.
                // This likely means that loaderHeap points to bad memory and we should ignore this entire
                // enumeration.
                if (hr && result != null)
                    return result;
            }

            return Enumerable.Empty<ClrNativeHeapInfo>();
        }

        private ulong FixupHeapAddress(ulong loaderHeap, LoaderHeapKind loaderHeapKind, bool normalNeedsAdjustment, bool explicitDoesNotNeedAdjustment)
        {
            if (normalNeedsAdjustment)
            {
                if (loaderHeapKind == LoaderHeapKind.LoaderHeapKindNormal)
                    loaderHeap += (uint)_dataReader.PointerSize;
            }
            else
            {
                if (loaderHeapKind == LoaderHeapKind.LoaderHeapKindExplicitControl && !explicitDoesNotNeedAdjustment)
                    loaderHeap -= (uint)_dataReader.PointerSize;
            }

            return loaderHeap;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateThunkHeaps(ulong thunkHeapAddress)
        {
            if (thunkHeapAddress == 0)
                return Array.Empty<ClrNativeHeapInfo>();

            lock (_sos.SyncRoot)
            {
                List<ClrNativeHeapInfo>? heaps = null;
                HResult hr = TraverseLoaderHeap(thunkHeapAddress, LoaderHeapKind.LoaderHeapKindNormal, (address, size, current) => {
                    heaps ??= new(16);
                    heaps.Add(new(MemoryRange.CreateFromLength(address, SanitizeSize(size)), NativeHeapKind.ThunkHeap, current != 0 ? ClrNativeHeapState.Active : ClrNativeHeapState.Inactive));
                });

                if (hr && heaps is not null && heaps.Count > 0)
                    return heaps;
            }

            return Array.Empty<ClrNativeHeapInfo>();
        }

        internal static ulong SanitizeSize(nint size)
        {
            // If TraverseHeap returns a negative size or a size that's too large, we'll treat
            // this as not having size info.  This shouldn't happen in practice.
            if (size is < 0 or > int.MaxValue)
                return 0;

            return (ulong)size;
        }

        private HResult TraverseLoaderHeap(ulong address, LoaderHeapKind kind, SOSDac.LoaderHeapTraverse callback)
            => TraverseLoaderHeap(_clrInfo, _sos, _sos13, address, kind, _target, callback);

        private static HResult TraverseLoaderHeap(ClrInfo clrInfo, SOSDac sos, ISOSDac13? sos13, ulong address, LoaderHeapKind kind, TargetProperties target, SOSDac.LoaderHeapTraverse callback)
        {
            if (address == 0)
                return HResult.E_INVALIDARG;

            HResult hr;
            if (sos13 is not null)
            {
                // ISOSDacInterface13 understands how to walk LoaderHeaps properly, but it's only implemented in
                // .Net 8 and beyond.

                hr = sos13.TraverseLoaderHeap(ClrDataAddress.FromTargetAddress(address, target), kind, callback);
            }
            else if (clrInfo.Flavor == ClrFlavor.Core && clrInfo.Version.Major == 7)
            {
                // See note below, .Net 7 inverts the logic that everything else uses.

                if (kind == LoaderHeapKind.LoaderHeapKindNormal)
                    address += (uint)target.PointerSize;

                hr = sos.TraverseLoaderHeap(ClrDataAddress.FromTargetAddress(address, target), callback);
            }
            else
            {
                // The basic ISOSDacInterface doesn't understand the difference between the different kinds of runtime
                // loader heaps.  If the heap is an ExplicitControlLoaderHeap then it doesn't have a vtable but it will
                // be treated as a LoaderHeap, which has the same layout aside from the fact that it DOES have a vtable.
                // So, if we are enumerating an ExplictControl we need to move the address back by one pointer so that
                // the enumeration code will work properly.

                if (kind == LoaderHeapKind.LoaderHeapKindExplicitControl)
                    address -= (uint)target.PointerSize;

                hr = sos.TraverseLoaderHeap(ClrDataAddress.FromTargetAddress(address, target), callback);
            }

            GC.KeepAlive(callback);
            return hr;
        }
    }
}
