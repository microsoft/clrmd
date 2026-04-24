// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal sealed unsafe class DacRuntime : IAbstractRuntime
    {
        private readonly IDataReader _dataReader;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly ISOSDac13? _sos13;
        private readonly TargetProperties _target;

        public DacRuntime(ClrInfo clrInfo, ClrDataProcess dac, SOSDac sos, ISOSDac13? sos13, TargetProperties target)
        {
            _dataReader = clrInfo.DataTarget.DataReader;
            _dac = dac;
            _sos = sos;
            _sos13 = sos13;
            _target = target;

            int version = 0;
            if (!_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data. This may indicate the dump was taken before the CLR was fully initialized, or the dump is corrupt or truncated. Failed to request DacVersion.");

            if (version is not (9 or 10))
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            _sos.DacVersion = version;
        }

        public IEnumerable<ClrThreadInfo> EnumerateThreads()
        {
            if (!_sos.GetThreadStoreData(out ThreadStoreData threadStore))
                yield break;

            HashSet<ulong> seen = new() { 0 };
            ClrDataAddress currentThread = threadStore.FirstThread;
            ulong threadAddress = currentThread.ToAddress(_target);

            for (int i = 0; i < threadStore.ThreadCount && seen.Add(threadAddress); i++)
            {
                if (!_sos.GetThreadData(currentThread, out ThreadData threadData))
                    break;

                ulong ex = 0;
                if (!threadData.LastThrownObjectHandle.IsNull)
                    ex = _dataReader.ReadPointer(threadData.LastThrownObjectHandle.ToAddress(_target));

                ulong stackBase = 0;
                ulong stackLimit = 0;
                ulong teb = 0;

                // Prefer IThreadReader.GetThreadTeb which looks up the TEB from the OS thread ID
                // via the data reader. Fall back to threadData.Teb for DesktopCLR or when the
                // data reader doesn't implement IThreadReader.
                if (_dataReader is IThreadReader threadReader)
                    teb = threadReader.GetThreadTeb(threadData.OSThreadId);

                if (teb == 0)
                    teb = threadData.Teb.ToAddress(_target);

                if (teb != 0)
                {
                    stackBase = _dataReader.ReadPointer(teb + (uint)_target.PointerSize);
                    stackLimit = _dataReader.ReadPointer(teb + (uint)_target.PointerSize * 2);
                }

                yield return new()
                {
                    Address = threadAddress,
                    AppDomain = threadData.Domain.ToAddress(_target),
                    ExceptionInFlight = ex,
                    GCMode = threadData.PreemptiveGCDisabled == 0 ? GCMode.Preemptive : GCMode.Cooperative,
                    IsFinalizer = threadStore.FinalizerThread.ToAddress(_target) == threadAddress,
                    IsGC = threadStore.GCThread.ToAddress(_target) == threadAddress,
                    LockCount = threadData.LockCount,
                    ManagedThreadId = threadData.ManagedThreadId < int.MaxValue ? (int)threadData.ManagedThreadId : int.MaxValue,
                    OSThreadId = threadData.OSThreadId,
                    StackBase = stackBase,
                    StackLimit = stackLimit,
                    State = (ClrThreadState)threadData.State,
                    Teb = teb,
                };

                currentThread = threadData.NextThread;
                threadAddress = currentThread.ToAddress(_target);
            }
        }

        public IEnumerable<AppDomainInfo> EnumerateAppDomains()
        {
            if (!_sos.GetAppDomainStoreData(out AppDomainStoreData domainStore))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data. This may indicate the dump was taken before the CLR was fully initialized, or the dump is corrupt or truncated. Failed to request AppDomainStoreData.");

            if (!domainStore.SharedDomain.IsNull)
                yield return CreateAppDomainInfo(domainStore.SharedDomain.ToAddress(_target), AppDomainKind.Shared, "Shared Domain");

            if (!domainStore.SystemDomain.IsNull)
                yield return CreateAppDomainInfo(domainStore.SystemDomain.ToAddress(_target), AppDomainKind.System, "System Domain");

            foreach (ClrDataAddress domain in _sos.GetAppDomainList())
            {
                ulong domainAddr = domain.ToAddress(_target);
                string name = _sos.GetAppDomainName(domain) ?? "";
                yield return CreateAppDomainInfo(domainAddr, AppDomainKind.Normal, name);
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
                ConfigFile = _sos.GetConfigFile(ClrDataAddress.FromAddress(address, _target)),
                ApplicationBase = _sos.GetAppBase(ClrDataAddress.FromAddress(address, _target)),
            };

            if (_sos.GetAppDomainData(ClrDataAddress.FromAddress(address, _target), out AppDomainData data))
                result.Id = data.Id;

            if (_sos13 is not null)
            {
                result.LoaderAllocator = _sos13.GetDomainLoaderAllocator(ClrDataAddress.FromAddress(address, _target)).ToAddress(_target);
            }

            return result;
        }

        public IEnumerable<ulong> GetModuleList(ulong domain)
        {
            return _sos.GetAssemblyList(ClrDataAddress.FromAddress(domain, _target))
                .SelectMany(assembly => _sos.GetModuleList(assembly))
                .Select(module => module.ToAddress(_target));
        }

        public IEnumerable<ClrHandleInfo> EnumerateHandles()
        {
            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles();
            if (handleEnum is null)
                yield break;

            foreach (HandleData handle in handleEnum.ReadHandles())
            {
                ulong handleAddr = handle.Handle.ToAddress(_target);
                yield return new ClrHandleInfo()
                {
                    Address = handleAddr,
                    Object = _dataReader.ReadPointer(handleAddr),
                    Kind = (ClrHandleKind)handle.Type,
                    AppDomain = handle.AppDomain.ToAddress(_target),
                    DependentTarget = handle.Secondary.ToAddress(_target),
                    RefCount = handle.IsPegged != 0 ? handle.JupiterRefCount : handle.RefCount,
                };
            }
        }

        public IEnumerable<JitManagerInfo> EnumerateClrJitManagers()
        {
            foreach (JitManagerData jitMgr in _sos.GetJitManagers())
                yield return new()
                {
                    Address = jitMgr.Address.ToAddress(_target),
                    Kind = jitMgr.Kind,
                    HeapList = jitMgr.HeapList.ToAddress(_target),
                };
        }

        public string? GetJitHelperFunctionName(ulong address) => _sos.GetJitHelperFunctionName(ClrDataAddress.FromAddress(address, _target));
    }
}