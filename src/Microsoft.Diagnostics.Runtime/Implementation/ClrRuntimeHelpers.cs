// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed unsafe class ClrRuntimeHelpers : IClrModuleHelpers, IClrRuntimeData
    {
        private ClrRuntime? _runtime;

        private readonly IDataReader _dataReader;
        private readonly ThreadStoreData _threadStore;
        private readonly AppDomainStoreData _domainStore;
        private readonly ClrInfo _clrInfo;
        private readonly DacLibrary _library;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly ISOSDac13? _sos13;
        private readonly CacheOptions _cacheOptions;
        private IClrNativeHeapHelpers? _nativeHeapHelpers;

        public ClrRuntimeHelpers(ClrInfo clrInfo, DacLibrary library, CacheOptions cacheOptions)
        {
            _clrInfo = clrInfo;
            _dataReader = clrInfo.DataTarget.DataReader;
            _library = library;
            _dac = library.DacPrivateInterface;
            _sos = library.SOSDacInterface;
            _sos6 = library.SOSDacInterface6;
            _sos8 = library.SOSDacInterface8;
            _sos12 = library.SOSDacInterface12;
            _sos13 = library.SOSDacInterface13;
            _cacheOptions = cacheOptions;

            int version = 0;
            if (!_dac.Request(DacRequests.VERSION, ReadOnlySpan<byte>.Empty, new Span<byte>(&version, sizeof(int))))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.  Failed to request DacVersion.");

            if (version != 9)
                throw new NotSupportedException($"The CLR debugging layer reported a version of {version} which this build of ClrMD does not support.");

            if (!_sos.GetThreadStoreData(out _threadStore))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request ThreadStoreData.");

            if (!_sos.GetAppDomainStoreData(out _domainStore))
                throw new InvalidDataException("This instance of CLR either has not been initialized or does not contain any data.    Failed to request AppDomainStoreData.");

            library.DacDataTarget.SetMagicCallback(_dac.Flush);
        }

        public void Dispose()
        {
            Flush();
            _dac.Dispose();
            _sos.Dispose();
            _sos6?.Dispose();
            _sos8?.Dispose();
            _sos12?.Dispose();
            _sos13?.Dispose();
            _library.Dispose();
        }

        // IClrModuleHelpers
        private const int mdtTypeDef = 0x02000000;
        private const int mdtTypeRef = 0x01000000;
        public IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefMap(ulong module) => GetModuleMap(module, SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable);

        public IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeRefMap(ulong module) => GetModuleMap(module, SOSDac.ModuleMapTraverseKind.TypeRefToMethodTable);

        private List<(ulong MethodTable, int Token)> GetModuleMap(ulong module, SOSDac.ModuleMapTraverseKind kind)
        {
            int tokenType = kind == SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable ? mdtTypeDef : mdtTypeRef;
            List<(ulong MethodTable, int Token)> result = new();
            _sos.TraverseModuleMap(kind, module, (token, mt, _) => {
                result.Add((mt, token | tokenType));
            });

            return result;
        }

        public MetadataImport? GetMetadataImport(ulong module) => _sos.GetMetadataImport(module);

        // IClrRuntimeHelpers
        public IClrModuleHelpers ModuleHelpers => this;

        public IClrNativeHeapHelpers NativeHeapHelpers
        {
            get
            {
                IClrNativeHeapHelpers? helpers = _nativeHeapHelpers;
                if (helpers is null)
                {
                    // We don't care if this races
                    helpers = new ClrNativeHeapHelpers(_clrInfo, _sos, _sos13, _dataReader);
                    _nativeHeapHelpers = helpers;
                }

                return helpers;
            }
        }

        public IClrHeapHelpers GetHeapHelpers() => new ClrHeapHelpers(_dac, _sos, _sos6, _sos8, _sos12, _dataReader, _cacheOptions);

        public ClrRuntime Runtime
        {
            get
            {
                if (_runtime is null)
                    throw new InvalidOperationException($"Must set {nameof(ClrRuntimeHelpers)}.{nameof(Runtime)} before using it!");

                return _runtime;
            }
            set
            {
                if (_runtime is not null && _runtime != value)
                    throw new InvalidOperationException($"Cannot change {nameof(ClrRuntimeHelpers)}.{nameof(Runtime)}!");

                _runtime = value;
            }
        }

        public void Flush()
        {
            _nativeHeapHelpers = null;
            FlushDac();
        }

        private void FlushDac()
        {
            if (_sos13 is not null && _sos13.LockedFlush())
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                // On Linux/MacOS, skip the above workaround because calling Flush() in the DAC data target's
                // ReadVirtual function can cause a SEGSIGV because of an access of freed memory causing the
                // tool/app running CLRMD to crash. On Windows, it would be caught by the SEH try/catch handler
                // in DAC enter/leave code.

                _dac.Flush();
            }
        }

        public IEnumerable<AppDomainInfo> EnumerateAppDomains()
        {
            if (_domainStore.SharedDomain != 0)
                yield return CreateAppDomainInfo(_domainStore.SharedDomain, AppDomainKind.Shared, "Shared Domain");

            if (_domainStore.SystemDomain != 0)
                yield return CreateAppDomainInfo(_domainStore.SystemDomain, AppDomainKind.System, "System Domain");

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

        public ClrModuleInfo GetModuleInfo(ulong moduleAddress)
        {
            _sos.GetModuleData(moduleAddress, out ModuleData data);

            ClrModuleInfo result = new()
            {
                Address = moduleAddress,
                Assembly = data.Assembly,
                AssemblyName = _sos.GetAssemblyName(data.Assembly),
                IsPEFile = data.IsPEFile != 0,
                ImageBase = data.ILBase,
                MetadataAddress = data.MetadataStart,
                MetadataSize = data.MetadataSize,
                IsDynamic = data.IsReflection != 0,
                ThunkHeap = data.ThunkHeap,
                LoaderAllocator = data.LoaderAllocator,
            };

            using ClrDataModule? dataModule = _sos.GetClrDataModule(moduleAddress);
            if (dataModule is not null && dataModule.GetModuleData(out DacInterface.ExtendedModuleData extended))
            {
                result.Layout = extended.IsFlatLayout != 0 ? ModuleLayout.Flat : ModuleLayout.Mapped;
                result.IsDynamic |= extended.IsDynamic != 0;
                result.Size = extended.LoadedPESize;
                result.FileName = dataModule.GetFileName();
            }

            return result;
        }

        public ClrMethod? GetMethodByMethodDesc(ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return null;

            ClrType? type = Runtime.Heap.GetTypeByMethodTable(mdData.MethodTable);
            if (type is null)
                return null;

            return type.Methods.FirstOrDefault(m => m.MethodDesc == methodDesc);
        }

        public ClrMethod? GetMethodByInstructionPointer(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (!_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    return null;

                if ((md = codeHeaderData.MethodDesc) == 0)
                    return null;
            }

            return GetMethodByMethodDesc(md);
        }

        public IEnumerable<IClrThreadData> EnumerateThreads()
        {
            HashSet<ulong> seen = new() { 0 };
            ulong addr = _threadStore.FirstThread;
            for (int i = 0; i < _threadStore.ThreadCount && seen.Add(addr); i++)
            {
                DacThreadData threadData = new DacThreadData(_dac, _sos, _dataReader, addr, _threadStore);
                yield return threadData;
                addr = threadData.NextThread;
            }
        }

        public IEnumerable<ClrJitManager> EnumerateClrJitManagers()
        {
            foreach (JitManagerInfo jitMgr in _sos.GetJitManagers())
                yield return new ClrJitManager(Runtime, jitMgr, NativeHeapHelpers);
        }

        public IEnumerable<HandleInfo> EnumerateHandles()
        {
            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles();
            if (handleEnum is null)
                yield break;

            ClrHeap heap = Runtime.Heap;
            foreach (HandleData handle in handleEnum.ReadHandles())
            {
                yield return new HandleInfo()
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

        public IEnumerable<ClrNativeHeapInfo> EnumerateGCFreeRegions()
        {
            using (SosMemoryEnum? memoryEnum = _sos13?.GetGCFreeRegions())
            {
                if (memoryEnum is not null)
                    foreach (SosMemoryRegion mem in memoryEnum)
                    {
                        NativeHeapKind kind = (ulong)mem.ExtraData switch
                        {
                            1 => NativeHeapKind.GCFreeGlobalHugeRegion,
                            2 => NativeHeapKind.GCFreeGlobalRegion,
                            3 => NativeHeapKind.GCFreeRegion,
                            4 => NativeHeapKind.GCFreeSohSegment,
                            5 => NativeHeapKind.GCFreeUohSegment,
                            _ => NativeHeapKind.GCFreeRegion
                        };

                        ulong raw = (ulong)mem.Start;
                        ulong start = raw & ~0xfful;
                        ulong diff = raw - start;
                        ulong len = mem.Length + diff;

                        yield return new ClrNativeHeapInfo(MemoryRange.CreateFromLength(start, len), kind, ClrNativeHeapState.Inactive, mem.Heap);
                    }
            }
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateHandleTableRegions()
        {
            using (SosMemoryEnum? memoryEnum = _sos13?.GetHandleTableRegions())
            {
                if (memoryEnum is not null)
                    foreach (SosMemoryRegion mem in memoryEnum)
                        yield return new ClrNativeHeapInfo(MemoryRange.CreateFromLength(mem.Start, mem.Length), NativeHeapKind.HandleTable, ClrNativeHeapState.Active, mem.Heap);
            }
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateGCBookkeepingRegions()
        {
            using (SosMemoryEnum? memoryEnum = _sos13?.GetGCBookkeepingMemoryRegions())
            {
                if (memoryEnum is not null)
                    foreach (SosMemoryRegion mem in memoryEnum)
                        yield return new ClrNativeHeapInfo(MemoryRange.CreateFromLength(mem.Start, mem.Length), NativeHeapKind.GCBookkeeping, ClrNativeHeapState.RegionOfRegions);
            }
        }

        public string? GetJitHelperFunctionName(ulong address) => _sos.GetJitHelperFunctionName(address);

        public ClrThreadPool? GetThreadPool()
        {
            ClrThreadPoolHelper helper = new(_sos);
            ClrThreadPool result = new(Runtime, helper);
            return result.Initialized ? result : null;
        }

        public IEnumerable<ClrSyncBlockCleanupData> EnumerateSyncBlockCleanupData()
        {
            ulong loopCheck = 0;
            while (_sos.GetSyncBlockCleanupData(0, out SyncBlockCleanupData data))
            {
                if (loopCheck == 0)
                    loopCheck = data.NextSyncBlock;
                else if (loopCheck == data.NextSyncBlock)
                    break;

                yield return new(data.SyncBlockPointer, data.BlockRCW, data.BlockCCW, data.BlockClassFactory);
            }
        }

        public IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData()
        {
            return _sos.EnumerateRCWCleanup(0).Select(r => new ClrRcwCleanupData(r.Rcw, r.Context, r.Thread, r.IsFreeThreaded));
        }
    }
}