// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdRuntime : ClrRuntime
    {
        private readonly IRuntimeHelpers _helpers;
        private ClrHeap? _heap;
        private ClrModule? _bcl;
        private ImmutableArray<ClrThread> _threads;
        private ImmutableArray<ClrAppDomain> _domains;
        private ClrAppDomain? _systemDomain;
        private ClrAppDomain? _sharedDomain;
        private bool _disposed;

        public override bool IsThreadSafe => _helpers.Factory.IsThreadSafe && _helpers.DataReader.IsThreadSafe;
        public override DataTarget? DataTarget => ClrInfo?.DataTarget;
        public override DacLibrary DacLibrary { get; }
        public override ClrInfo ClrInfo { get; }

        public override ImmutableArray<ClrThread> Threads
        {
            get
            {
                if (_threads.IsDefault)
                    _threads = _helpers.GetThreads(this);

                return _threads;
            }
        }

        public override ClrHeap Heap => _heap ??= _helpers.Factory.GetOrCreateHeap();

        public override ImmutableArray<ClrAppDomain> AppDomains
        {
            get
            {
                if (_domains.IsDefault)
                    _domains = _helpers.GetAppDomains(this, out _systemDomain, out _sharedDomain);

                return _domains;
            }
        }

        public override ClrAppDomain? SharedDomain
        {
            get
            {
                if (_domains.IsDefault)
                    _ = AppDomains;

                return _sharedDomain;
            }
        }

        public override ClrAppDomain? SystemDomain
        {
            get
            {
                if (_domains.IsDefault)
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
        /// Gets the <see cref="ClrType"/> corresponding to the given MethodTable.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or <see langword="null"/> if no such type exists.</returns>
        public override ClrType? GetTypeByMethodTable(ulong methodTable) => _helpers.Factory.GetOrCreateType(methodTable, 0);

        /// <summary>
        /// Flushes the DAC cache.  This function <b>must</b> be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.
        /// </summary>
        public override void FlushCachedData()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClrRuntime));

            _heap = null;
            _bcl = null;
            _threads = default;
            _domains = default;
            _systemDomain = null;
            _sharedDomain = null;

            _helpers.DataReader.FlushCachedData();
            _helpers.FlushCachedData();
        }

        public override IEnumerable<ClrModule> EnumerateModules()
        {
            // In Desktop CLR, modules in the SharedDomain can potentially also be in every other domain.
            // To prevent duplicates we'll first enumerate all shared modules, then we'll make sure every
            // module we yield return after that isn't in the SharedDomain.
            // In .NET Core, there's only one AppDomain and no shared domain, so "sharedModules" will always be
            // Empty and we'll enumerate everything in the single domain.

            ImmutableArray<ClrModule> sharedModules = SharedDomain?.Modules ?? ImmutableArray<ClrModule>.Empty;

            foreach (ClrModule module in sharedModules)
                yield return module;

            // sharedModules will always contain a small number of items, so using the raw array will be better
            // than creating a tiny HashSet.
            foreach (ClrAppDomain domain in AppDomains)
                foreach (ClrModule module in domain.Modules)
                    if (!sharedModules.Contains(module))
                        yield return module;

            if (SystemDomain != null)
                foreach (ClrModule module in SystemDomain.Modules)
                    if (!sharedModules.Contains(module))
                        yield return module;
        }

        public override IEnumerable<ClrHandle> EnumerateHandles() => _helpers.EnumerateHandleTable(this);

        public override string? GetJitHelperFunctionName(ulong ip) => _helpers.GetJitHelperFunctionName(ip);

        public override ClrMethod? GetMethodByHandle(ulong methodHandle) => _helpers.Factory.CreateMethodFromHandle(methodHandle);

        public override ClrMethod? GetMethodByInstructionPointer(ulong ip)
        {
            ulong md = _helpers.GetMethodDesc(ip);
            if (md == 0)
                return null;

            return GetMethodByHandle(md);
        }

        // _bcl will not be null by the time we reach this
        public override ClrModule BaseClassLibrary => _bcl!;
    }
}
