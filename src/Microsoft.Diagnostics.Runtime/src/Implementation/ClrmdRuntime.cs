// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdRuntime : ClrRuntime
    {
        private readonly IRuntimeHelpers _helpers;
        private ClrHeap _heap;
        private ClrModule _bcl;
        private IReadOnlyList<ClrThread> _threads;
        private IReadOnlyList<ClrAppDomain> _domains;
        private ClrAppDomain _systemDomain;
        private ClrAppDomain _sharedDomain;
        private bool _disposed;

        public override DataTarget DataTarget => ClrInfo?.DataTarget;
        public override DacLibrary DacLibrary { get; }
        public override ClrInfo ClrInfo { get; }
        public override IReadOnlyList<ClrThread> Threads => _threads ?? (_threads = _helpers.GetThreads(this));
        public override ClrHeap Heap => _heap ?? (_heap = _helpers.Factory.GetOrCreateHeap());
        public override IReadOnlyList<ClrAppDomain> AppDomains
        {
            get
            {
                if (_domains == null)
                    _domains = _helpers.GetAppDomains(this, out _systemDomain, out _sharedDomain);

                return _domains;
            }
        }

        public override ClrAppDomain SharedDomain
        {
            get
            {
                if (_domains == null)
                    _ = AppDomains;

                return _sharedDomain;
            }
        }

        public override ClrAppDomain SystemDomain
        {
            get
            {
                if (_domains == null)
                    _ = AppDomains;

                return _systemDomain;
            }
        }

        public ClrmdRuntime(ClrInfo info, DacLibrary dac, IRuntimeHelpers helpers)
        {
            ClrInfo = info;
            DacLibrary = dac;
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }


        public void Initialize()
        {
            _ = AppDomains;
            _bcl = _helpers.GetBaseClassLibrary(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _helpers?.Dispose();
            }
        }

        /// <summary>
        /// Gets the ClrType corresponding to the given MethodTable.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <param name="componentMethodTable">The ClrType's component MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        public override ClrType GetTypeByMethodTable(ulong methodTable) => _helpers.Factory.GetOrCreateType(methodTable, 0);

        /// <summary>
        /// Flushes the dac cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.
        /// </summary>
        public override void ClearCachedData()
        {
            OnRuntimeFlushed();
            _helpers.DataReader.ClearCachedData();
            _helpers.ClearCachedData();
            _heap = null;
            _bcl = null;
            _threads = null;
        }

        public override IEnumerable<ClrModule> EnumerateModules()
        {
            IEnumerable<ClrModule> modules = AppDomains.SelectMany(ad => ad.Modules);

            if (SharedDomain != null)
                modules = SharedDomain.Modules.Concat(modules);

            if (SystemDomain != null)
                modules = SystemDomain.Modules.Concat(modules);

            return modules;
        }

        public override IEnumerable<ClrHandle> EnumerateHandles() => _helpers.EnumerateHandleTable(this);

        public override string GetJitHelperFunctionName(ulong ip) => _helpers.GetJitHelperFunctionName(ip);

        public override ClrMethod GetMethodByHandle(ulong methodHandle) => _helpers.Factory.CreateMethodFromHandle(methodHandle);

        /// <summary>
        /// Converts an address into an AppDomain.
        /// </summary>
        internal ClrAppDomain GetAppDomainByAddress(ulong address)
        {
            foreach (ClrAppDomain ad in AppDomains)
                if (ad.Address == address)
                    return ad;

            return null;
        }

        public override ClrMethod GetMethodByInstructionPointer(ulong ip)
        {
            ulong md = _helpers.GetMethodDesc(ip);
            if (md == 0)
                return null;

            return GetMethodByHandle(md);
        }

        public override ClrModule BaseClassLibrary => _bcl;
    }
}
