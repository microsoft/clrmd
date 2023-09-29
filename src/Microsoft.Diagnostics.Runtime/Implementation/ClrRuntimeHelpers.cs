// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using GCKind = Microsoft.Diagnostics.Runtime.AbstractDac.GCKind;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed unsafe class ClrRuntimeHelpers : IClrModuleHelpers, IClrRuntimeHelpers, IClrThreadHelpers, IClrThreadPoolHelpers
    {
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

        ////////////////////////////////////////////////////////////////////////////////
        // Heaps
        ////////////////////////////////////////////////////////////////////////////////
        #region Heaps
        public bool GetGCState(out GCState state)
        {
            if (!_sos.GetGCHeapData(out GCInfo gcInfo) || !_sos.GetCommonMethodTables(out CommonMethodTables commonMethodTables))
            {
                state = default;
                return false;
            }

            state = new()
            {
                Kind = gcInfo.ServerMode != 0 ? GCKind.Server : GCKind.Workstation,
                AreGCStructuresValid = gcInfo.GCStructuresValid != 0,
                HeapCount = gcInfo.HeapCount,
                MaxGeneration = gcInfo.MaxGeneration,
                ExceptionMethodTable = commonMethodTables.ExceptionMethodTable,
                FreeMethodTable = commonMethodTables.FreeMethodTable,
                ObjectMethodTable = commonMethodTables.ObjectMethodTable,
                StringMethodTable = commonMethodTables.StringMethodTable,
            };

            return state.ObjectMethodTable != 0;
        }


        public IClrHeapHelpers GetHeapHelpers() => new ClrHeapHelpers(this, _sos, _sos8, _sos12, _dataReader, _cacheOptions);

        public IClrTypeHelpers GetClrTypeHelpers() => new ClrTypeHelpers(_dac, _sos, _sos6, _sos8, _dataReader);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // Threads
        ////////////////////////////////////////////////////////////////////////////////
        #region Threads
        public IClrThreadHelpers ThreadHelpers => this;

        public IEnumerable<ClrThreadInfo> EnumerateThreads()
        {
            HashSet<ulong> seen = new() { 0 };
            ulong threadAddress = _threadStore.FirstThread;

            uint pointerSize = (uint)_dataReader.PointerSize;

            for (int i = 0; i < _threadStore.ThreadCount && seen.Add(threadAddress); i++)
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
                    IsFinalizer = _threadStore.FinalizerThread == threadAddress,
                    IsGC = _threadStore.GCThread == threadAddress,
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

        // IClrThreadHelpers
        public IEnumerable<StackRootInfo> EnumerateStackRoots(uint osThreadId)
        {
            using SOSStackRefEnum? stackRefEnum = _sos.EnumerateStackRefs(osThreadId);
            if (stackRefEnum is null)
                yield break;

            const int GCInteriorFlag = 1;
            const int GCPinnedFlag = 2;
            const int SOS_StackSourceIP = 0;
            const int SOS_StackSourceFrame = 1;
            foreach (StackRefData stackRef in stackRefEnum.ReadStackRefs())
            {
                if (stackRef.Object == 0)
                {
                    Trace.TraceInformation($"EnumerateStackRoots found an entry with Object == 0, addr:{(ulong)stackRef.Address:x} srcType:{stackRef.SourceType:x}");
                    continue;
                }

                bool interior = (stackRef.Flags & GCInteriorFlag) == GCInteriorFlag;
                bool isPinned = (stackRef.Flags & GCPinnedFlag) == GCPinnedFlag;
                int regOffset = 0;
                string? regName = null;
                if (stackRef.HasRegisterInformation != 0)
                {
                    regOffset = stackRef.Offset;
                    regName = _sos.GetRegisterName(stackRef.Register);
                }

                ulong ip = 0;
                ulong frame = 0;
                if (stackRef.SourceType == SOS_StackSourceIP)
                    ip = stackRef.Source;
                else if (stackRef.SourceType == SOS_StackSourceFrame)
                    frame = stackRef.Source;

                yield return new StackRootInfo()
                {
                    InstructionPointer = ip,
                    StackPointer = stackRef.StackPointer,
                    InternalFrame = frame,

                    IsInterior = interior,
                    IsPinned = isPinned,

                    Address = stackRef.Address,
                    Object = stackRef.Object,

                    IsEnregistered = stackRef.HasRegisterInformation != 0,
                    RegisterName = regName,
                    RegisterOffset = regOffset,
                };
            }
        }

        public IEnumerable<StackFrameInfo> EnumerateStackTrace(uint osThreadId, bool includeContext)
        {
            using ClrStackWalk? stackwalk = _dac.CreateStackWalk(osThreadId, 0xf);
            if (stackwalk is null)
                yield break;

            int ipOffset;
            int spOffset;
            int contextSize;
            uint contextFlags = 0;
            if (_dataReader.Architecture == Architecture.Arm)
            {
                ipOffset = 64;
                spOffset = 56;
                contextSize = 416;
            }
            else if (_dataReader.Architecture == Architecture.Arm64)
            {
                ipOffset = 264;
                spOffset = 256;
                contextSize = 912;
            }
            else if (_dataReader.Architecture == Architecture.X86)
            {
                ipOffset = 184;
                spOffset = 196;
                contextSize = 716;
                contextFlags = 0x1003f;
            }
            else // Architecture.X64
            {
                ipOffset = 248;
                spOffset = 152;
                contextSize = 1232;
                contextFlags = 0x10003f;
            }

            HResult hr = HResult.S_OK;
            byte[] context = ArrayPool<byte>.Shared.Rent(contextSize);
            while (hr.IsOK)
            {
                hr = stackwalk.GetContext(contextFlags, contextSize, out _, context);
                if (!hr)
                {
                    Trace.TraceInformation($"GetContext failed, flags:{contextFlags:x} size: {contextSize:x} hr={hr}");
                    break;
                }

                ulong ip = context.AsSpan().AsPointer(ipOffset);
                ulong sp = context.AsSpan().AsPointer(spOffset);

                ulong frameVtbl = stackwalk.GetFrameVtable();
                string? frameName = null;
                ulong frameMethod = 0;
                if (frameVtbl != 0)
                {
                    sp = frameVtbl;
                    frameVtbl = _dataReader.ReadPointer(sp);
                    frameName = _sos.GetFrameName(frameVtbl);
                    frameMethod = _sos.GetMethodDescPtrFromFrame(sp);
                }

                byte[]? contextCopy = null;
                if (includeContext)
                {
                    contextCopy = new byte[contextSize];
                    context.AsSpan(0, contextSize).CopyTo(contextCopy);
                }

                yield return new StackFrameInfo()
                {
                    InstructionPointer = ip,
                    StackPointer = sp,
                    Context = contextCopy,
                    InternalFrameVTable = frameVtbl,
                    InternalFrameName = frameName,
                    InnerMethodMethodHandle = frameMethod,
                };

                hr = stackwalk.Next();
                if (!hr)
                    Trace.TraceInformation($"STACKWALK FAILED - hr:{hr}");
            }

            ArrayPool<byte>.Shared.Return(context);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // AppDomains
        ////////////////////////////////////////////////////////////////////////////////
        #region AppDomains
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
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // Modules
        ////////////////////////////////////////////////////////////////////////////////
        #region Modules
        public IClrModuleHelpers ModuleHelpers => this;

        public IEnumerable<ulong> GetModuleList(ulong domain) => _sos.GetAssemblyList(domain).SelectMany(assembly => _sos.GetModuleList(assembly)).Select(module => (ulong)module);

        public ClrModuleInfo GetModuleInfo(ulong moduleAddress)
        {
            _sos.GetModuleData(moduleAddress, out ModuleData data);

            ClrModuleInfo result = new()
            {
                Address = moduleAddress,
                Assembly = data.Assembly,
                AssemblyName = _sos.GetAssemblyName(data.Assembly),
                Id = data.ModuleID,
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
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // Methods
        ////////////////////////////////////////////////////////////////////////////////
        #region Methods
        public ulong GetMethodHandleContainingType(ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return 0;

            return mdData.MethodTable;
        }

        public ulong GetMethodHandleByInstructionPointer(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    md = codeHeaderData.MethodDesc;
            }

            return md;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // HandleTable
        ////////////////////////////////////////////////////////////////////////////////
        #region HandleTable
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
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // JIT
        ////////////////////////////////////////////////////////////////////////////////
        #region JIT
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
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // ThreadPool
        ////////////////////////////////////////////////////////////////////////////////
        #region ThreadPool
        public IClrThreadPoolHelpers? LegacyThreadPoolHelpers => this;

        public bool GetLegacyThreadPoolData(out ThreadPoolData data)
        {
            HResult hr = _sos.GetThreadPoolData(out data);
            return hr;
        }

        public bool GetLegacyWorkRequestData(ulong workRequest, out WorkRequestData workRequestData)
        {
            return _sos.GetWorkRequestData(workRequest, out workRequestData);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // Native Heaps
        ////////////////////////////////////////////////////////////////////////////////
        #region Native Heaps
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

        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // COM
        ////////////////////////////////////////////////////////////////////////////////
        #region COM
        public IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData()
        {
            return _sos.EnumerateRCWCleanup(0).Select(r => new ClrRcwCleanupData(r.Rcw, r.Context, r.Thread, r.IsFreeThreaded));
        }

        public bool GetCcwInfo(ulong obj, out CcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(obj, out ObjectData objData) &&
                objData.CCW != 0 &&
                _sos.GetCCWData(objData.CCW, out CcwData ccwData))
            {
                COMInterfacePointerData[]? pointers = _sos.GetCCWInterfaces(objData.CCW, ccwData.InterfaceCount);
                info = new()
                {
                    Address = objData.CCW,
                    IUnknown = ccwData.OuterIUnknown,
                    Object = ccwData.ManagedObject,
                    Handle = ccwData.Handle,
                    RefCount = ccwData.RefCount,
                    JupiterRefCount = ccwData.JupiterRefCount,
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer, MethodTable = r.MethodTable }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }

        public bool GetRcwInfo(ulong obj, out RcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(obj, out ObjectData objData) &&
                objData.RCW != 0 &&
                _sos.GetRCWData(objData.RCW, out RcwData rcwData))
            {
                COMInterfacePointerData[]? pointers = _sos.GetRCWInterfaces(objData.RCW, rcwData.InterfaceCount);
                info = new()
                {
                    Address = objData.RCW,
                    IUnknown = rcwData.IUnknownPointer,
                    Object = rcwData.ManagedObject,
                    RefCount = rcwData.RefCount,
                    CreatorThread = rcwData.CreatorThread,
                    IsDisconnected = rcwData.IsDisconnected != 0,
                    VTablePointer = rcwData.VTablePointer,
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer, MethodTable = r.MethodTable }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////
        // Helpers
        ////////////////////////////////////////////////////////////////////////////////
        #region Helpers
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
        #endregion
    }
}