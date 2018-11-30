// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class AppDomainHeapWalker
    {
        private enum InternalHeapTypes
        {
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap
        }

        private readonly List<MemoryRegion> _regions = new List<MemoryRegion>();
        private readonly SOSDac.LoaderHeapTraverse _delegate;
        private ClrMemoryRegionType _type;
        private ulong _appDomain;
        private readonly DesktopRuntimeBase _runtime;

        public AppDomainHeapWalker(DesktopRuntimeBase runtime)
        {
            _runtime = runtime;
            _delegate = VisitOneHeap;
        }

        public IEnumerable<MemoryRegion> EnumerateHeaps(IAppDomainData appDomain)
        {
            Debug.Assert(appDomain != null);
            _appDomain = appDomain.Address;
            _regions.Clear();

            // Standard heaps.
            _type = ClrMemoryRegionType.LowFrequencyLoaderHeap;
            _runtime.TraverseHeap(appDomain.LowFrequencyHeap, _delegate);

            _type = ClrMemoryRegionType.HighFrequencyLoaderHeap;
            _runtime.TraverseHeap(appDomain.HighFrequencyHeap, _delegate);

            _type = ClrMemoryRegionType.StubHeap;
            _runtime.TraverseHeap(appDomain.StubHeap, _delegate);

            // Stub heaps.
            _type = ClrMemoryRegionType.IndcellHeap;
            _runtime.TraverseStubHeap(_appDomain, (int)InternalHeapTypes.IndcellHeap, _delegate);

            _type = ClrMemoryRegionType.LookupHeap;
            _runtime.TraverseStubHeap(_appDomain, (int)InternalHeapTypes.LookupHeap, _delegate);

            _type = ClrMemoryRegionType.ResolveHeap;
            _runtime.TraverseStubHeap(_appDomain, (int)InternalHeapTypes.ResolveHeap, _delegate);

            _type = ClrMemoryRegionType.DispatchHeap;
            _runtime.TraverseStubHeap(_appDomain, (int)InternalHeapTypes.DispatchHeap, _delegate);

            _type = ClrMemoryRegionType.CacheEntryHeap;
            _runtime.TraverseStubHeap(_appDomain, (int)InternalHeapTypes.CacheEntryHeap, _delegate);

            return _regions;
        }

        public IEnumerable<MemoryRegion> EnumerateModuleHeaps(IAppDomainData appDomain, ulong addr)
        {
            Debug.Assert(appDomain != null);
            _appDomain = appDomain.Address;
            _regions.Clear();

            if (addr == 0)
                return _regions;

            IModuleData module = _runtime.GetModuleData(addr);
            if (module != null)
            {
                _type = ClrMemoryRegionType.ModuleThunkHeap;
                _runtime.TraverseHeap(module.ThunkHeap, _delegate);

                _type = ClrMemoryRegionType.ModuleLookupTableHeap;
                _runtime.TraverseHeap(module.LookupTableHeap, _delegate);
            }

            return _regions;
        }

        public IEnumerable<MemoryRegion> EnumerateJitHeap(ulong heap)
        {
            _appDomain = 0;
            _regions.Clear();

            _type = ClrMemoryRegionType.JitLoaderCodeHeap;
            _runtime.TraverseHeap(heap, _delegate);

            return _regions;
        }

        private void VisitOneHeap(ulong address, IntPtr size, int isCurrent)
        {
            if (_appDomain == 0)
                _regions.Add(new MemoryRegion(_runtime, address, (ulong)size.ToInt64(), _type));
            else
                _regions.Add(new MemoryRegion(_runtime, address, (ulong)size.ToInt64(), _type, _appDomain));
        }
    }
}