// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class V45Runtime : DesktopRuntimeBase
    {
        private List<ClrHandle> _handles;
        private SOSDac _sos;

        public V45Runtime(ClrInfo info, DataTarget dt, DacLibrary lib)
            : base(info, dt, lib)
        {
            if (!GetCommonMethodTables(ref _commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsExceptionKind.DacError);

            if (!_commonMTs.Validate())
                CanWalkHeap = false;

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsExceptionKind.DacError);

            int v = BitConverter.ToInt32(tmp, 0);
            if (v != 9)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsExceptionKind.DacError);
        }

        protected override void InitApi()
        {
            if (_sos == null)
                _sos = DacLibrary.GetSOSInterfaceNoAddRef();

            Debug.Assert(_sos != null);
        }

        internal override DesktopVersion CLRVersion => DesktopVersion.v45;

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
                        result.Add(handle);
                        yield return handle;

                        handle = handle.GetInteriorHandle();
                        if (handle != null)
                        {
                            result.Add(handle);
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
                ClrHeap heap = Heap;
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

                        ClrStackFrame frame = thread.StackTrace.SingleOrDefault(
                            f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);

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

        internal override IThreadData GetThread(ulong addr)
        {
            if (_sos.GetThreadData(addr, out ThreadData data))
                return data;

            return null;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            if (_sos.GetServerHeapDetails(addr, out HeapDetails data))
                return data;

            return null;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            if (_sos.GetWksHeapDetails(out HeapDetails details))
                return details;

            return null;
        }

        internal override ulong[] GetServerHeapList()
        {
            return _sos.GetHeapList(HeapCount);
        }

        internal override ulong[] GetAppDomainList(int count)
        {
            return _sos.GetAppDomainList(count);
        }

        internal override ulong[] GetAssemblyList(ulong appDomain, int count)
        {
            return _sos.GetAssemblyList(appDomain, count);
        }

        internal override ulong[] GetModuleList(ulong assembly, int count)
        {
            return _sos.GetModuleList(assembly, count);
        }

        internal override IAssemblyData GetAssemblyData(ulong domain, ulong assembly)
        {
            if (_sos.GetAssemblyData(domain, assembly, out AssemblyData data))
                return data;

            return null;
        }

        internal override IAppDomainStoreData GetAppDomainStoreData()
        {
            if (_sos.GetAppDomainStoreData(out AppDomainStoreData data))
                return data;

            return null;
        }

        internal override IMethodTableData GetMethodTableData(ulong addr)
        {
            if (_sos.GetMethodTableData(addr, out MethodTableData data))
                return data;

            return null;
        }

        internal override ulong GetMethodTableByEEClass(ulong eeclass)
        {
            return _sos.GetMethodTableByEEClass(eeclass);
        }

        internal override IGCInfo GetGCInfoImpl()
        {
            if (_sos.GetGcHeapData(out GCInfo data))
                return data;

            return null;
        }

        internal override bool GetCommonMethodTables(ref CommonMethodTables mts)
        {
            return _sos.GetCommonMethodTables(out mts);
        }

        public override string GetMethodTableName(ulong mt)
        {
            return _sos.GetMethodTableName(mt);
        }

        internal override string GetPEFileName(ulong addr)
        {
            return _sos.GetPEFileName(addr);
        }

        internal override IModuleData GetModuleData(ulong addr)
        {
            if (_sos.GetModuleData(addr, out ModuleData data))
                return data;

            return null;
        }

        internal override ulong GetModuleForMT(ulong addr)
        {
            if (_sos.GetMethodTableData(addr, out MethodTableData data))
                return data.Module;

            return 0;
        }

        internal override ISegmentData GetSegmentData(ulong addr)
        {
            if (_sos.GetSegmentData(addr, out SegmentData data))
                return data;

            return null;
        }

        internal override IAppDomainData GetAppDomainData(ulong addr)
        {
            if (_sos.GetAppDomainData(addr, out AppDomainData data))
                return data;

            return null;
        }

        internal override string GetAppDomaminName(ulong addr)
        {
            return _sos.GetAppDomainName(addr);
        }

        internal override string GetAssemblyName(ulong addr)
        {
            return _sos.GetAssemblyName(addr);
        }

        internal override bool TraverseHeap(ulong heap, SOSDac.LoaderHeapTraverse callback)
        {
            return _sos.TraverseLoaderHeap(heap, callback);
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, SOSDac.LoaderHeapTraverse callback)
        {
            return _sos.TraverseStubHeap(appDomain, type, callback);
        }

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            JitManagerInfo[] jitManagers = _sos.GetJitManagers();
            for (int i = 0; i < jitManagers.Length; ++i)
            {
                if (jitManagers[i].Type != CodeHeapType.Unknown)
                    continue;

                JitCodeHeapInfo[] heapInfo = _sos.GetCodeHeapList(jitManagers[i].Address);

                for (int j = 0; j < heapInfo.Length; ++j)
                    yield return heapInfo[i];
            }
        }

        internal override IFieldInfo GetFieldInfo(ulong mt)
        {
            if (_sos.GetFieldInfo(mt, out V4FieldInfo fieldInfo))
                return fieldInfo;

            return null;
        }

        internal override IFieldData GetFieldData(ulong fieldDesc)
        {
            if (_sos.GetFieldData(fieldDesc, out FieldData data))
                return data;

            return null;
        }

        internal override MetaDataImport GetMetadataImport(ulong module)
        {
            return _sos.GetMetadataImport(module);
        }

        internal override IObjectData GetObjectData(ulong objRef)
        {
            if (_sos.GetObjectData(objRef, out V45ObjectData data))
                return data;

            return null;
        }

        internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
        {
            List<MethodTableTokenPair> mts = new List<MethodTableTokenPair>();
            _sos.TraverseModuleMap(
                SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable,
                module,
                delegate(uint index, ulong mt, IntPtr token) { mts.Add(new MethodTableTokenPair(mt, index)); });

            return mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModuleById(ulong appDomain, ulong id)
        {
            if (_sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)id, out DomainLocalModuleData data))
                return data;

            return null;
        }

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            return _sos.GetCCWInterfaces(ccw, count);
        }

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
        {
            return _sos.GetRCWInterfaces(rcw, count);
        }

        internal override ICCWData GetCCWData(ulong ccw)
        {
            if (_sos.GetCCWData(ccw, out CCWData data))
                return data;

            return null;
        }

        internal override IRCWData GetRCWData(ulong rcw)
        {
            if (_sos.GetRCWData(rcw, out RCWData data))
                return data;

            return null;
        }

        internal override ulong GetILForModule(ClrModule module, uint rva)
        {
            return _sos.GetILForModule(module.Address, rva);
        }

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            ulong addr = offset;

            if (!_sos.GetThreadLocalModuleData(thread, moduleId, out ThreadLocalModuleData data))
                return 0;

            if (type.IsObjectReference() || type.IsValueClass())
                addr += data.GCStaticDataStart;
            else
                addr += data.NonGCStaticDataStart;

            return addr;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong module)
        {
            if (_sos.GetDomainLocalModuleDataFromModule(module, out DomainLocalModuleData data))
                return data;

            return null;
        }

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            if (!_sos.GetMethodTableData(methodTable, out MethodTableData mtData))
                return null;

            uint numMethods = mtData.NumMethods;
            ulong[] mds = new ulong[numMethods];

            for (int i = 0; i < numMethods; ++i)
                if (_sos.GetCodeHeaderData(_sos.GetMethodTableSlot(methodTable, i), out CodeHeaderData header))
                    mds[i] = header.MethodDesc;

            return mds;
        }

        internal override string GetNameForMD(ulong md)
        {
            return _sos.GetMethodDescName(md);
        }

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            V45MethodDescDataWrapper wrapper = new V45MethodDescDataWrapper();
            if (!wrapper.Init(_sos, md))
                return null;

            return wrapper;
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            if (!_sos.GetMethodTableData(mt, out MethodTableData data))
                return uint.MaxValue;

            return data.Token;
        }

        protected override DesktopStackFrame GetStackFrame(DesktopThread thread, byte[] context, ulong ip, ulong framePtr, ulong frameVtbl)
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

                frame = new DesktopStackFrame(this, thread, context, framePtr, frameName, innerMethod);
            }
            else
            {
                frame = new DesktopStackFrame(this, thread, context, ip, framePtr, _sos.GetMethodDescPtrFromIP(ip));
            }

            return frame;
        }

        private bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
        {
            stackTrace = 0;
            ClrInstanceField field = type.GetFieldByName("_stackTrace");
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

                result.Add(new DesktopStackFrame(this, thread, null, ip, sp, md));

                dataPtr += (ulong)elementSize;
            }

            return result;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            if (!_sos.GetThreadStoreData(out ThreadStoreData data))
                return null;

            return data;
        }

        internal override string GetAppBase(ulong appDomain)
        {
            return _sos.GetAppBase(appDomain);
        }

        internal override string GetConfigFile(ulong appDomain)
        {
            return _sos.GetConfigFile(appDomain);
        }

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (!_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    return null;

                if ((md = codeHeaderData.MethodDesc) == 0)
                    return null;
            }

            V45MethodDescDataWrapper mdWrapper = new V45MethodDescDataWrapper();
            if (!mdWrapper.Init(_sos, md))
                return null;

            return mdWrapper;
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            return _sos.GetThreadFromThinlockId(threadId);
        }

        internal override int GetSyncblkCount()
        {
            if (_sos.GetSyncBlockData(1, out SyncBlockData data))
                return (int)data.TotalSyncBlockCount;

            return 0;
        }

        internal override ISyncBlkData GetSyncblkData(int index)
        {
            if (_sos.GetSyncBlockData(index + 1, out SyncBlockData data))
                return data;

            return null;
        }

        internal override IThreadPoolData GetThreadPoolData()
        {
            if (_sos.GetThreadPoolData(out ThreadPoolData data))
                return data;

            return null;
        }

        internal override uint GetTlsSlot()
        {
            return _sos.GetTlsIndex();
        }

        internal override uint GetThreadTypeIndex()
        {
            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (PointerSize == 8)
                return 0x30;

            return 0x18;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            IThreadPoolData data = GetThreadPoolData();
            ulong request = data.FirstWorkRequest;
            while (request != 0)
            {
                if (!_sos.GetWorkRequestData(request, out WorkRequestData requestData))
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

        public override string GetJitHelperFunctionName(ulong addr)
        {
            return _sos.GetJitHelperFunctionName(addr);
        }
    }
}