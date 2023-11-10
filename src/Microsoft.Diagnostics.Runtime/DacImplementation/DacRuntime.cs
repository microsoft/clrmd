// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal sealed unsafe class DacRuntime : IAbstractRuntime
    {
        private readonly IDataReader _dataReader;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly ISOSDac13? _sos13;

        public DacRuntime(ClrInfo clrInfo, ClrDataProcess dac, SOSDac sos, ISOSDac13? sos13)
        {
            _dataReader = clrInfo.DataTarget.DataReader;
            _dac = dac;
            _sos = sos;
            _sos13 = sos13;

            int version = 0;
            if (!_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");
        }

        public IEnumerable<ClrThreadInfo> EnumerateThreads()
        {
            if (!_sos.GetThreadStoreData(out ThreadStoreData threadStore))
                yield break;

            HashSet<ulong> seen = new() { 0 };
            ulong threadAddress = threadStore.FirstThread;

            uint pointerSize = (uint)_dataReader.PointerSize;

            for (int i = 0; i < threadStore.ThreadCount && seen.Add(threadAddress); i++)
            {
                if (!_sos.GetThreadData(threadAddress, out ThreadData threadData))
                    break;

                ulong ex = 0;
                if (threadData.LastThrownObjectHandle != 0)
                    ex = _dataReader.ReadPointer(threadData.LastThrownObjectHandle);

                ulong stackBase = 0;
                ulong stackLimit = 0;
                if (threadData.Teb != 0)
                {
                    stackBase = _dataReader.ReadPointer(threadData.Teb + pointerSize);
                    stackLimit = _dataReader.ReadPointer(threadData.Teb + pointerSize * 2);
                }

                yield return new()
                {
                    Address = threadAddress,
                    AppDomain = threadData.Domain,
                    ExceptionInFlight = ex,
                    GCMode = threadData.PreemptiveGCDisabled == 0 ? GCMode.Preemptive : GCMode.Cooperative,
                    IsFinalizer = threadStore.FinalizerThread == threadAddress,
                    IsGC = threadStore.GCThread == threadAddress,
                    LockCount = threadData.LockCount,
                    ManagedThreadId = threadData.ManagedThreadId < int.MaxValue ? (int)threadData.ManagedThreadId : int.MaxValue,
                    OSThreadId = threadData.OSThreadId,
                    StackBase = stackBase,
                    StackLimit = stackLimit,
                    State = (ClrThreadState)threadData.State,
                    Teb = threadData.Teb,
                };

                threadAddress = threadData.NextThread;
            }
        }

        public IEnumerable<AppDomainInfo> EnumerateAppDomains()
        {
            if (!_sos.GetAppDomainStoreData(out AppDomainStoreData domainStore))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data. Failed to request AppDomainStoreData.");

            if (domainStore.SharedDomain != 0)
                yield return CreateAppDomainInfo(domainStore.SharedDomain, AppDomainKind.Shared, "Shared Domain");

            if (domainStore.SystemDomain != 0)
                yield return CreateAppDomainInfo(domainStore.SystemDomain, AppDomainKind.System, "System Domain");

            foreach (ulong domain in _sos.GetAppDomainList())
            {
                string name = _sos.GetAppDomainName(domain) ?? "";
                yield return CreateAppDomainInfo(domain, AppDomainKind.Normal, name);
            }
        }

        private AppDomainInfo CreateAppDomainInfo(ulong address, AppDomainKind kind, string name)
        {
            AppDomainInfo result = new()
            {
                Address = address,
                Kind = kind,
                Name = name,
                Id = int.MinValue,
                ConfigFile = _sos.GetConfigFile(address),
                ApplicationBase = _sos.GetAppBase(address),
            };

            if (_sos.GetAppDomainData(address, out AppDomainData data))
                result.Id = data.Id;

            if (_sos13 is not null)
                result.LoaderAllocator = _sos13.GetDomainLoaderAllocator(address);

            return result;
        }

        public IEnumerable<ulong> GetModuleList(ulong domain) => _sos.GetAssemblyList(domain).SelectMany(assembly => _sos.GetModuleList(assembly)).Select(module => (ulong)module);

        public IEnumerable<ClrHandleInfo> EnumerateHandles()
        {
            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles();
            if (handleEnum is null)
                yield break;

            foreach (HandleData handle in handleEnum.ReadHandles())
            {
                yield return new ClrHandleInfo()
                {
                    Address = handle.Handle,
                    Object = _dataReader.ReadPointer(handle.Handle),
                    Kind = (ClrHandleKind)handle.Type,
                    AppDomain = handle.AppDomain,
                    DependentTarget = handle.Secondary,
                    RefCount = handle.IsPegged != 0 ? handle.JupiterRefCount : handle.RefCount,
                };
            }
        }

        public IEnumerable<JitManagerInfo> EnumerateClrJitManagers()
        {
            foreach (JitManagerData jitMgr in _sos.GetJitManagers())
                yield return new()
                {
                    Address = jitMgr.Address,
                    Kind = jitMgr.Kind,
                    HeapList = jitMgr.HeapList,
                };
        }

        public string? GetJitHelperFunctionName(ulong address) => _sos.GetJitHelperFunctionName(address);
    }
}