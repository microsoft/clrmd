// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.ComWrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class V45Runtime : DesktopRuntimeBase
    {
        private SOSDac _sos;

        #region Constructor
        public V45Runtime(ClrInfo info, DataTargetImpl dt, DacLibrary lib)
            : base(info, dt, lib)
        {
            if (!GetCommonMethodTables(ref _commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsException.HR.DacError);

            if (!_commonMTs.Validate())
                CanWalkHeap = false;

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            int v = BitConverter.ToInt32(tmp, 0);
            if (v != 9)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
        }
        #endregion

        #region Overrides
        protected override void InitApi()
        {
            if (_sos == null)
                _sos = _library.SOSInterface;

            Debug.Assert(_sos != null);
        }
        internal override DesktopVersion CLRVersion
        {
            get { return DesktopVersion.v45; }
        }
        private List<ClrHandle> _handles;

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            if (_handles != null)
                return _handles;

            return EnumerateHandleWorker();
        }
        private IEnumerable<ClrHandle> EnumerateHandleWorker()
        {
            Debug.Assert(_handles == null);
            List<ClrHandle> result = new List<ClrHandle>();

            using (SOSHandleEnum handleEnum = _sos.EnumerateHandles())
            {
                HandleData[] handles = new HandleData[8];
                int fetched = 0;

                while ((fetched = handleEnum.ReadHandles(handles)) != 0)
                {
                    for (int i = 0; i < fetched; i++)
                    {
                        ClrHandle handle = new ClrHandle(this, Heap, handles[i]);
                        _handles.Add(handle);
                        yield return handle;

                        handle = handle.GetInteriorHandle();
                        if (handle != null)
                        {
                            _handles.Add(handle);
                            yield return handle;
                        }
                    }
                }
            }

            _handles = result;
        }


        internal override Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken)
        {
            Dictionary<ulong, List<ulong>> result = new Dictionary<ulong, List<ulong>>();

            using (SOSHandleEnum handleEnum = _sos.EnumerateHandles())
            {
                if (handleEnum == null)
                    return result;

                HandleData[] handles = new HandleData[32];

                int fetched;
                while ((fetched = handleEnum.ReadHandles(handles)) != 0)
                {
                    for (int i = 0; i < fetched; i++)
                    {
                        cancelToken.ThrowIfCancellationRequested();

                        HandleType type = (HandleType)handles[i].Type;
                        if (type != HandleType.Dependent)
                            continue;

                        if (ReadPointer(handles[i].Handle, out ulong address))
                        {
                            if (!result.TryGetValue(address, out List<ulong> value))
                                result[address] = value = new List<ulong>();

                            value.Add(handles[i].Secondary);
                        }
                    }
                }

                return result;
            }
        }

        internal override IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
        {
            if (includeDead)
                return base.EnumerateStackReferences(thread, includeDead);

            return EnumerateStackReferencesWorker(thread);
        }

        private IEnumerable<ClrRoot> EnumerateStackReferencesWorker(ClrThread thread)
        {
            using (SOSStackRefEnum stackRefEnum = _sos.EnumerateStackRefs(thread.OSThreadId))
            {
                if (stackRefEnum == null)
                    yield break;

                ClrAppDomain domain = GetAppDomainByAddress(thread.AppDomain);
                var heap = Heap;
                StackRefData[] refs = new StackRefData[1024];

                const int GCInteriorFlag = 1;
                const int GCPinnedFlag = 2;
                int fetched = 0;
                while ((fetched = stackRefEnum.ReadStackReferences(refs)) != 0)
                {
                    for (uint i = 0; i < fetched && i < refs.Length; ++i)
                    {
                        if (refs[i].Object == 0)
                            continue;

                        bool pinned = (refs[i].Flags & GCPinnedFlag) == GCPinnedFlag;
                        bool interior = (refs[i].Flags & GCInteriorFlag) == GCInteriorFlag;

                        ClrType type = null;

                        if (!interior)
                            type = heap.GetObjectType(refs[i].Object);

                        ClrStackFrame frame = thread.StackTrace.SingleOrDefault(f => f.StackPointer == refs[i].Source || (f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source));

                        if (interior || type != null)
                            yield return new LocalVarRoot(refs[i].Address, refs[i].Object, type, domain, thread, pinned, false, interior, frame);
                    }
                }
            }
        }

        internal override ulong GetFirstThread()
        {
            IThreadStoreData threadStore = GetThreadStoreData();
            return threadStore != null ? threadStore.FirstThread : 0;
        }

        internal override IThreadData GetThread(ulong addr) => _sos.GetThreadData(addr);

        internal override IHeapDetails GetSvrHeapDetails(ulong addr) => _sos.GetServerHeapDetails(addr);
        internal override IHeapDetails GetWksHeapDetails() => _sos.GetWksHeapDetails();

        internal override ulong[] GetServerHeapList() => _sos.GetHeapList(HeapCount);

        internal override ulong[] GetAppDomainList(int count) => _sos.GetAppDomainList();

        internal override ulong[] GetAssemblyList(ulong appDomain, int count) => _sos.GetAssemblyList(appDomain, count);

        internal override ulong[] GetModuleList(ulong assembly, int count) => _sos.GetModuleList(assembly, count);

        internal override IAssemblyData GetAssemblyData(ulong domain, ulong assembly) => _sos.GetAssemblyData(domain, assembly);

        internal override IAppDomainStoreData GetAppDomainStoreData() => _sos.GetAppDomainStoreData();

        internal override IMethodTableData GetMethodTableData(ulong addr) => _sos.GetMethodTableData(addr);

        internal override ulong GetMethodTableByEEClass(ulong eeclass) => _sos.GetMethodTableByEEClass(eeclass);

        internal override IGCInfo GetGCInfoImpl() => _sos.GetGcHeapData();

        internal override bool GetCommonMethodTables(ref CommonMethodTables mts) => _sos.GetCommonMethodTables(out mts);

        internal override string GetNameForMT(ulong mt) => _sos.GetMethodTableName(mt);

        internal override string GetPEFileName(ulong addr) => _sos.GetPEFileName(addr);

        internal override IModuleData GetModuleData(ulong addr) => _sos.GetModuleData(addr);

        internal override ulong GetModuleForMT(ulong addr) => _sos.GetMethodTableData(addr)?.Module ?? 0;

        internal override ISegmentData GetSegmentData(ulong addr) => _sos.GetSegmentData(addr);

        internal override IAppDomainData GetAppDomainData(ulong addr) => _sos.GetAppDomainData(addr);

        internal override string GetAppDomaminName(ulong addr) => _sos.GetAppDomainName(addr);

        internal override string GetAssemblyName(ulong addr) => _sos.GetAssemblyName(addr);

        internal override bool TraverseHeap(ulong heap, LoaderHeapTraverse callback) => _sos.TraverseLoaderHeap(heap, callback);

        internal override bool TraverseStubHeap(ulong appDomain, int type, LoaderHeapTraverse callback) => _sos.TraverseStubHeap(appDomain, type, callback);

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            LegacyJitManagerInfo[] jitManagers = _sos.GetJitManagers();
            for (int i = 0; i < jitManagers.Length; ++i)
            {
                if (jitManagers[i].type != CodeHeapType.Unknown)
                    continue;

                LegacyJitCodeHeapInfo[] heapInfo = _sos.GetCodeHeapList(jitManagers[i].addr);

                for (int j = 0; j < heapInfo.Length; ++j)
                    yield return heapInfo[i];
            }
        }

        internal override IFieldInfo GetFieldInfo(ulong mt) => _sos.GetFieldInfo(mt);

        internal override IFieldData GetFieldData(ulong fieldDesc) => _sos.GetFieldData(fieldDesc);

        internal override ICorDebug.IMetadataImport GetMetadataImport(ulong module) => _sos.GetMetadataImport(module);

        internal override IObjectData GetObjectData(ulong objRef) => _sos.GetObjectData(objRef);

        internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
        {
            List<MethodTableTokenPair> mts = new List<MethodTableTokenPair>();
            _sos.TraverseModuleMap(0, module, new ModuleMapTraverse(delegate (uint index, ulong mt, IntPtr token)
            {
                mts.Add(new MethodTableTokenPair(mt, index));
            }));

            return mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id) => _sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)id);

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count) => _sos.GetCCWInterfaces(ccw, count);

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count) => _sos.GetRCWInterfaces(rcw, count);

        internal override ICCWData GetCCWData(ulong ccw) => _sos.GetCCWData(ccw);

        internal override IRCWData GetRCWData(ulong rcw) => _sos.GetRCWData(rcw);
        #endregion

        internal override ulong GetILForModule(ClrModule module, uint rva) => _sos.GetILForModule(module.Address, rva);

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            ulong addr = offset;

            if (!_sos.GetThreadLocalModuleData(thread, moduleId, out V45ThreadLocalModuleData data))
                return 0;

            if (IsObjectReference(type) || IsValueClass(type))
                addr += data.pGCStaticDataStart;
            else
                addr += data.pNonGCStaticDataStart;

            return addr;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong module) => _sos.GetDomainLocalModuleDataFromModule(module);

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            IMethodTableData mtData = _sos.GetMethodTableData(methodTable);
            if (mtData ==  null)
                return null;

            uint numMethods = mtData.NumMethods;
            ulong[] mds = new ulong[numMethods];

            for (uint i = 0; i < numMethods; ++i)
                if (_sos.GetCodeHeaderData(_sos.GetMethodTableSlot(methodTable, i), out CodeHeaderData header))
                    mds[i] = header.MethodDescPtr;

            return mds;
        }

        internal override string GetNameForMD(ulong md) => _sos.GetMethodDescName(md);

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            V45MethodDescDataWrapper wrapper = new V45MethodDescDataWrapper();
            if (!wrapper.Init(_sos, md))
                return null;

            return wrapper;
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            IMethodTableData data = _sos.GetMethodTableData(mt);
            return data?.Token ?? uint.MaxValue;
        }

        protected override DesktopStackFrame GetStackFrame(DesktopThread thread, ulong ip, ulong framePtr, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            if (frameVtbl != 0)
            {
                ClrMethod innerMethod = null;
                string frameName = _sos.GetFrameName(frameVtbl);

                ulong md = _sos.GetMethodDescPtrFromFrame(framePtr);
                if (md != 0)
                {
                    V45MethodDescDataWrapper mdData = new V45MethodDescDataWrapper();
                    if (mdData.Init(_sos, md))
                        innerMethod = DesktopMethod.Create(this, mdData);
                }

                frame = new DesktopStackFrame(this, thread, framePtr, frameName, innerMethod);
            }
            else
            {
                frame = new DesktopStackFrame(this, thread, ip, framePtr, _sos.GetMethodDescPtrFromIP(ip));
            }

            return frame;
        }

        private bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
        {
            stackTrace = 0;
            var field = type.GetFieldByName("_stackTrace");
            if (field == null)
                return false;

            object tmp = field.GetValue(obj);
            if (tmp == null || !(tmp is ulong))
                return false;

            stackTrace = (ulong)tmp;
            return true;
        }


        internal override IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type)
        {
            // TODO: Review this and if it works on v4.5, merge the two implementations back into RuntimeBase.
            List<ClrStackFrame> result = new List<ClrStackFrame>();
            if (type == null)
                return result;

            if (!GetStackTraceFromField(type, obj, out ulong _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            ClrHeap heap = Heap;
            ClrType stackTraceType = heap.GetObjectType(_stackTrace);
            if (stackTraceType == null || !stackTraceType.IsArray)
                return result;

            int len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!ReadPointer(dataPtr, out ulong count))
                return result;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            DesktopThread thread = null;
            for (int i = 0; i < (int)count; ++i)
            {
                if (!ReadPointer(dataPtr, out ulong ip))
                    break;
                if (!ReadPointer(dataPtr + (ulong)IntPtr.Size, out ulong sp))
                    break;
                if (!ReadPointer(dataPtr + (ulong)(2 * IntPtr.Size), out ulong md))
                    break;

                if (i == 0 && sp != 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                // it seems that the first frame often has 0 for IP and SP.  Try the 2nd frame as well
                if (i == 1 && thread == null && sp != 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                result.Add(new DesktopStackFrame(this, thread, ip, sp, md));

                dataPtr += (ulong)elementSize;
            }

            return result;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            if (!_sos.GetThreadStoreData(out LegacyThreadStoreData data))
                return null;

            return data;
        }

        internal override string GetAppBase(ulong appDomain) => _sos.GetAppBase(appDomain);
        internal override string GetConfigFile(ulong appDomain) => _sos.GetConfigFile(appDomain);

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (!_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    return null;

                if ((md = codeHeaderData.MethodDescPtr) == 0)
                    return null;
            }

            V45MethodDescDataWrapper mdWrapper = new V45MethodDescDataWrapper();
            if (!mdWrapper.Init(_sos, md))
                return null;

            return mdWrapper;
        }

        protected override ulong GetThreadFromThinlock(uint threadId) => _sos.GetThreadFromThinlockId(threadId);

        internal override int GetSyncblkCount() => (int)(_sos.GetSyncBlockData(1)?.TotalCount ?? 0);

        internal override ISyncBlkData GetSyncblkData(int index) => _sos.GetSyncBlockData(index + 1);

        internal override IThreadPoolData GetThreadPoolData() => _sos.GetThreadPoolData();

        internal override uint GetTlsSlot() => _sos.GetTlsIndex();

        internal override uint GetThreadTypeIndex()
        {
            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (PointerSize == 8)
                return 0x30;
            else
                return 0x18;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            IThreadPoolData data = _sos.GetThreadPoolData();
            ulong request = data.FirstWorkRequest;
            while (request != 0)
            {
                if (!_sos.GetWorkRequestData(request, out V45WorkRequestData requestData))
                    break;

                yield return new DesktopNativeWorkItem(requestData);
                request = requestData.NextWorkRequest;
            }
        }

        internal override uint GetStringFirstCharOffset()
        {
            if (PointerSize == 8)
                return 0xc;

            return 8;
        }

        internal override uint GetStringLengthOffset()
        {
            if (PointerSize == 8)
                return 0x8;

            return 0x4;
        }

        internal override uint GetExceptionHROffset()
        {
            return PointerSize == 8 ? 0x8cu : 0x40u;
        }
    }


    #region V45 Dac Interface

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void ModuleMapTraverse(uint index, ulong methodTable, IntPtr token);
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("436f00f2-b42a-4b9f-870c-e73db66ae930")]

    internal interface ISOSDac
    {
        // ThreadStore
        [PreserveSig]
        int GetThreadStoreData(out LegacyThreadStoreData data);

        // AppDomains
        [PreserveSig]
        int GetAppDomainStoreData(out LegacyAppDomainStoreData data);
        [PreserveSig]
        int GetAppDomainList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]ulong[] values, out uint pNeeded);
        [PreserveSig]
        int GetAppDomainData(ulong addr, out LegacyAppDomainData data);
        [PreserveSig]
        int GetAppDomainName(ulong addr, uint count, [Out]StringBuilder lpFilename, out uint pNeeded);
        [PreserveSig]
        int GetDomainFromContext(ulong context, out ulong domain);

        // Assemblies
        [PreserveSig]
        int GetAssemblyList(ulong appDomain, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] values, out int pNeeded);
        [PreserveSig]
        int GetAssemblyData(ulong baseDomainPtr, ulong assembly, out LegacyAssemblyData data);
        [PreserveSig]
        int GetAssemblyName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);

        // Modules
        [PreserveSig]
        int GetModule(ulong addr, [Out, MarshalAs(UnmanagedType.IUnknown)] out object module);
        [PreserveSig]
        int GetModuleData(ulong moduleAddr, out V45ModuleData data);
        [PreserveSig]
        int TraverseModuleMap(int mmt, ulong moduleAddr, [In, MarshalAs(UnmanagedType.FunctionPtr)] ModuleMapTraverse pCallback, IntPtr token);
        [PreserveSig]
        int GetAssemblyModuleList(ulong assembly, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] modules, out uint pNeeded);
        [PreserveSig]
        int GetILForModule(ulong moduleAddr, uint rva, out ulong il);

        // Threads
        [PreserveSig]
        int GetThreadData(ulong thread, out V4ThreadData data);
        [PreserveSig]
        int GetThreadFromThinlockID(uint thinLockId, out ulong pThread);
        [PreserveSig]
        int GetStackLimits(ulong threadPtr, out ulong lower, out ulong upper, out ulong fp);

        // MethodDescs
        [PreserveSig]
        int GetMethodDescData(ulong methodDesc, ulong ip, out V45MethodDescData data, uint cRevertedRejitVersions, V45ReJitData[] rgRevertedRejitData, out ulong pcNeededRevertedRejitData);
        [PreserveSig]
        int GetMethodDescPtrFromIP(ulong ip, out ulong ppMD);
        [PreserveSig]
        int GetMethodDescName(ulong methodDesc, uint count, [Out] StringBuilder name, out uint pNeeded);
        [PreserveSig]
        int GetMethodDescPtrFromFrame(ulong frameAddr, out ulong ppMD);
        [PreserveSig]
        int GetMethodDescFromToken(ulong moduleAddr, uint token, out ulong methodDesc);
        [PreserveSig]
        int GetMethodDescTransparencyData_do_not_use();//(ulong methodDesc, out DacpMethodDescTransparencyData data);

        // JIT Data
        [PreserveSig]
        int GetCodeHeaderData(ulong ip, out CodeHeaderData data);
        [PreserveSig]
        int GetJitManagerList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] LegacyJitManagerInfo[] jitManagers, out uint pNeeded);
        [PreserveSig]
        int GetJitHelperFunctionName(ulong ip, uint count, char name, out uint pNeeded);
        [PreserveSig]
        int GetJumpThunkTarget_do_not_use(uint ctx, out ulong targetIP, out ulong targetMD);

        // ThreadPool
        [PreserveSig]
        int GetThreadpoolData(out V45ThreadPoolData data);
        [PreserveSig]
        int GetWorkRequestData(ulong addrWorkRequest, out V45WorkRequestData data);
        [PreserveSig]
        int GetHillClimbingLogEntry_do_not_use(); //(ulong addr, out DacpHillClimbingLogEntry data);

        // Objects
        [PreserveSig]
        int GetObjectData(ulong objAddr, out V45ObjectData data);
        [PreserveSig]
        int GetObjectStringData(ulong obj, uint count, [Out] StringBuilder stringData, out uint pNeeded);
        [PreserveSig]
        int GetObjectClassName(ulong obj, uint count, [Out] StringBuilder className, out uint pNeeded);

        // MethodTable
        [PreserveSig]
        int GetMethodTableName(ulong mt, uint count, [Out] StringBuilder mtName, out uint pNeeded);
        [PreserveSig]
        int GetMethodTableData(ulong mt, out V45MethodTableData data);
        [PreserveSig]
        int GetMethodTableSlot(ulong mt, uint slot, out ulong value);
        [PreserveSig]
        int GetMethodTableFieldData(ulong mt, out V4FieldInfo data);
        [PreserveSig]
        int GetMethodTableTransparencyData_do_not_use(); //(ulong mt, out DacpMethodTableTransparencyData data);

        // EEClass
        [PreserveSig]
        int GetMethodTableForEEClass(ulong eeClass, out ulong value);

        // FieldDesc
        [PreserveSig]
        int GetFieldDescData(ulong fieldDesc, out LegacyFieldData data);

        // Frames
        [PreserveSig]
        int GetFrameName(ulong vtable, uint count, [Out] StringBuilder frameName, out uint pNeeded);


        // PEFiles
        [PreserveSig]
        int GetPEFileBase(ulong addr, [Out] out ulong baseAddr);

        [PreserveSig]
        int GetPEFileName(ulong addr, uint count, [Out] StringBuilder ptr, [Out] out uint pNeeded);

        // GC
        [PreserveSig]
        int GetGCHeapData(out LegacyGCInfo data);
        [PreserveSig]
        int GetGCHeapList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] heaps, out uint pNeeded); // svr only
        [PreserveSig]
        int GetGCHeapDetails(ulong heap, out V4HeapDetails details); // wks only
        [PreserveSig]
        int GetGCHeapStaticData(out V4HeapDetails data);
        [PreserveSig]
        int GetHeapSegmentData(ulong seg, out V4SegmentData data);
        [PreserveSig]
        int GetOOMData_do_not_use(); //(ulong oomAddr, out DacpOomData data);
        [PreserveSig]
        int GetOOMStaticData_do_not_use(); //(out DacpOomData data);
        [PreserveSig]
        int GetHeapAnalyzeData_do_not_use(); //(ulong addr, out  DacpGcHeapAnalyzeData data);
        [PreserveSig]
        int GetHeapAnalyzeStaticData_do_not_use(); //(out DacpGcHeapAnalyzeData data);

        // DomainLocal
        [PreserveSig]
        int GetDomainLocalModuleData_do_not_use(); //(ulong addr, out DacpDomainLocalModuleData data);
        [PreserveSig]
        int GetDomainLocalModuleDataFromAppDomain(ulong appDomainAddr, int moduleID, out V45DomainLocalModuleData data);
        [PreserveSig]
        int GetDomainLocalModuleDataFromModule(ulong moduleAddr, out V45DomainLocalModuleData data);

        // ThreadLocal
        [PreserveSig]
        int GetThreadLocalModuleData(ulong thread, uint index, out V45ThreadLocalModuleData data);

        // SyncBlock
        [PreserveSig]
        int GetSyncBlockData(uint number, out LegacySyncBlkData data);
        [PreserveSig]
        int GetSyncBlockCleanupData_do_not_use(); //(ulong addr, out DacpSyncBlockCleanupData data);

        // Handles
        [PreserveSig]
        int GetHandleEnum([Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);
        [PreserveSig]
        int GetHandleEnumForTypes([In] uint[] types, uint count, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);
        [PreserveSig]
        int GetHandleEnumForGC(uint gen, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);

        // EH
        [PreserveSig]
        int TraverseEHInfo_do_not_use(); //(ulong ip, DUMPEHINFO pCallback, IntPtr token);
        [PreserveSig]
        int GetNestedExceptionData(ulong exception, out ulong exceptionObject, out ulong nextNestedException);

        // StressLog
        [PreserveSig]
        int GetStressLogAddress(out ulong stressLog);

        // Heaps
        [PreserveSig]
        int TraverseLoaderHeap(ulong loaderHeapAddr, IntPtr pCallback);
        [PreserveSig]
        int GetCodeHeapList(ulong jitManager, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] LegacyJitCodeHeapInfo[] codeHeaps, out uint pNeeded);
        [PreserveSig]
        int TraverseVirtCallStubHeap(ulong pAppDomain, uint heaptype, IntPtr pCallback);

        // Other
        [PreserveSig]
        int GetUsefulGlobals(out CommonMethodTables data);
        [PreserveSig]
        int GetClrWatsonBuckets(ulong thread, out IntPtr pGenericModeBlock);
        [PreserveSig]
        int GetTLSIndex(out uint pIndex);
        [PreserveSig]
        int GetDacModuleHandle(out IntPtr phModule);

        // COM
        [PreserveSig]
        int GetRCWData(ulong addr, out V45RCWData data);
        [PreserveSig]
        int GetRCWInterfaces(ulong rcw, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COMInterfacePointerData[] interfaces, out uint pNeeded);
        [PreserveSig]
        int GetCCWData(ulong ccw, out V45CCWData data);
        [PreserveSig]
        int GetCCWInterfaces(ulong ccw, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COMInterfacePointerData[] interfaces, out uint pNeeded);
        [PreserveSig]
        int TraverseRCWCleanupList_do_not_use(); //(ulong cleanupListPtr, VISITRCWFORCLEANUP pCallback, LPVOID token);

        // GC Reference Functions
        [PreserveSig]
        int GetStackReferences(uint osThreadID, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
        [PreserveSig]
        int GetRegisterName(int regName, uint count, [Out] StringBuilder buffer, out uint pNeeded);


        [PreserveSig]
        int GetThreadAllocData(ulong thread, ref V45AllocData data);
        [PreserveSig]
        int GetHeapAllocData(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] V45GenerationAllocData[] data, out uint pNeeded);

        // For BindingDisplay plugin
        [PreserveSig]
        int GetFailedAssemblyList(ulong appDomain, int count, ulong[] values, out uint pNeeded);
        [PreserveSig]
        int GetPrivateBinPaths(ulong appDomain, int count, [Out] StringBuilder paths, out uint pNeeded);
        [PreserveSig]
        int GetAssemblyLocation(ulong assembly, int count, [Out] StringBuilder location, out uint pNeeded);
        [PreserveSig]
        int GetAppDomainConfigFile(ulong appDomain, int count, [Out] StringBuilder configFile, out uint pNeeded);
        [PreserveSig]
        int GetApplicationBase(ulong appDomain, int count, [Out] StringBuilder appBase, out uint pNeeded);
        [PreserveSig]
        int GetFailedAssemblyData(ulong assembly, out uint pContext, out int pResult);
        [PreserveSig]
        int GetFailedAssemblyLocation(ulong assesmbly, uint count, [Out] StringBuilder location, out uint pNeeded);
        [PreserveSig]
        int GetFailedAssemblyDisplayName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);
    }
    #endregion

