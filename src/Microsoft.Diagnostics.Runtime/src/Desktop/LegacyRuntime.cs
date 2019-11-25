// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class LegacyRuntime : DesktopRuntimeBase
    {
        // Buffer used for all name requests, this needs to be QUITE large because with anonymous types we can have
        // type names that are 8k+ long...
        private readonly byte[] _buffer = new byte[1024 * 32];
        private readonly DesktopVersion _version;
        private readonly int _patch;

        public LegacyRuntime(ClrInfo info, DataTarget dt, DacLibrary lib, DesktopVersion version, int patch)
            : base(info, dt, lib)
        {
            _version = version;
            _patch = patch;

            if (!GetCommonMethodTables(ref _commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsExceptionKind.DacError);

            if (!_commonMTs.Validate())
                CanWalkHeap = false;

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            Span<byte> tmp = stackalloc byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsExceptionKind.DacError);

            int v = tmp.AsInt32();
            if (v != 8)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsExceptionKind.DacError);
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
            HandleTableWalker handleTable = new HandleTableWalker(this);

            byte[] input;
            if (CLRVersion == DesktopVersion.v2)
                input = handleTable.V2Request;
            else
                input = handleTable.V4Request;

            // TODO:  Better to return partial data or null?  Maybe bool function return?
            //        I don't even think the dac api will fail unless there's a data read error.
            bool ret = Request(DacRequests.HANDLETABLE_TRAVERSE, input, null);
            if (!ret)
                Trace.WriteLine("Warning, GetHandles() method failed, returning partial results.");

            return handleTable.Handles;
        }

        internal override bool TraverseHeap(ulong heap, SOSDac.LoaderHeapTraverse callback)
        {
            Span<byte> input = stackalloc byte[sizeof(ulong) * 2];
            WriteValueToBuffer(heap, input, 0);
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong));

            return Request(DacRequests.LOADERHEAP_TRAVERSE, input, null);
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, SOSDac.LoaderHeapTraverse callback)
        {
            Span<byte> input = IntPtr.Size == 4
                ? (stackalloc byte[sizeof(ulong) * 2])
                : (stackalloc byte[sizeof(ulong) * 3]);

            WriteValueToBuffer(appDomain, input, 0);
            WriteValueToBuffer(type, input, sizeof(ulong));
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong) + sizeof(int));

            return Request(DacRequests.VIRTCALLSTUBHEAP_TRAVERSE, input, null);
        }

        internal override ulong GetFirstThread()
        {
            IThreadStoreData threadStore = GetThreadStoreData();
            return threadStore != null ? threadStore.FirstThread : 0;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (addr == 0)
                return null;

            byte[] input = new byte[2 * sizeof(ulong)];
            Buffer.BlockCopy(BitConverter.GetBytes(addr), 0, input, 0, sizeof(ulong));

            if (CLRVersion == DesktopVersion.v2)
                return Request<IThreadData, V2ThreadData>(DacRequests.THREAD_DATA, input);

            ThreadData result = (ThreadData)Request<IThreadData, ThreadData>(DacRequests.THREAD_DATA, input);
            if (IntPtr.Size == 4)
                result = new ThreadData(ref result);
            return result;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);

            HeapDetails result = (HeapDetails)Request<IHeapDetails, HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);
            result = new HeapDetails(ref result);
            return result;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);

            HeapDetails result = (HeapDetails)Request<IHeapDetails, HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);
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

        internal override IMethodTableCollectibleData GetMethodTableCollectibleData(ulong addr)
        {
            throw new NotImplementedException();
        }

        internal override IGCInfo GetGCInfoImpl()
        {
            return Request<IGCInfo, GCInfo>(DacRequests.GCHEAP_DATA);
        }

        internal override ISegmentData GetSegmentData(ulong segmentAddr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<ISegmentData, V2SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);

            ISegmentData result = Request<ISegmentData, SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);
            if (IntPtr.Size == 4 && result != null)
            {
                // fixup pointers
                SegmentData s = (SegmentData)result;
                result = new SegmentData(ref s);
            }

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

        public override string GetMethodTableName(ulong mt)
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

            IModuleData result;
            if (CLRVersion == DesktopVersion.v2)
            {
                V2ModuleData data = new V2ModuleData();
                if (!RequestStruct(DacRequests.MODULE_DATA, addr, ref data))
                    return null;

                COMHelper.Release(data.MetaDataImport);
                result = data;
            }
            else
            {
                V4ModuleData data = new V4ModuleData();
                if (!RequestStruct(DacRequests.MODULE_DATA, addr, ref data))
                    return null;

                COMHelper.Release(data.MetaDataImport);
                result = data;
            }

            return result;
        }

        internal override ulong GetModuleForMT(ulong mt)
        {
            if (mt == 0)
                return 0;

            IMethodTableData mtData = GetMethodTableData(mt);
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
            Span<byte> output = stackalloc byte[sizeof(int)];
            if (Request(DacRequests.JITLIST, null, output))
            {
                int JitManagerSize = Marshal.SizeOf(typeof(JitManagerInfo));
                int count = output.AsInt32();
                int size = JitManagerSize * count;

                if (size > 0)
                {
                    byte[] outputArray = new byte[size];
                    if (Request(DacRequests.MANAGER_LIST, null, outputArray))
                    {
                        MutableJitCodeHeapInfo heapInfo = new MutableJitCodeHeapInfo();
                        int CodeHeapTypeOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "codeHeapType").ToInt32();
                        int AddressOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "address").ToInt32();
                        int CurrAddrOffset = Marshal.OffsetOf(typeof(JitCodeHeapInfo), "currentAddr").ToInt32();
                        int JitCodeHeapInfoSize = Marshal.SizeOf(typeof(JitCodeHeapInfo));

                        for (int i = 0; i < count; ++i)
                        {
                            int type = BitConverter.ToInt32(outputArray, i * JitManagerSize + sizeof(ulong));

                            // Is this code heap IL?
                            if ((type & 3) != 0)
                                continue;

                            ulong address = BitConverter.ToUInt64(outputArray, i * JitManagerSize);
                            Span<byte> jitManagerBuffer = stackalloc byte[sizeof(ulong) * 2];
                            WriteValueToBuffer(address, jitManagerBuffer, 0);

                            if (Request(DacRequests.JITHEAPLIST, jitManagerBuffer, jitManagerBuffer))
                            {
                                int heapCount = Unsafe.ReadUnaligned<int>(ref jitManagerBuffer[sizeof(ulong)]);

                                byte[] codeHeapBuffer = new byte[heapCount * JitCodeHeapInfoSize];
                                if (Request(DacRequests.CODEHEAP_LIST, jitManagerBuffer, codeHeapBuffer))
                                {
                                    for (int j = 0; j < heapCount; ++j)
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
            IMethodTableData mtData = GetMethodTableData(mt);

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
            IntPtr import;
            if (CLRVersion == DesktopVersion.v2)
            {
                V2ModuleData data = new V2ModuleData();
                if (!RequestStruct(DacRequests.MODULE_DATA, module, ref data))
                    return null;

                import = data.MetaDataImport;
            }
            else
            {
                V4ModuleData data = new V4ModuleData();
                if (!RequestStruct(DacRequests.MODULE_DATA, module, ref data))
                    return null;

                import = data.MetaDataImport;
            }

            try
            {
                return import != IntPtr.Zero ? new MetaDataImport(DacLibrary, import) : null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
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

        internal override IDomainLocalModuleData GetDomainLocalModuleById(ulong appDomain, ulong id)
        {
            byte[] inout = GetByteArrayForStruct<LegacyDomainLocalModuleData>();

            int i = WriteValueToBuffer(appDomain, inout, 0);
            WriteValueToBuffer(id.AsIntPtr(), inout, i);

            if (Request(DacRequests.DOMAINLOCALMODULEFROMAPPDOMAIN_DATA, null, inout))
                return ConvertStruct<IDomainLocalModuleData, LegacyDomainLocalModuleData>(inout);

            return null;
        }

        internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
        {
            List<MethodTableTokenPair> mts = new List<MethodTableTokenPair>();

            SOSDac.ModuleMapTraverse traverse = delegate (uint index, ulong mt, IntPtr token) { mts.Add(new MethodTableTokenPair(mt, index)); };
            LegacyModuleMapTraverseArgs args = new LegacyModuleMapTraverseArgs
            {
                Callback = Marshal.GetFunctionPointerForDelegate(traverse),
                Module = module
            };

            // TODO:  Blah, theres got to be a better way to do this.
            byte[] input = GetByteArrayForStruct<LegacyModuleMapTraverseArgs>();
            IntPtr mem = Marshal.AllocHGlobal(input.Length);
            Marshal.StructureToPtr(args, mem, true);
            Marshal.Copy(mem, input, 0, input.Length);
            Marshal.FreeHGlobal(mem);

            bool r = Request(DacRequests.MODULEMAP_TRAVERSE, input, null);

            GC.KeepAlive(traverse);

            return mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong module)
        {
            DacLibrary.DacDataTarget.SetNextCurrentThreadId(0x12345678);
            DacLibrary.DacDataTarget.SetNextTLSValue(appDomain);

            IDomainLocalModuleData result = Request<IDomainLocalModuleData, LegacyDomainLocalModuleData>(DacRequests.DOMAINLOCALMODULE_DATA_FROM_MODULE, module);

            DacLibrary.DacDataTarget.SetNextCurrentThreadId(null);
            DacLibrary.DacDataTarget.SetNextTLSValue(null);

            return result;
        }

        private ulong GetMethodDescFromIp(ulong ip)
        {
            if (ip == 0)
                return 0;

            IMethodDescData data = Request<IMethodDescData, V35MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);
            if (data == null)
                data = Request<IMethodDescData, V2MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);

            if (data == null)
            {
                CodeHeaderData codeHeaderData = new CodeHeaderData();
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
            uint token = uint.MaxValue;

            IMethodTableData mtData = GetMethodTableData(mt);
            if (mtData != null)
            {
                byte[] buffer = CLRVersion == DesktopVersion.v2 ? GetByteArrayForStruct<V2EEClassData>() : GetByteArrayForStruct<V4EEClassData>();
                if (Request(DacRequests.EECLASS_DATA, mtData.EEClass, buffer))
                {
                    if (CLRVersion == DesktopVersion.v2)
                    {
                        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        V2EEClassData result = (V2EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V2EEClassData));
                        handle.Free();

                        token = result.Token;
                    }
                    else
                    {
                        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        V4EEClassData result = (V4EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V4EEClassData));
                        handle.Free();

                        token = result.Token;
                    }
                }
            }

            return token;
        }

        protected override DesktopStackFrame GetStackFrame(DesktopThread thread, byte[] context, ulong ip, ulong sp, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            ClearBuffer();

            if (frameVtbl != 0)
            {
                ClrMethod method = null;
                string frameName = "Unknown Frame";
                if (Request(DacRequests.FRAME_NAME, frameVtbl, _buffer))
                    frameName = BytesToString(_buffer);

                IMethodDescData mdData = GetMethodDescData(DacRequests.METHODDESC_FRAME_DATA, sp);
                if (mdData != null)
                    method = DesktopMethod.Create(this, mdData);

                frame = new DesktopStackFrame(this, thread, context, sp, frameName, method);
            }
            else
            {
                ulong md = GetMethodDescFromIp(ip);
                frame = new DesktopStackFrame(this, thread, context, ip, sp, md);
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
            List<ClrStackFrame> result = new List<ClrStackFrame>();

            if (!GetStackTraceFromField(type, obj, out ulong _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            ClrHeapImpl heap = (ClrHeapImpl)Heap;
            ClrType stackTraceType = heap.GetObjectType(_stackTrace);

            if (stackTraceType == null)
                stackTraceType = heap.ArrayType;

            if (!stackTraceType.IsArray)
                return result;

            int len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            int elementSize = CLRVersion == DesktopVersion.v2 ? IntPtr.Size * 4 : IntPtr.Size * 3;
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

                if (i == 0)
                    thread = (DesktopThread)GetThreadByStackAddress(sp);

                result.Add(new DesktopStackFrame(this, thread, null, ip, sp, md));

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
            IMethodTableData mtData = Request<IMethodTableData, LegacyMethodTableData>(DacRequests.METHODTABLE_DATA, methodTable);
            ulong[] values = new ulong[mtData.NumMethods];

            if (mtData.NumMethods == 0)
                return values;

            CodeHeaderData codeHeader = new CodeHeaderData();
            Span<byte> slotArgs = stackalloc byte[0x10];
            Span<byte> result = stackalloc byte[sizeof(ulong)];

            WriteValueToBuffer(methodTable, slotArgs, 0);
            for (int i = 0; i < mtData.NumMethods; ++i)
            {
                WriteValueToBuffer(i, slotArgs, sizeof(ulong));
                if (!Request(DacRequests.METHODTABLE_SLOT, slotArgs, result))
                    continue;

                ulong ip = result.AsUInt64();

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
            ThreadStoreData threadStore = new ThreadStoreData();
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
            IMethodDescData result = GetMethodDescData(DacRequests.METHODDESC_IP_DATA, ip);
            if (result != null)
                return result;

            ulong methodDesc = GetMethodDescFromIp(ip);
            if (methodDesc != 0)
                return GetMethodDescData(DacRequests.METHODDESC_DATA, ip);

            return null;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            IThreadPoolData data = GetThreadPoolData();

            if (_version == DesktopVersion.v2)
            {
                ulong curr = data.FirstWorkRequest;
                byte[] bytes = GetByteArrayForStruct<DacpWorkRequestData>();

                while (Request(DacRequests.WORKREQUEST_DATA, curr, bytes))
                {
                    GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    DacpWorkRequestData result = (DacpWorkRequestData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(DacpWorkRequestData));
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
                CodeHeaderData codeHeaderData = new CodeHeaderData();

                if (RequestStruct(DacRequests.CODEHEADER_DATA, addr, ref codeHeaderData))
                    result = GetMethodDescData(DacRequests.METHODDESC_DATA, codeHeaderData.MethodDesc);
            }

            return result;
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            Span<byte> input = stackalloc byte[sizeof(uint)];
            WriteValueToBuffer(threadId, input, 0);

            Span<byte> output = stackalloc byte[sizeof(ulong)];
            if (!Request(DacRequests.THREAD_THINLOCK_DATA, input, output))
                return 0;

            return output.AsUInt64();
        }

        internal override int GetSyncblkCount()
        {
            ISyncBlkData data = Request<ISyncBlkData, SyncBlockData>(DacRequests.SYNCBLOCK_DATA, 1);
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
            Span<byte> value = stackalloc byte[sizeof(uint)];
            if (!Request(DacRequests.CLRTLSDATA_INDEX, null, value))
                return uint.MaxValue;

            return value.AsUInt32();
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

        public override string GetJitHelperFunctionName(ulong addr)
        {
            ClearBuffer();
            if (!Request(DacRequests.JIT_HELPER_FUNCTION_NAME, addr, _buffer))
                return null;

            int len = Array.IndexOf(_buffer, (byte)0);
            Debug.Assert(len >= 0);
            if (len < 0)
                return null;
            return Encoding.ASCII.GetString(_buffer, 0, len);
        }


        private bool Request(uint id, ulong param, byte[] output)
        {
            Span<byte> input = stackalloc byte[sizeof(ulong)];
            Unsafe.As<byte, ulong>(ref MemoryMarshal.GetReference(input)) = param;

            return Request(id, input, output);
        }

        private bool Request(uint id, uint param, byte[] output)
        {
            Span<byte> input = stackalloc byte[sizeof(uint)];
            Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(input)) = param;

            return Request(id, input, output);
        }

        private bool Request(uint id, ReadOnlySpan<byte> input, Span<byte> output)
        {
            int result = _dacInterface.Request(id, input, output);
            return result >= 0;
        }

        private I Request<I, T>(uint id, byte[] input)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, input, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        private I Request<I, T>(uint id, ulong param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        private I Request<I, T>(uint id, uint param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        private I Request<I, T>(uint id)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        private bool RequestStruct<T>(uint id, ref T t)
            where T : struct
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        private bool RequestStruct<T>(uint id, ulong addr, ref T t)
            where T : struct
        {
            Span<byte> input = stackalloc byte[sizeof(ulong)];
            byte[] output = GetByteArrayForStruct<T>();

            WriteValueToBuffer(addr, input, 0);

            if (!Request(id, input, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        private ulong[] RequestAddrList(uint id, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, null, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }

        private ulong[] RequestAddrList(uint id, ulong param, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, param, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }

        private static string BytesToString(byte[] output)
        {
            int len = 0;
            while (len < output.Length && (output[len] != 0 || output[len + 1] != 0))
                len += 2;

            if (len > output.Length)
                len = output.Length;

            return Encoding.Unicode.GetString(output, 0, len);
        }

        private byte[] GetByteArrayForStruct<T>()
            where T : struct
        {
            return new byte[Marshal.SizeOf(typeof(T))];
        }

        private I ConvertStruct<I, T>(byte[] bytes)
            where I : class
            where T : I
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            I result = (I)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return result;
        }


        private int WriteValueToBuffer(IntPtr ptr, Span<byte> buffer, int offset)
        {
            ulong value = (ulong)ptr.ToInt64();
            for (int i = offset; i < offset + IntPtr.Size; ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + IntPtr.Size;
        }

        private int WriteValueToBuffer(int value, Span<byte> buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        private int WriteValueToBuffer(uint value, Span<byte> buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        private int WriteValueToBuffer(ulong value, Span<byte> buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(ulong); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(ulong);
        }

        private class HandleTableWalker
        {
            private readonly DesktopRuntimeBase _runtime;
            private readonly ClrHeap _heap;
            private int _max = 10000;
            private VISITHANDLEV2 _mV2Delegate;
            private VISITHANDLEV4 _mV4Delegate;

            public List<ClrHandle> Handles { get; }
            public byte[] V4Request
            {
                get
                {
                    // MULTITHREAD ISSUE
                    if (_mV4Delegate == null)
                        _mV4Delegate = VisitHandleV4;

                    IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV4Delegate);
                    byte[] request = new byte[IntPtr.Size * 2];
                    FunctionPointerToByteArray(functionPtr, request, 0);

                    return request;
                }
            }

            public byte[] V2Request
            {
                get
                {
                    // MULTITHREAD ISSUE
                    if (_mV2Delegate == null)
                        _mV2Delegate = VisitHandleV2;

                    IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(_mV2Delegate);
                    byte[] request = new byte[IntPtr.Size * 2];

                    FunctionPointerToByteArray(functionPtr, request, 0);

                    return request;
                }
            }

            public HandleTableWalker(DesktopRuntimeBase dac)
            {
                _runtime = dac;
                _heap = dac.Heap;
                Handles = new List<ClrHandle>();
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int VISITHANDLEV4(ulong HandleAddr, ulong HandleValue, int HandleType, uint ulRefCount, ulong appDomainPtr, IntPtr token);

            private int VisitHandleV4(ulong addr, ulong obj, int hndType, uint refCnt, ulong appDomain, IntPtr unused)
            {
                Debug.Assert(unused == IntPtr.Zero);

                return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int VISITHANDLEV2(ulong HandleAddr, ulong HandleValue, int HandleType, ulong appDomainPtr, IntPtr token);

            private int VisitHandleV2(ulong addr, ulong obj, int hndType, ulong appDomain, IntPtr unused)
            {
                Debug.Assert(unused == IntPtr.Zero);

                // V2 cannot actually get the ref count from a handle.  We'll always report the RefCount as
                // 1 in this case so the user will treat this as a strong handle (which the majority of COM
                // handles are).
                uint refCnt = 0;
                if (hndType == (uint)HandleType.RefCount)
                    refCnt = 1;

                return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
            }

            public int AddHandle(ulong addr, ulong obj, int hndType, uint refCnt, uint dependentTarget, ulong appDomain)
            {
                // If we fail to get the MT of this object, just skip it and keep going
                if (!GetMethodTables(obj, out _, out _))
                    return _max-- > 0 ? 1 : 0;

                ClrHandle handle = new ClrHandle
                {
                    Address = addr,
                    Object = obj,
                    Type = _heap.GetObjectType(obj),
                    HandleType = (HandleType)hndType,
                    RefCount = refCnt,
                    AppDomain = _runtime.GetAppDomainByAddress(appDomain),
                    DependentTarget = dependentTarget
                };

                if (dependentTarget != 0)
                    handle.DependentType = _heap.GetObjectType(dependentTarget);

                Handles.Add(handle);
                handle = handle.GetInteriorHandle();
                if (handle != null)
                    Handles.Add(handle);

                // Stop if we have too many handles (likely infinite loop in dac due to
                // inconsistent data).
                return _max-- > 0 ? 1 : 0;
            }

            private bool GetMethodTables(ulong obj, out ulong mt, out ulong cmt)
            {
                mt = 0;
                cmt = 0;

                Span<byte> data = stackalloc byte[IntPtr.Size * 3];
                if (!_runtime.ReadMemory(obj, data, out int read) || read != data.Length)
                    return false;

                mt = data.AsPointer(0);
                if (mt == _runtime.ArrayMethodTable)
                    cmt = data.AsPointer(2);

                return true;
            }

            private static void FunctionPointerToByteArray(IntPtr functionPtr, byte[] request, int start)
            {
                long ptr = functionPtr.ToInt64();

                for (int i = start; i < start + sizeof(ulong); ++i)
                {
                    request[i] = (byte)ptr;
                    ptr >>= 8;
                }
            }
        }
    }
}
