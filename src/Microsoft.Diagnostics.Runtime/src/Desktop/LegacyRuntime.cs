// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class LegacyRuntime : DesktopRuntimeBase
    {
        // Buffer used for all name requests, this needs to be QUITE large because with anonymous types we can have
        // type names that are 8k+ long...
        private readonly byte[] _buffer = new byte[1024 * 32];
        private readonly DesktopVersion _version;
        private readonly int _patch;

        public LegacyRuntime(ClrInfo info, DataTargetImpl dt, DacLibrary lib, DesktopVersion version, int patch)
            : base(info, dt, lib)
        {
            _version = version;
            _patch = patch;

            if (!GetCommonMethodTables(ref _commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsException.HR.DacError);

            if (!_commonMTs.Validate())
                CanWalkHeap = false;

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            var tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            var v = BitConverter.ToInt32(tmp, 0);
            if (v != 8)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
        }

        protected override void InitApi()
        {
        }

        internal override DesktopVersion CLRVersion => _version;

        internal override Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken)
        {
            return new Dictionary<ulong, List<ulong>>();
        }

        internal override ulong GetILForModule(ClrModule module, uint rva)
        {
            throw new NotImplementedException();
        }

        internal override ulong[] GetAssemblyList(ulong appDomain, int count)
        {
            return RequestAddrList(DacRequests.ASSEMBLY_LIST, appDomain, count);
        }

        internal override ulong[] GetModuleList(ulong assembly, int count)
        {
            return RequestAddrList(DacRequests.ASSEMBLYMODULE_LIST, assembly, count);
        }

        internal override IAssemblyData GetAssemblyData(ulong appDomain, ulong assembly)
        {
            if (assembly == 0)
                return null;

            return Request<IAssemblyData, AssemblyData>(DacRequests.ASSEMBLY_DATA, assembly);
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            var handleTable = new HandleTableWalker(this);

            byte[] input = null;
            if (CLRVersion == DesktopVersion.v2)
                input = handleTable.V2Request;
            else
                input = handleTable.V4Request;

            // TODO:  Better to return partial data or null?  Maybe bool function return?
            //        I don't even think the dac api will fail unless there's a data read error.
            var ret = Request(DacRequests.HANDLETABLE_TRAVERSE, input, null);
            if (!ret)
                Trace.WriteLine("Warning, GetHandles() method failed, returning partial results.");

            return handleTable.Handles;
        }

        internal override bool TraverseHeap(ulong heap, SOSDac.LoaderHeapTraverse callback)
        {
            var input = new byte[sizeof(ulong) * 2];
            WriteValueToBuffer(heap, input, 0);
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong));

            return Request(DacRequests.LOADERHEAP_TRAVERSE, input, null);
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, SOSDac.LoaderHeapTraverse callback)
        {
            byte[] input;
            if (IntPtr.Size == 4)
                input = new byte[sizeof(ulong) * 2];
            else
                input = new byte[sizeof(ulong) * 3];

            WriteValueToBuffer(appDomain, input, 0);
            WriteValueToBuffer(type, input, sizeof(ulong));
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong) + sizeof(int));

            return Request(DacRequests.VIRTCALLSTUBHEAP_TRAVERSE, input, null);
        }

        internal override ulong GetFirstThread()
        {
            var threadStore = GetThreadStoreData();
            return threadStore != null ? threadStore.FirstThread : 0;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (addr == 0)
                return null;

            var input = new byte[2 * sizeof(ulong)];
            Buffer.BlockCopy(BitConverter.GetBytes(addr), 0, input, 0, sizeof(ulong));

            if (CLRVersion == DesktopVersion.v2)
                return Request<IThreadData, V2ThreadData>(DacRequests.THREAD_DATA, input);

            var result = (ThreadData)Request<IThreadData, ThreadData>(DacRequests.THREAD_DATA, input);
            if (IntPtr.Size == 4)
                result = new ThreadData(ref result);
            return result;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);

            var result = (HeapDetails)Request<IHeapDetails, HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);
            result = new HeapDetails(ref result);
            return result;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);

            var result = (HeapDetails)Request<IHeapDetails, HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);
            result = new HeapDetails(ref result);
            return result;
        }

        internal override ulong[] GetServerHeapList()
        {
            return RequestAddrList(DacRequests.GCHEAP_LIST, HeapCount);
        }

        internal override ulong[] GetAppDomainList(int count)
        {
            return RequestAddrList(DacRequests.APPDOMAIN_LIST, count);
        }

        internal override ulong GetMethodTableByEEClass(ulong eeclass)
        {
            if (eeclass == 0)
                return 0;

            IEEClassData classData;
            if (CLRVersion == DesktopVersion.v2)
                classData = Request<IEEClassData, V2EEClassData>(DacRequests.EECLASS_DATA, eeclass);
            else
                classData = Request<IEEClassData, V4EEClassData>(DacRequests.EECLASS_DATA, eeclass);

            if (classData == null)
                return 0;

            return classData.Module;
        }

        internal override IMethodTableData GetMethodTableData(ulong addr)
        {
            return Request<IMethodTableData, LegacyMethodTableData>(DacRequests.METHODTABLE_DATA, addr);
        }

        internal override IGCInfo GetGCInfoImpl()
        {
            return Request<IGCInfo, GCInfo>(DacRequests.GCHEAP_DATA);
        }

        internal override ISegmentData GetSegmentData(ulong segmentAddr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<ISegmentData, V2SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);

            var result = (SegmentData)Request<ISegmentData, SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);
            if (IntPtr.Size == 4)
                result = new SegmentData(ref result);

            return result;
        }

        internal override string GetAppDomaminName(ulong addr)
        {
            if (addr == 0)
                return null;

            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_NAME, addr, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        private void ClearBuffer()
        {
            _buffer[0] = 0;
            _buffer[1] = 0;
        }

        internal override string GetAssemblyName(ulong addr)
        {
            if (addr == 0)
                return null;

            // todo: should this be ASSEMBLY_DISPLAY_NAME?
            ClearBuffer();
            if (!Request(DacRequests.ASSEMBLY_NAME, addr, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        internal override IAppDomainStoreData GetAppDomainStoreData()
        {
            return Request<IAppDomainStoreData, AppDomainStoreData>(DacRequests.APPDOMAIN_STORE_DATA);
        }

        internal override IAppDomainData GetAppDomainData(ulong addr)
        {
            return Request<IAppDomainData, AppDomainData>(DacRequests.APPDOMAIN_DATA, addr);
        }

        internal override bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs)
        {
            return RequestStruct(DacRequests.USEFULGLOBALS, ref mCommonMTs);
        }

        internal override string GetNameForMT(ulong mt)
        {
            ClearBuffer();
            if (!Request(DacRequests.METHODTABLE_NAME, mt, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        internal override string GetPEFileName(ulong addr)
        {
            if (addr == 0)
                return null;

            ClearBuffer();
            if (!Request(DacRequests.PEFILE_NAME, addr, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        internal override IModuleData GetModuleData(ulong addr)
        {
            if (addr == 0)
                return null;

            IModuleData result = null;
            if (CLRVersion == DesktopVersion.v2)
                result = Request<IModuleData, V2ModuleData>(DacRequests.MODULE_DATA, addr);
            else
                result = Request<IModuleData, V4ModuleData>(DacRequests.MODULE_DATA, addr);

            // Only needed in legacy runtime since v4.5 and on do not return this interface.
            RegisterForRelease(result);
            return result;
        }

        internal override ulong GetModuleForMT(ulong mt)
        {
            if (mt == 0)
                return 0;

            var mtData = GetMethodTableData(mt);
            if (mtData == null)
                return 0;

            IEEClassData classData;
            if (CLRVersion == DesktopVersion.v2)
                classData = Request<IEEClassData, V2EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);
            else
                classData = Request<IEEClassData, V4EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);

            if (classData == null)
                return 0;

            return classData.Module;
        }

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            var output = new byte[sizeof(int)];
            if (Request(DacRequests.JITLIST, null, output))
            {
                var JitManagerSize = Marshal.SizeOf(typeof(JitManagerInfo));
                var count = BitConverter.ToInt32(output, 0);
                var size = JitManagerSize * count;

                if (size > 0)
                {
                    output = new byte[size];
                    if (Request(DacRequests.MANAGER_LIST, null, output))
                    {
                        var heapInfo = new MutableJitCodeHeapInfo();
                        var CodeHeapTypeOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "codeHeapType").ToInt32();
                        var AddressOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "address").ToInt32();
                        var CurrAddrOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "currentAddr").ToInt32();
                        var JitCodeHeapInfoSize = Marshal.SizeOf(typeof(JitCodeHeapInfo));

                        for (var i = 0; i < count; ++i)
                        {
                            var type = BitConverter.ToInt32(output, i * JitManagerSize + sizeof(ulong));

                            // Is this code heap IL?
                            if ((type & 3) != 0)
                                continue;

                            var address = BitConverter.ToUInt64(output, i * JitManagerSize);
                            var jitManagerBuffer = new byte[sizeof(ulong) * 2];
                            WriteValueToBuffer(address, jitManagerBuffer, 0);

                            if (Request(DacRequests.JITHEAPLIST, jitManagerBuffer, jitManagerBuffer))
                            {
                                var heapCount = BitConverter.ToInt32(jitManagerBuffer, sizeof(ulong));

                                var codeHeapBuffer = new byte[heapCount * JitCodeHeapInfoSize];
                                if (Request(DacRequests.CODEHEAP_LIST, jitManagerBuffer, codeHeapBuffer))
                                {
                                    for (var j = 0; j < heapCount; ++j)
                                    {
                                        heapInfo.Address = BitConverter.ToUInt64(codeHeapBuffer, j * JitCodeHeapInfoSize + AddressOffset);
                                        heapInfo.Type = (CodeHeapType)BitConverter.ToUInt32(codeHeapBuffer, j * JitCodeHeapInfoSize + CodeHeapTypeOffset);
                                        heapInfo.CurrentAddress = BitConverter.ToUInt64(codeHeapBuffer, j * JitCodeHeapInfoSize + CurrAddrOffset);

                                        yield return heapInfo;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private struct MutableJitCodeHeapInfo : ICodeHeap
        {
            public CodeHeapType Type;
            public ulong Address;
            public ulong CurrentAddress;

            CodeHeapType ICodeHeap.Type => Type;
            ulong ICodeHeap.Address => Address;
        }

        internal override IFieldInfo GetFieldInfo(ulong mt)
        {
            var mtData = GetMethodTableData(mt);

            IFieldInfo fieldData;

            if (CLRVersion == DesktopVersion.v2)
                fieldData = Request<IFieldInfo, V2EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);
            else
                fieldData = Request<IFieldInfo, V4EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);

            return fieldData;
        }

        internal override IFieldData GetFieldData(ulong fieldDesc)
        {
            return Request<IFieldData, FieldData>(DacRequests.FIELDDESC_DATA, fieldDesc);
        }

        internal override IObjectData GetObjectData(ulong objRef)
        {
            return Request<IObjectData, LegacyObjectData>(DacRequests.OBJECT_DATA, objRef);
        }

        internal override MetaDataImport GetMetadataImport(ulong module)
        {
            var data = GetModuleData(module);
            RegisterForRelease(data);

            if (data != null && data.LegacyMetaDataImport != IntPtr.Zero)
                return new MetaDataImport(DacLibrary, data.LegacyMetaDataImport);

            return null;
        }

        internal override ICCWData GetCCWData(ulong ccw)
        {
            // Not supported pre-v4.5.
            return null;
        }

        internal override IRCWData GetRCWData(ulong rcw)
        {
            // Not supported pre-v4.5.
            return null;
        }

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            return null;
        }

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
        {
            return null;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id)
        {
            var inout = GetByteArrayForStruct<LegacyDomainLocalModuleData>();

            var i = WriteValueToBuffer(appDomain, inout, 0);
            i = WriteValueToBuffer(new IntPtr((long)id), inout, i);

            if (Request(DacRequests.DOMAINLOCALMODULEFROMAPPDOMAIN_DATA, null, inout))
                return ConvertStruct<IDomainLocalModuleData, LegacyDomainLocalModuleData>(inout);

            return null;
        }

        internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
        {
            var mts = new List<MethodTableTokenPair>();

            SOSDac.ModuleMapTraverse traverse = delegate(uint index, ulong mt, IntPtr token) { mts.Add(new MethodTableTokenPair(mt, index)); };
            var args = new LegacyModuleMapTraverseArgs
            {
                pCallback = Marshal.GetFunctionPointerForDelegate(traverse),
                module = module
            };

            // TODO:  Blah, theres got to be a better way to do this.
            var input = GetByteArrayForStruct<LegacyModuleMapTraverseArgs>();
            var mem = Marshal.AllocHGlobal(input.Length);
            Marshal.StructureToPtr(args, mem, true);
            Marshal.Copy(mem, input, 0, input.Length);
            Marshal.FreeHGlobal(mem);

            var r = Request(DacRequests.MODULEMAP_TRAVERSE, input, null);

            GC.KeepAlive(traverse);

            return mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong module)
        {
            return Request<IDomainLocalModuleData, LegacyDomainLocalModuleData>(DacRequests.DOMAINLOCALMODULE_DATA_FROM_MODULE, module);
        }

        private ulong GetMethodDescFromIp(ulong ip)
        {
            if (ip == 0)
                return 0;

            var data = Request<IMethodDescData, V35MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);
            if (data == null)
                data = Request<IMethodDescData, V2MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);

            if (data == null)
            {
                var codeHeaderData = new CodeHeaderData();
                if (RequestStruct(DacRequests.CODEHEADER_DATA, ip, ref codeHeaderData))
                    return codeHeaderData.MethodDesc;
            }

            return data != null ? data.MethodDesc : 0;
        }

        internal override string GetNameForMD(ulong md)
        {
            ClearBuffer();
            if (!Request(DacRequests.METHODDESC_NAME, md, _buffer))
                return "<Error>";

            return BytesToString(_buffer);
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            var token = uint.MaxValue;

            var mtData = GetMethodTableData(mt);
            if (mtData != null)
            {
                byte[] buffer = null;
                if (CLRVersion == DesktopVersion.v2)
                    buffer = GetByteArrayForStruct<V2EEClassData>();
                else
                    buffer = GetByteArrayForStruct<V4EEClassData>();

                if (Request(DacRequests.EECLASS_DATA, mtData.EEClass, buffer))
                {
                    if (CLRVersion == DesktopVersion.v2)
                    {
                        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        var result = (V2EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V2EEClassData));
                        handle.Free();

                        token = result.token;
                    }
                    else
                    {
                        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        var result = (V4EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V4EEClassData));
                        handle.Free();

                        token = result.token;
                    }
                }
            }

            return token;
        }

        protected override DesktopStackFrame GetStackFrame(DesktopThread thread, ulong ip, ulong sp, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            ClearBuffer();

            if (frameVtbl != 0)
            {
                ClrMethod method = null;
                var frameName = "Unknown Frame";
                if (Request(DacRequests.FRAME_NAME, frameVtbl, _buffer))
                    frameName = BytesToString(_buffer);

                var mdData = GetMethodDescData(DacRequests.METHODDESC_FRAME_DATA, sp);
                if (mdData != null)
                    method = DesktopMethod.Create(this, mdData);

                frame = new DesktopStackFrame(this, thread, sp, frameName, method);
            }
            else
            {
                var md = GetMethodDescFromIp(ip);
                frame = new DesktopStackFrame(this, thread, ip, sp, md);
            }

            return frame;
        }

        private bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
        {
            stackTrace = 0;
            var field = type.GetFieldByName("_stackTrace");
            if (field == null)
                return false;

            var tmp = field.GetValue(obj);
            if (tmp == null || !(tmp is ulong))
                return false;

            stackTrace = (ulong)tmp;
            return true;
        }

        internal override IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type)
        {
            var result = new List<ClrStackFrame>();

            if (!GetStackTraceFromField(type, obj, out var _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            var heap = (DesktopGCHeap)Heap;
            var stackTraceType = heap.GetObjectType(_stackTrace);

            if (stackTraceType == null)
                stackTraceType = heap.ArrayType;

            if (!stackTraceType.IsArray)
                return result;

            var len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            var elementSize = CLRVersion == DesktopVersion.v2 ? IntPtr.Size * 4 : IntPtr.Size * 3;
            var dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!ReadPointer(dataPtr, out var count))
                return result;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            DesktopThread thread = null;
            for (var i = 0; i < (int)count; ++i)
            {
                if (!ReadPointer(dataPtr, out var ip))
                    break;
                if (!ReadPointer(dataPtr + (ulong)IntPtr.Size, out var sp))
                    break;
                if (!ReadPointer(dataPtr + (ulong)(2 * IntPtr.Size), out var md))
                    break;

                if (i == 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                result.Add(new DesktopStackFrame(this, thread, ip, sp, md));

                dataPtr += (ulong)elementSize;
            }

            return result;
        }

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            return GetMethodDescData(DacRequests.METHODDESC_DATA, md);
        }

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            var mtData = Request<IMethodTableData, LegacyMethodTableData>(DacRequests.METHODTABLE_DATA, methodTable);
            var values = new ulong[mtData.NumMethods];

            if (mtData.NumMethods == 0)
                return values;

            var codeHeader = new CodeHeaderData();
            var slotArgs = new byte[0x10];
            var result = new byte[sizeof(ulong)];

            WriteValueToBuffer(methodTable, slotArgs, 0);
            for (var i = 0; i < mtData.NumMethods; ++i)
            {
                WriteValueToBuffer(i, slotArgs, sizeof(ulong));
                if (!Request(DacRequests.METHODTABLE_SLOT, slotArgs, result))
                    continue;

                var ip = BitConverter.ToUInt64(result, 0);

                if (!RequestStruct(DacRequests.CODEHEADER_DATA, ip, ref codeHeader))
                    continue;

                values[i] = codeHeader.MethodDesc;
            }

            return values;
        }

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            // TODO
            return 0;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            var threadStore = new ThreadStoreData();
            if (!RequestStruct(DacRequests.THREAD_STORE_DATA, ref threadStore))
                return null;

            return threadStore;
        }

        internal override string GetAppBase(ulong appDomain)
        {
            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_APP_BASE, appDomain, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        internal override string GetConfigFile(ulong appDomain)
        {
            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_CONFIG_FILE, appDomain, _buffer))
                return null;

            return BytesToString(_buffer);
        }

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            var result = GetMethodDescData(DacRequests.METHODDESC_IP_DATA, ip);
            if (result != null)
                return result;

            var methodDesc = GetMethodDescFromIp(ip);
            if (methodDesc != 0)
                return GetMethodDescData(DacRequests.METHODDESC_DATA, ip);

            return null;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            var data = GetThreadPoolData();

            if (_version == DesktopVersion.v2)
            {
                var curr = data.FirstWorkRequest;
                var bytes = GetByteArrayForStruct<DacpWorkRequestData>();

                while (Request(DacRequests.WORKREQUEST_DATA, curr, bytes))
                {
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var result = (DacpWorkRequestData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(DacpWorkRequestData));
                    handle.Free();

                    yield return new DesktopNativeWorkItem(result);

                    curr = result.NextWorkRequest;
                    if (curr == 0)
                        break;
                }
            }
        }

        private IMethodDescData GetMethodDescData(uint request_id, ulong addr)
        {
            if (addr == 0)
                return null;

            IMethodDescData result;
            if (_version == DesktopVersion.v4 || _patch > 4016)
            {
                result = Request<IMethodDescData, V35MethodDescData>(request_id, addr);
            }
            else if (_patch < 3053)
            {
                result = Request<IMethodDescData, V2MethodDescData>(request_id, addr);
            }
            else
            {
                // We aren't sure which version it is between 3053 and 4016, so we'll just do both.  Slow, but we
                // might not even encounter those versions in the wild.
                result = Request<IMethodDescData, V35MethodDescData>(request_id, addr);
                if (result == null)
                    result = Request<IMethodDescData, V2MethodDescData>(request_id, addr);
            }

            if (result == null && request_id == DacRequests.METHODDESC_IP_DATA)
            {
                var codeHeaderData = new CodeHeaderData();

                if (RequestStruct(DacRequests.CODEHEADER_DATA, addr, ref codeHeaderData))
                    result = GetMethodDescData(DacRequests.METHODDESC_DATA, codeHeaderData.MethodDesc);
            }

            return result;
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            var input = new byte[sizeof(uint)];
            WriteValueToBuffer(threadId, input, 0);

            var output = new byte[sizeof(ulong)];
            if (!Request(DacRequests.THREAD_THINLOCK_DATA, input, output))
                return 0;

            return BitConverter.ToUInt64(output, 0);
        }

        internal override int GetSyncblkCount()
        {
            var data = Request<ISyncBlkData, SyncBlockData>(DacRequests.SYNCBLOCK_DATA, 1);
            if (data == null)
                return 0;

            return (int)data.TotalCount;
        }

        internal override ISyncBlkData GetSyncblkData(int index)
        {
            if (index < 0)
                return null;

            return Request<ISyncBlkData, SyncBlockData>(DacRequests.SYNCBLOCK_DATA, (uint)index + 1);
        }

        internal override IThreadPoolData GetThreadPoolData()
        {
            if (_version == DesktopVersion.v2)
                return Request<IThreadPoolData, V2ThreadPoolData>(DacRequests.THREADPOOL_DATA);

            return Request<IThreadPoolData, V4ThreadPoolData>(DacRequests.THREADPOOL_DATA_2);
        }

        internal override uint GetTlsSlot()
        {
            var value = new byte[sizeof(uint)];
            if (!Request(DacRequests.CLRTLSDATA_INDEX, null, value))
                return uint.MaxValue;

            return BitConverter.ToUInt32(value, 0);
        }

        internal override uint GetThreadTypeIndex()
        {
            if (_version == DesktopVersion.v2)
                return PointerSize == 4 ? 12u : 13u;

            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (PointerSize == 8)
                return 0x38;

            return 0x24;
        }

        internal override uint GetStringFirstCharOffset()
        {
            if (PointerSize == 0x8)
                return 0x10;

            return 0xc;
        }

        internal override uint GetStringLengthOffset()
        {
            if (PointerSize == 8)
                return 0xc;

            return 8;
        }

        internal override uint GetExceptionHROffset()
        {
            return PointerSize == 8 ? 0x74u : 0x38u;
        }
    }

    internal class DacRequests
    {
        internal const uint VERSION = 0xe0000000U;
        internal const uint THREAD_STORE_DATA = 0xf0000000U;
        internal const uint APPDOMAIN_STORE_DATA = 0xf0000001U;
        internal const uint APPDOMAIN_LIST = 0xf0000002U;
        internal const uint APPDOMAIN_DATA = 0xf0000003U;
        internal const uint APPDOMAIN_NAME = 0xf0000004U;
        internal const uint APPDOMAIN_APP_BASE = 0xf0000005U;
        internal const uint APPDOMAIN_PRIVATE_BIN_PATHS = 0xf0000006U;
        internal const uint APPDOMAIN_CONFIG_FILE = 0xf0000007U;
        internal const uint ASSEMBLY_LIST = 0xf0000008U;
        internal const uint FAILED_ASSEMBLY_LIST = 0xf0000009U;
        internal const uint ASSEMBLY_DATA = 0xf000000aU;
        internal const uint ASSEMBLY_NAME = 0xf000000bU;
        internal const uint ASSEMBLY_DISPLAY_NAME = 0xf000000cU;
        internal const uint ASSEMBLY_LOCATION = 0xf000000dU;
        internal const uint FAILED_ASSEMBLY_DATA = 0xf000000eU;
        internal const uint FAILED_ASSEMBLY_DISPLAY_NAME = 0xf000000fU;
        internal const uint FAILED_ASSEMBLY_LOCATION = 0xf0000010U;
        internal const uint THREAD_DATA = 0xf0000011U;
        internal const uint THREAD_THINLOCK_DATA = 0xf0000012U;
        internal const uint CONTEXT_DATA = 0xf0000013U;
        internal const uint METHODDESC_DATA = 0xf0000014U;
        internal const uint METHODDESC_IP_DATA = 0xf0000015U;
        internal const uint METHODDESC_NAME = 0xf0000016U;
        internal const uint METHODDESC_FRAME_DATA = 0xf0000017U;
        internal const uint CODEHEADER_DATA = 0xf0000018U;
        internal const uint THREADPOOL_DATA = 0xf0000019U;
        internal const uint WORKREQUEST_DATA = 0xf000001aU;
        internal const uint OBJECT_DATA = 0xf000001bU;
        internal const uint FRAME_NAME = 0xf000001cU;
        internal const uint OBJECT_STRING_DATA = 0xf000001dU;
        internal const uint OBJECT_CLASS_NAME = 0xf000001eU;
        internal const uint METHODTABLE_NAME = 0xf000001fU;
        internal const uint METHODTABLE_DATA = 0xf0000020U;
        internal const uint EECLASS_DATA = 0xf0000021U;
        internal const uint FIELDDESC_DATA = 0xf0000022U;
        internal const uint MANAGEDSTATICADDR = 0xf0000023U;
        internal const uint MODULE_DATA = 0xf0000024U;
        internal const uint MODULEMAP_TRAVERSE = 0xf0000025U;
        internal const uint MODULETOKEN_DATA = 0xf0000026U;
        internal const uint PEFILE_DATA = 0xf0000027U;
        internal const uint PEFILE_NAME = 0xf0000028U;
        internal const uint ASSEMBLYMODULE_LIST = 0xf0000029U;
        internal const uint GCHEAP_DATA = 0xf000002aU;
        internal const uint GCHEAP_LIST = 0xf000002bU;
        internal const uint GCHEAPDETAILS_DATA = 0xf000002cU;
        internal const uint GCHEAPDETAILS_STATIC_DATA = 0xf000002dU;
        internal const uint HEAPSEGMENT_DATA = 0xf000002eU;
        internal const uint UNITTEST_DATA = 0xf000002fU;
        internal const uint ISSTUB = 0xf0000030U;
        internal const uint DOMAINLOCALMODULE_DATA = 0xf0000031U;
        internal const uint DOMAINLOCALMODULEFROMAPPDOMAIN_DATA = 0xf0000032U;
        internal const uint DOMAINLOCALMODULE_DATA_FROM_MODULE = 0xf0000033U;
        internal const uint SYNCBLOCK_DATA = 0xf0000034U;
        internal const uint SYNCBLOCK_CLEANUP_DATA = 0xf0000035U;
        internal const uint HANDLETABLE_TRAVERSE = 0xf0000036U;
        internal const uint RCWCLEANUP_TRAVERSE = 0xf0000037U;
        internal const uint EHINFO_TRAVERSE = 0xf0000038U;
        internal const uint STRESSLOG_DATA = 0xf0000039U;
        internal const uint JITLIST = 0xf000003aU;
        internal const uint JIT_HELPER_FUNCTION_NAME = 0xf000003bU;
        internal const uint JUMP_THUNK_TARGET = 0xf000003cU;
        internal const uint LOADERHEAP_TRAVERSE = 0xf000003dU;
        internal const uint MANAGER_LIST = 0xf000003eU;
        internal const uint JITHEAPLIST = 0xf000003fU;
        internal const uint CODEHEAP_LIST = 0xf0000040U;
        internal const uint METHODTABLE_SLOT = 0xf0000041U;
        internal const uint VIRTCALLSTUBHEAP_TRAVERSE = 0xf0000042U;
        internal const uint NESTEDEXCEPTION_DATA = 0xf0000043U;
        internal const uint USEFULGLOBALS = 0xf0000044U;
        internal const uint CLRTLSDATA_INDEX = 0xf0000045U;
        internal const uint MODULE_FINDIL = 0xf0000046U;
        internal const uint CLR_WATSON_BUCKETS = 0xf0000047U;
        internal const uint OOM_DATA = 0xf0000048U;
        internal const uint OOM_STATIC_DATA = 0xf0000049U;
        internal const uint GCHEAP_HEAPANALYZE_DATA = 0xf000004aU;
        internal const uint GCHEAP_HEAPANALYZE_STATIC_DATA = 0xf000004bU;
        internal const uint HANDLETABLE_FILTERED_TRAVERSE = 0xf000004cU;
        internal const uint METHODDESC_TRANSPARENCY_DATA = 0xf000004dU;
        internal const uint EECLASS_TRANSPARENCY_DATA = 0xf000004eU;
        internal const uint THREAD_STACK_BOUNDS = 0xf000004fU;
        internal const uint HILL_CLIMBING_LOG_ENTRY = 0xf0000050U;
        internal const uint THREADPOOL_DATA_2 = 0xf0000051U;
        internal const uint THREADLOCALMODULE_DAT = 0xf0000052U;
    }