#pragma warning disable 0649
#pragma warning disable 0169


    #region V45 Structs
    internal struct V45ThreadPoolData : IThreadPoolData
    {
        private int _cpuUtilization;
        private int _numIdleWorkerThreads;
        private int _numWorkingWorkerThreads;
        private int _numRetiredWorkerThreads;
        private int _minLimitTotalWorkerThreads;
        private int _maxLimitTotalWorkerThreads;

        private ulong _firstUnmanagedWorkRequest;

        private ulong _hillClimbingLog;
        private int _hillClimbingLogFirstIndex;
        private int _hillClimbingLogSize;

        private int _numTimers;

        private int _numCPThreads;
        private int _numFreeCPThreads;
        private int _maxFreeCPThreads;
        private int _numRetiredCPThreads;
        private int _maxLimitTotalCPThreads;
        private int _currentLimitTotalCPThreads;
        private int _minLimitTotalCPThreads;

        private ulong _asyncTimerCallbackCompletionFPtr;

        public int MinCP
        {
            get { return _minLimitTotalCPThreads; }
        }

        public int MaxCP
        {
            get { return _maxLimitTotalCPThreads; }
        }

        public int CPU
        {
            get { return _cpuUtilization; }
        }

        public int NumFreeCP
        {
            get { return _numFreeCPThreads; }
        }

        public int MaxFreeCP
        {
            get { return _maxFreeCPThreads; }
        }

        public int TotalThreads
        {
            get { return _numIdleWorkerThreads + _numWorkingWorkerThreads + _numRetiredWorkerThreads; }
        }

        public int RunningThreads
        {
            get { return _numWorkingWorkerThreads; }
        }

        public int IdleThreads
        {
            get { return _numIdleWorkerThreads; }
        }

        public int MinThreads
        {
            get { return _minLimitTotalWorkerThreads; }
        }

        public int MaxThreads
        {
            get { return _maxLimitTotalWorkerThreads; }
        }


        public ulong FirstWorkRequest
        {
            get { return _firstUnmanagedWorkRequest; }
        }


        public ulong QueueUserWorkItemCallbackFPtr
        {
            get { return ulong.MaxValue; }
        }

        public ulong AsyncCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }

        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }
    }

    internal struct V45ThreadLocalModuleData
    {
        private ulong _threadAddr;
        private ulong _moduleIndex;

        private ulong _pClassData;
        private ulong _pDynamicClassTable;
        public ulong pGCStaticDataStart;
        public ulong pNonGCStaticDataStart;
    }

    internal struct V45DomainLocalModuleData : IDomainLocalModuleData
    {
        private ulong _appDomainAddr;
        private ulong _moduleID;

        private ulong _pClassData;
        private ulong _pDynamicClassTable;
        private ulong _pGCStaticDataStart;
        private ulong _pNonGCStaticDataStart;

        public ulong AppDomainAddr
        {
            get { return _appDomainAddr; }
        }

        public ulong ModuleID
        {
            get { return _moduleID; }
        }

        public ulong ClassData
        {
            get { return _pClassData; }
        }

        public ulong DynamicClassTable
        {
            get { return _pDynamicClassTable; }
        }

        public ulong GCStaticDataStart
        {
            get { return _pGCStaticDataStart; }
        }

        public ulong NonGCStaticDataStart
        {
            get { return _pNonGCStaticDataStart; }
        }
    }

    internal struct StackRefData
    {
        public uint HasRegisterInformation;
        public int Register;
        public int Offset;
        public ulong Address;
        public ulong Object;
        public uint Flags;

        public uint SourceType;
        public ulong Source;
        public ulong StackPointer;
    }
    [StructLayout(LayoutKind.Sequential)]

    internal struct HandleData
    {
        public ulong AppDomain;
        public ulong Handle;
        public ulong Secondary;
        public uint Type;
        public uint StrongReference;

        // For RefCounted Handles
        public uint RefCount;
        public uint JupiterRefCount;
        public uint IsPegged;
    }

    internal struct V45ReJitData
    {
        private ulong _rejitID;
        private uint _flags;
        private ulong _nativeCodeAddr;
    }

    internal class V45MethodDescDataWrapper : IMethodDescData
    {
        public bool Init(SOSDac sos, ulong md)
        {
            if (!sos.GetMethodDescData(md, 0, out V45MethodDescData data))
                return false;

            _md = data.MethodDescPtr;
            _ip = data.NativeCodeAddr;
            _module = data.ModulePtr;
            _token = data.MDToken;
            _mt = data.MethodTablePtr;

            if (sos.GetCodeHeaderData(data.NativeCodeAddr, out CodeHeaderData header))
            {
                if (header.JITType == 1)
                    _jitType = MethodCompilationType.Jit;
                else if (header.JITType == 2)
                    _jitType = MethodCompilationType.Ngen;
                else
                    _jitType = MethodCompilationType.None;

                _gcInfo = header.GCInfo;
                _coldStart = header.ColdRegionStart;
                _coldSize = header.ColdRegionSize;
                _hotSize = header.HotRegionSize;
            }
            else
            {
                _jitType = MethodCompilationType.None;
            }

            return true;
        }

        private MethodCompilationType _jitType;
        private ulong _gcInfo, _md, _module, _ip, _coldStart;
        private uint _token, _coldSize, _hotSize;
        private ulong _mt;
        
        public ulong GCInfo
        {
            get
            {
                return _gcInfo;
            }
        }

        public ulong MethodDesc
        {
            get { return _md; }
        }

        public ulong Module
        {
            get { return _module; }
        }

        public uint MDToken
        {
            get { return _token; }
        }

        public ulong NativeCodeAddr
        {
            get { return _ip; }
        }

        public MethodCompilationType JITType
        {
            get { return _jitType; }
        }


        public ulong MethodTable
        {
            get { return _mt; }
        }

        public ulong ColdStart
        {
            get { return _coldStart; }
        }

        public uint ColdSize
        {
            get { return _coldSize; }
        }

        public uint HotSize
        {
            get { return _hotSize; }
        }
    }

    internal struct V45MethodDescData
    {
        private uint _bHasNativeCode;
        private uint _bIsDynamic;
        private short _wSlotNumber;
        internal ulong NativeCodeAddr;
        // Useful for breaking when a method is jitted.
        private ulong _addressOfNativeCodeSlot;

        internal ulong MethodDescPtr;
        internal ulong MethodTablePtr;
        internal ulong ModulePtr;

        internal uint MDToken;
        public ulong GCInfo;
        private ulong _GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        private ulong _managedDynamicMethodObject;

        private ulong _requestedIP;

        // Gives info for the single currently active version of a method
        private V45ReJitData _rejitDataCurrent;

        // Gives info corresponding to requestedIP (for !ip2md)
        private V45ReJitData _rejitDataRequested;

        // Total number of rejit versions that have been jitted
        private uint _cJittedRejitVersions;
    }

    internal struct CodeHeaderData
    {
        public ulong GCInfo;
        public uint JITType;
        public ulong MethodDescPtr;
        public ulong MethodStart;
        public uint MethodSize;
        public ulong ColdRegionStart;
        public uint ColdRegionSize;
        public uint HotRegionSize;
    }

    internal struct V45ModuleData : IModuleData
    {
        public ulong address;
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public ulong metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public ulong dwBaseClassIndex;
        public ulong dwModuleID;
        public uint dwTransientFlags;
        public ulong TypeDefToMethodTableMap;
        public ulong TypeRefToMethodTableMap;
        public ulong MethodDefToDescMap;
        public ulong FieldDefToDescMap;
        public ulong MemberRefToDescMap;
        public ulong FileReferencesMap;
        public ulong ManifestModuleReferencesMap;
        public ulong pLookupTableHeap;
        public ulong pThunkHeap;
        public ulong dwModuleIndex;

        #region IModuleData
        public ulong Assembly
        {
            get
            {
                return assembly;
            }
        }

        public ulong PEFile
        {
            get
            {
                return (bIsPEFile == 0) ? ilBase : peFile;
            }
        }
        public ulong LookupTableHeap
        {
            get { return pLookupTableHeap; }
        }
        public ulong ThunkHeap
        {
            get { return pThunkHeap; }
        }


        public object LegacyMetaDataImport
        {
            get { return null; }
        }


        public ulong ModuleId
        {
            get { return dwModuleID; }
        }

        public ulong ModuleIndex
        {
            get { return dwModuleIndex; }
        }

        public bool IsReflection
        {
            get { return bIsReflection != 0; }
        }

        public bool IsPEFile
        {
            get { return bIsPEFile != 0; }
        }
        public ulong ImageBase
        {
            get { return ilBase; }
        }
        public ulong MetdataStart
        {
            get { return metadataStart; }
        }

        public ulong MetadataLength
        {
            get { return metadataSize; }
        }
        #endregion
    }

    internal struct V45ObjectData : IObjectData
    {
        private ulong _methodTable;
        private uint _objectType;
        private ulong _size;
        private ulong _elementTypeHandle;
        private uint _elementType;
        private uint _dwRank;
        private ulong _dwNumComponents;
        private ulong _dwComponentSize;
        private ulong _arrayDataPtr;
        private ulong _arrayBoundsPtr;
        private ulong _arrayLowerBoundsPtr;

        private ulong _rcw;
        private ulong _ccw;

        public ClrElementType ElementType { get { return (ClrElementType)_elementType; } }
        public ulong ElementTypeHandle { get { return _elementTypeHandle; } }
        public ulong RCW { get { return _rcw; } }
        public ulong CCW { get { return _ccw; } }

        public ulong DataPointer
        {
            get { return _arrayDataPtr; }
        }
    }

    internal struct V45MethodTableData : IMethodTableData
    {
        public uint bIsFree; // everything else is NULL if this is true.
        public ulong module;
        public ulong eeClass;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumMethods;
        public ushort wNumVtableSlots;
        public ushort wNumVirtuals;
        public uint baseSize;
        public uint componentSize;
        public uint token;
        public uint dwAttrClass;
        public uint isShared; // flags & enum_flag_DomainNeutral
        public uint isDynamic;
        public uint containsPointers;

        public uint Token => token;
        public ulong Module => module;

        public bool ContainsPointers
        {
            get { return containsPointers != 0; }
        }

        public uint BaseSize
        {
            get { return baseSize; }
        }

        public uint ComponentSize
        {
            get { return componentSize; }
        }

        public ulong EEClass
        {
            get { return eeClass; }
        }

        public bool Free
        {
            get { return bIsFree != 0; }
        }

        public ulong Parent
        {
            get { return parentMethodTable; }
        }

        public bool Shared
        {
            get { return isShared != 0; }
        }


        public uint NumMethods
        {
            get { return wNumMethods; }
        }


        public ulong ElementTypeHandle
        {
            get { throw new NotImplementedException(); }
        }
    }

    internal struct V45CCWData : ICCWData
    {
        private ulong _outerIUnknown;
        private ulong _managedObject;
        private ulong _handle;
        private ulong _ccwAddress;

        private int _refCount;
        private int _interfaceCount;
        private uint _isNeutered;

        private int _jupiterRefCount;
        private uint _isPegged;
        private uint _isGlobalPegged;
        private uint _hasStrongRef;
        private uint _isExtendsCOMObject;
        private uint _hasWeakReference;
        private uint _isAggregated;

        public ulong IUnknown
        {
            get { return _outerIUnknown; }
        }

        public ulong Object
        {
            get { return _managedObject; }
        }

        public ulong Handle
        {
            get { return _handle; }
        }

        public ulong CCWAddress
        {
            get { return _ccwAddress; }
        }

        public int RefCount
        {
            get { return _refCount; }
        }

        public int JupiterRefCount
        {
            get { return _jupiterRefCount; }
        }

        public int InterfaceCount
        {
            get { return _interfaceCount; }
        }
    }

    internal struct COMInterfacePointerData
    {
        public ulong MethodTable;
        public ulong InterfacePtr;
        public ulong ComContext;
    }

    internal struct V45RCWData : IRCWData
    {
        private ulong _identityPointer;
        private ulong _unknownPointer;
        private ulong _managedObject;
        private ulong _jupiterObject;
        private ulong _vtablePtr;
        private ulong _creatorThread;
        private ulong _ctxCookie;

        private int _refCount;
        private int _interfaceCount;

        private uint _isJupiterObject;
        private uint _supportsIInspectable;
        private uint _isAggregated;
        private uint _isContained;
        private uint _isFreeThreaded;
        private uint _isDisconnected;

        public ulong IdentityPointer
        {
            get { return _identityPointer; }
        }

        public ulong UnknownPointer
        {
            get { return _unknownPointer; }
        }

        public ulong ManagedObject
        {
            get { return _managedObject; }
        }

        public ulong JupiterObject
        {
            get { return _jupiterObject; }
        }

        public ulong VTablePtr
        {
            get { return _vtablePtr; }
        }

        public ulong CreatorThread
        {
            get { return _creatorThread; }
        }

        public int RefCount
        {
            get { return _refCount; }
        }

        public int InterfaceCount
        {
            get { return _interfaceCount; }
        }

        public bool IsJupiterObject
        {
            get { return _isJupiterObject != 0; }
        }

        public bool IsDisconnected
        {
            get { return _isDisconnected != 0; }
        }
    }


    internal struct V45WorkRequestData
    {
        public ulong Function;
        public ulong Context;
        public ulong NextWorkRequest;
    }

    #endregion

#pragma warning restore 0169
#pragma warning restore 0649
}
