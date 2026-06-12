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
            // This DAC request runs during lazy service construction (under _serviceLock);
            // serialize it on the DAC lock like every other DAC entry point.
            lock (_sos.SyncRoot)
            {
                if (!_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))))
                    throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data. This may indicate the dump was taken before the CLR was fully initialized, or the dump is corrupt or truncated. Failed to request DacVersion.");

                if (version is not (9 or 10))
                    throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

                _sos.DacVersion = version;
            }
        }

        internal ulong GetStressLogAddress()
        {
            // Translate the wire-format ClrDataAddress to a target-process
            // pointer; ToAddress un-sign-extends the high 32 bits on 32-bit
            // targets so the address can be used directly with IDataReader.
            lock (_sos.SyncRoot)
                return _sos.TryGetStressLogAddress(out ClrDataAddress addr) ? addr.ToAddress(_target) : 0;
        }

        public IEnumerable<ClrThreadInfo> EnumerateThreads(int maxCount)
        {
            // Materialize entirely under the DAC lock. The IDataReader.ReadPointer
            // / GetThreadTeb calls don't go through the DAC, but the surrounding
            // _sos.* calls must be serialized. maxCount bounds the work so a corrupt
            // dump reporting a huge ThreadCount can't drive unbounded materialization.
            List<ClrThreadInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetThreadStoreData(out ThreadStoreData threadStore))
                    return Array.Empty<ClrThreadInfo>();

                HashSet<ulong> seen = new() { 0 };
                ClrDataAddress currentThread = threadStore.FirstThread;
                ulong threadAddress = currentThread.ToAddress(_target);

                for (int i = 0; i < threadStore.ThreadCount && seen.Add(threadAddress); i++)
                {
                    if ((results?.Count ?? 0) >= maxCount)
                        break;

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

                    results ??= new List<ClrThreadInfo>();
                    results.Add(new()
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
                    });

                    currentThread = threadData.NextThread;
                    threadAddress = currentThread.ToAddress(_target);
                }
            }

            return (IEnumerable<ClrThreadInfo>?)results ?? Array.Empty<ClrThreadInfo>();
        }

        public IEnumerable<AppDomainInfo> EnumerateAppDomains(int maxCount)
        {
            List<AppDomainInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetAppDomainStoreData(out AppDomainStoreData domainStore))
                    throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data. This may indicate the dump was taken before the CLR was fully initialized, or the dump is corrupt or truncated. Failed to request AppDomainStoreData.");

                if (!domainStore.SharedDomain.IsNull && (results?.Count ?? 0) < maxCount)
                {
                    results ??= new List<AppDomainInfo>();
                    results.Add(CreateAppDomainInfo(domainStore.SharedDomain.ToAddress(_target), AppDomainKind.Shared, "Shared Domain"));
                }

                if (!domainStore.SystemDomain.IsNull && (results?.Count ?? 0) < maxCount)
                {
                    results ??= new List<AppDomainInfo>();
                    results.Add(CreateAppDomainInfo(domainStore.SystemDomain.ToAddress(_target), AppDomainKind.System, "System Domain"));
                }

                foreach (ClrDataAddress domain in _sos.GetAppDomainList())
                {
                    if ((results?.Count ?? 0) >= maxCount)
                        break;

                    ulong domainAddr = domain.ToAddress(_target);
                    string name = _sos.GetAppDomainName(domain) ?? "";
                    results ??= new List<AppDomainInfo>();
                    results.Add(CreateAppDomainInfo(domainAddr, AppDomainKind.Normal, name));
                }
            }

            return (IEnumerable<AppDomainInfo>?)results ?? Array.Empty<AppDomainInfo>();
        }

        private AppDomainInfo CreateAppDomainInfo(ulong address, AppDomainKind kind, string name)
        {
            // Always called from within lock(_sos.SyncRoot); Monitor is reentrant
            // so taking it again here would be safe but unnecessary.
            AppDomainInfo result = new()
            {
                Address = address,
                Kind = kind,
                Name = name,
                Id = int.MinValue,
                ConfigFile = _sos.GetConfigFile(ClrDataAddress.FromTargetAddress(address, _target)),
                ApplicationBase = _sos.GetAppBase(ClrDataAddress.FromTargetAddress(address, _target)),
            };

            if (_sos.GetAppDomainData(ClrDataAddress.FromTargetAddress(address, _target), out AppDomainData data))
                result.Id = data.Id;

            if (_sos13 is not null)
            {
                result.LoaderAllocator = _sos13.GetDomainLoaderAllocator(ClrDataAddress.FromTargetAddress(address, _target)).ToAddress(_target);
            }

            return result;
        }

        public IEnumerable<ulong> GetModuleList(ulong domain, int maxCount)
        {
            List<ulong> result = new();
            lock (_sos.SyncRoot)
            {
                foreach (ClrDataAddress assembly in _sos.GetAssemblyList(ClrDataAddress.FromTargetAddress(domain, _target)))
                {
                    if (result.Count >= maxCount)
                        break;

                    foreach (ClrDataAddress module in _sos.GetModuleList(assembly))
                    {
                        if (result.Count >= maxCount)
                            break;

                        result.Add(module.ToAddress(_target));
                    }
                }
            }

            return result;
        }

        public IEnumerable<ClrHandleInfo> EnumerateHandles()
        {
            // SOSHandleEnum is a stateful DAC enumerator. Concurrent enumerations
            // share internal DAC state and can truncate each other. Drain the enumerator
            // entirely under the DAC lock (creation, iteration, dispose), but collect only
            // the raw HandleData under the lock. Resolving the object pointer
            // (IDataReader.ReadPointer) does not go through the DAC, so do it outside the
            // lock to keep the global DAC lock hold time short.
            List<HandleData>? raw = null;
            lock (_sos.SyncRoot)
            {
                using SOSHandleEnum? handleEnum = _sos.EnumerateHandles();
                if (handleEnum is null)
                    return Array.Empty<ClrHandleInfo>();

                foreach (HandleData handle in handleEnum.ReadHandles())
                {
                    raw ??= new List<HandleData>();
                    raw.Add(handle);
                }
            }

            if (raw is null)
                return Array.Empty<ClrHandleInfo>();

            ClrHandleInfo[] results = new ClrHandleInfo[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                HandleData handle = raw[i];
                ulong handleAddr = handle.Handle.ToAddress(_target);
                results[i] = new ClrHandleInfo()
                {
                    Address = handleAddr,
                    Object = _dataReader.ReadPointer(handleAddr),
                    Kind = (ClrHandleKind)handle.Type,
                    AppDomain = handle.AppDomain.ToAddress(_target),
                    DependentTarget = handle.Secondary.ToAddress(_target),
                    RefCount = handle.IsPegged != 0 ? handle.JupiterRefCount : handle.RefCount,
                };
            }

            return results;
        }

        public IEnumerable<JitManagerInfo> EnumerateClrJitManagers()
        {
            List<JitManagerInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                foreach (JitManagerData jitMgr in _sos.GetJitManagers())
                {
                    results ??= new List<JitManagerInfo>();
                    results.Add(new()
                    {
                        Address = jitMgr.Address.ToAddress(_target),
                        Kind = jitMgr.Kind,
                        HeapList = jitMgr.HeapList.ToAddress(_target),
                    });
                }
            }

            return (IEnumerable<JitManagerInfo>?)results ?? Array.Empty<JitManagerInfo>();
        }

        public string? GetJitHelperFunctionName(ulong address)
        {
            lock (_sos.SyncRoot)
                return _sos.GetJitHelperFunctionName(ClrDataAddress.FromTargetAddress(address, _target));
        }
    }
}