#pragma warning disable 0649
#pragma warning disable 0169

    [StructLayout(LayoutKind.Sequential)]
    // Same for v2 and v4
    internal struct LegacyModuleMapTraverseArgs
    {
        private readonly uint _setToZero;
        public ulong module;
        public IntPtr pCallback;
        public IntPtr token;
    }

    internal struct V2MethodDescData : IMethodDescData
    {
        private int _bHasNativeCode;
        private int _bIsDynamic;
        private short _wSlotNumber;
        // Useful for breaking when a method is jitted.
        private ulong _addressOfNativeCodeSlot;

        private ulong _EEClassPtr;

        private ulong _preStubAddr;
        private short _JITType;
        private ulong _GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        private ulong _managedDynamicMethodObject;

        public ulong MethodDesc { get; }

        public ulong Module { get; }

        public uint MDToken { get; }

        ulong IMethodDescData.NativeCodeAddr { get; }

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (_JITType == 1)
                    return MethodCompilationType.Jit;
                if (_JITType == 2)
                    return MethodCompilationType.Ngen;

                return MethodCompilationType.None;
            }
        }

        public ulong MethodTable { get; }

        public ulong GCInfo { get; }

        public ulong ColdStart => 0;

        public uint ColdSize => 0;

        public uint HotSize => 0;
    }

    internal struct V35MethodDescData : IMethodDescData
    {
        private int _bHasNativeCode;
        private int _bIsDynamic;
        private short _wSlotNumber;
        // Useful for breaking when a method is jitted.
        private ulong _addressOfNativeCodeSlot;

        private ulong _EEClassPtr;

        private short _JITType;
        private ulong _GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        private ulong _managedDynamicMethodObject;

        public ulong MethodTable { get; }

        public ulong MethodDesc { get; }

        public ulong Module { get; }

        public uint MDToken { get; }

        public ulong GCInfo { get; }

        ulong IMethodDescData.NativeCodeAddr { get; }

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (_JITType == 1)
                    return MethodCompilationType.Jit;
                if (_JITType == 2)
                    return MethodCompilationType.Ngen;

                return MethodCompilationType.None;
            }
        }

        public ulong ColdStart => 0;

        public uint ColdSize => 0;

        public uint HotSize => 0;
    }

    internal struct LegacyDomainLocalModuleData : IDomainLocalModuleData
    {
        private IntPtr _moduleID;

        public ulong AppDomainAddr { get; }

        public ulong ModuleID => (ulong)_moduleID.ToInt64();

        public ulong ClassData { get; }

        public ulong DynamicClassTable { get; }

        public ulong GCStaticDataStart { get; }

        public ulong NonGCStaticDataStart { get; }
    }

    internal struct LegacyObjectData : IObjectData
    {
        private ulong _eeClass;
        private ulong _methodTable;
        private uint _objectType;
        private uint _size;
        private uint _elementType;
        private uint _dwRank;
        private uint _dwNumComponents;
        private uint _dwComponentSize;
        private ulong _arrayBoundsPtr;
        private ulong _arrayLowerBoundsPtr;

        public ClrElementType ElementType => (ClrElementType)_elementType;
        public ulong ElementTypeHandle { get; }
        public ulong RCW => 0;
        public ulong CCW => 0;

        public ulong DataPointer { get; }
    }

    internal struct LegacyMethodTableData : IMethodTableData
    {
        public uint bIsFree; // everything else is NULL if this is true.
        public ulong eeClass;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumVtableSlots;
        public uint baseSize;
        public uint componentSize;
        public uint isShared; // flags & enum_flag_DomainNeutral
        public uint sizeofMethodTable;
        public uint isDynamic;
        public uint containsPointers;

        public bool ContainsPointers => containsPointers != 0;

        public uint BaseSize => baseSize;

        public uint ComponentSize => componentSize;

        public ulong EEClass => eeClass;

        public bool Free => bIsFree != 0;

        public ulong Parent => parentMethodTable;

        public bool Shared => isShared != 0;

        public uint NumMethods => wNumVtableSlots;

        public ulong ElementTypeHandle => throw new NotImplementedException();

        public uint Token => 0;

        public ulong Module => 0;
    }

    internal enum WorkRequestFunctionTypes
    {
        QUEUEUSERWORKITEM,
        TIMERDELETEWORKITEM,
        ASYNCCALLBACKCOMPLETION,
        ASYNCTIMERCALLBACKCOMPLETION,
        UNKNOWNWORKITEM
    }

    internal struct DacpWorkRequestData
    {
        public WorkRequestFunctionTypes FunctionType;
        public ulong Function;
        public ulong Context;
        public ulong NextWorkRequest;
    }

    internal struct V2ThreadPoolData : IThreadPoolData
    {
        private int _numQueuedWorkRequests;

        private uint _numTimers;

        private int _numCPThreads;
        private int _numRetiredCPThreads;
        private int _currentLimitTotalCPThreads;

        public int MinCP { get; }

        public int MaxCP { get; }

        public int CPU { get; }

        public int NumFreeCP { get; }

        public int MaxFreeCP { get; }

        public int TotalThreads { get; }

        public int RunningThreads { get; }

        public int IdleThreads { get; }

        public int MinThreads { get; }

        public int MaxThreads { get; }

        ulong IThreadPoolData.FirstWorkRequest { get; }

        public ulong QueueUserWorkItemCallbackFPtr { get; }

        public ulong AsyncCallbackCompletionFPtr { get; }

        public ulong AsyncTimerCallbackCompletionFPtr { get; }
    }

    internal struct V2ModuleData : IModuleData
    {
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public IntPtr metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public IntPtr dwBaseClassIndex;
        public IntPtr ModuleDefinition;
        public IntPtr dwDomainNeutralIndex;

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

        public ulong Assembly => assembly;

        public ulong ImageBase => ilBase;

        public ulong PEFile => peFile;

        public ulong LookupTableHeap => pLookupTableHeap;

        public ulong ThunkHeap => pThunkHeap;

        public IntPtr LegacyMetaDataImport => ModuleDefinition;

        public ulong ModuleId => (ulong)dwDomainNeutralIndex.ToInt64();

        public ulong ModuleIndex => 0;

        public bool IsReflection => bIsReflection != 0;

        public bool IsPEFile => bIsPEFile != 0;

        public ulong MetdataStart => metadataStart;

        public ulong MetadataLength => (ulong)metadataSize.ToInt64();
    }

    internal struct V2EEClassData : IEEClassData, IFieldInfo
    {
        public ulong methodTable;
        public ulong module;
        public short wNumVtableSlots;
        public short wNumMethodSlots;
        public short wNumInstanceFields;
        public short wNumStaticFields;
        public uint dwClassDomainNeutralIndex;
        public uint dwAttrClass; // cached metadata
        public uint token; // Metadata token

        public ulong addrFirstField; // If non-null, you can retrieve more

        public short wThreadStaticOffset;
        public short wThreadStaticsSize;
        public short wContextStaticOffset;
        public short wContextStaticsSize;

        public ulong Module => module;

        public ulong MethodTable => methodTable;

        public uint InstanceFields => (uint)wNumInstanceFields;

        public uint StaticFields => (uint)wNumStaticFields;

        public uint ThreadStaticFields => 0;

        public ulong FirstField => addrFirstField;
    }

    internal struct V2ThreadData : IThreadData
    {
        public uint corThreadId;
        public uint osThreadId;
        public int state;
        public uint preemptiveGCDisabled;
        public ulong allocContextPtr;
        public ulong allocContextLimit;
        public ulong context;
        public ulong domain;
        public ulong sharedStaticData;
        public ulong unsharedStaticData;
        public ulong pFrame;
        public uint lockCount;
        public ulong firstNestedException;
        public ulong teb;
        public ulong fiberData;
        public ulong lastThrownObjectHandle;
        public ulong nextThread;

        public ulong Next => IntPtr.Size == 8 ? nextThread : (uint)nextThread;

        public ulong AllocPtr => IntPtr.Size == 8 ? allocContextPtr : (uint)allocContextPtr;

        public ulong AllocLimit => IntPtr.Size == 8 ? allocContextLimit : (uint)allocContextLimit;

        public uint OSThreadID => osThreadId;

        public ulong Teb => IntPtr.Size == 8 ? teb : (uint)teb;

        public ulong AppDomain => domain;

        public uint LockCount => lockCount;

        public int State => state;

        public ulong ExceptionPtr => lastThrownObjectHandle;

        public uint ManagedThreadID => corThreadId;

        public bool Preemptive => preemptiveGCDisabled == 0;
    }

    internal struct V2SegmentData : ISegmentData
    {
        public ulong segmentAddr;
        public ulong allocated;
        public ulong committed;
        public ulong reserved;
        public ulong used;
        public ulong mem;
        public ulong next;
        public ulong gc_heap;
        public ulong highAllocMark;

        public ulong Address => segmentAddr;

        public ulong Next => next;

        public ulong Start => mem;

        public ulong End => allocated;

        public ulong Reserved => reserved;

        public ulong Committed => committed;
    }

    internal struct V2HeapDetails : IHeapDetails
    {
        public ulong heapAddr;
        public ulong alloc_allocated;

        public GenerationData generation_table0;
        public GenerationData generation_table1;
        public GenerationData generation_table2;
        public GenerationData generation_table3;
        public ulong ephemeral_heap_segment;
        public ulong finalization_fill_pointers0;
        public ulong finalization_fill_pointers1;
        public ulong finalization_fill_pointers2;
        public ulong finalization_fill_pointers3;
        public ulong finalization_fill_pointers4;
        public ulong finalization_fill_pointers5;
        public ulong lowest_address;
        public ulong highest_address;
        public ulong card_table;

        public ulong FirstHeapSegment => generation_table2.StartSegment;

        public ulong FirstLargeHeapSegment => generation_table3.StartSegment;

        public ulong EphemeralSegment => ephemeral_heap_segment;

        public ulong EphemeralEnd => alloc_allocated;

        public ulong EphemeralAllocContextPtr => generation_table0.AllocationContextPointer;

        public ulong EphemeralAllocContextLimit => generation_table0.AllocationContextLimit;

        public ulong FQAllObjectsStart => finalization_fill_pointers0;

        public ulong FQAllObjectsStop => finalization_fill_pointers3;

        public ulong FQRootsStart => finalization_fill_pointers3;

        public ulong FQRootsStop => finalization_fill_pointers5;

        public ulong Gen0Start => generation_table0.AllocationStart;

        public ulong Gen0Stop => alloc_allocated;

        public ulong Gen1Start => generation_table1.AllocationStart;

        public ulong Gen1Stop => generation_table0.AllocationStart;

        public ulong Gen2Start => generation_table2.AllocationStart;

        public ulong Gen2Stop => generation_table1.AllocationStart;
    }

    internal struct V4ThreadPoolData : IThreadPoolData
    {
        private uint _useNewWorkerPool;

        private int _numRetiredWorkerThreads;

        private ulong _hillClimbingLog;
        private int _hillClimbingLogFirstIndex;
        private int _hillClimbingLogSize;

        private uint _numTimers;

        private int _numCPThreads;
        private int _numRetiredCPThreads;
        private int _currentLimitTotalCPThreads;

        private ulong _queueUserWorkItemCallbackFPtr;
        private ulong _asyncCallbackCompletionFPtr;
        private ulong _asyncTimerCallbackCompletionFPtr;

        public int MinCP { get; }

        public int MaxCP { get; }

        public int CPU { get; }

        public int NumFreeCP { get; }

        public int MaxFreeCP { get; }

        public int TotalThreads { get; }

        public int RunningThreads => TotalThreads + IdleThreads + _numRetiredWorkerThreads;

        public int IdleThreads { get; }

        public int MinThreads { get; }

        public int MaxThreads { get; }

        public ulong FirstWorkRequest { get; }

        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr => ulong.MaxValue;

        ulong IThreadPoolData.AsyncCallbackCompletionFPtr => ulong.MaxValue;

        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => ulong.MaxValue;
    }

    internal struct V4EEClassData : IEEClassData, IFieldInfo
    {
        public ulong methodTable;
        public ulong module;
        public short wNumVtableSlots;
        public short wNumMethodSlots;
        public short wNumInstanceFields;
        public short wNumStaticFields;
        public short wNumThreadStaticFields;
        public uint dwClassDomainNeutralIndex;
        public uint dwAttrClass; // cached metadata
        public uint token; // Metadata token

        public ulong addrFirstField; // If non-null, you can retrieve more

        public short wContextStaticOffset;
        public short wContextStaticsSize;

        public ulong Module => module;

        ulong IEEClassData.MethodTable => methodTable;

        public uint InstanceFields => (uint)wNumInstanceFields;

        public uint StaticFields => (uint)wNumStaticFields;

        public uint ThreadStaticFields => 0;

        public ulong FirstField => addrFirstField;
    }

    internal struct V4ModuleData : IModuleData
    {
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public IntPtr metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public IntPtr dwBaseClassIndex;
        public IntPtr ModuleDefinition;
        public IntPtr dwModuleID;

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

        public IntPtr dwModuleIndex;

        public ulong PEFile => peFile;

        public ulong Assembly => assembly;

        public ulong ImageBase => ilBase;

        public ulong LookupTableHeap => pLookupTableHeap;

        public ulong ThunkHeap => pThunkHeap;

        public IntPtr LegacyMetaDataImport => ModuleDefinition;

        public ulong ModuleId => (ulong)dwModuleID.ToInt64();

        public ulong ModuleIndex => (ulong)dwModuleIndex.ToInt64();

        public bool IsReflection => bIsReflection != 0;

        public bool IsPEFile => bIsPEFile != 0;

        public ulong MetdataStart => metadataStart;

        public ulong MetadataLength => (ulong)metadataSize.ToInt64();
    }

    internal struct V45AllocData
    {
        public ulong allocBytes;
        public ulong allocBytesLoh;
    }

    internal struct V45GenerationAllocData
    {
        public ulong allocBytesGen0;
        public ulong allocBytesLohGen0;
        public ulong allocBytesGen1;
        public ulong allocBytesLohGen1;
        public ulong allocBytesGen2;
        public ulong allocBytesLohGen2;
        public ulong allocBytesGen3;
        public ulong allocBytesLohGen3;
    }
}