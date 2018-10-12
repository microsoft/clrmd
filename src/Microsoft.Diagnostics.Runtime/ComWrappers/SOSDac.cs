using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.ComWrappers
{

    internal unsafe sealed class SOSDac : CallableCOMWrapper
    {
        private static Guid IID_ISOSDac = new Guid("436f00f2-b42a-4b9f-870c-e73db66ae930");
        private ISOSDacVTable* VTable => (ISOSDacVTable*)_vtable;

        public SOSDac(IntPtr ptr)
            : base(ref IID_ISOSDac, ptr)
        {
        }

        const int CharBufferSize = 256;
        private byte[] _buffer = new byte[CharBufferSize];


        private DacGetIntPtr _getHandleEnum;
        private DacGetIntPtrWithArg _getStackRefEnum;
        private DacGetThreadData _getThreadData;
        private DacGetHeapDetailsWithArg _getGCHeapDetails;
        private DacGetHeapDetails _getGCHeapStaticData;
        private DacGetUlongArray _getGCHeapList;
        private DacGetUlongArray _getAppDomainList;
        private DacGetUlongArrayWithArg _getAssemblyList;
        private DacGetUlongArrayWithArg _getModuleList;
        private DacGetAssemblyData _getAssemblyData;
        private DacGetADStoreData _getAppDomainStoreData;
        private DacGetMTData _getMethodTableData;
        private DacGetUlongWithArg _getMTForEEClass;
        private DacGetGCInfoData _getGCHeapData;
        private DacGetCommonMethodTables _getCommonMethodTables;
        private DacGetCharArrayWithArg _getMethodTableName;
        private DacGetCharArrayWithArg _getPEFileName;
        private DacGetCharArrayWithArg _getAppDomainName;
        private DacGetCharArrayWithArg _getAssemblyName;
        private DacGetCharArrayWithArg _getAppBase;
        private DacGetCharArrayWithArg _getConfigFile;
        private DacGetModuleData _getModuleData;
        private DacGetSegmentData _getSegmentData;
        private DacGetAppDomainData _getAppDomainData;
        private DacGetJitManagers _getJitManagers;
        private DacTraverseLoaderHeap _traverseLoaderHeap;
        private DacTraverseStubHeap _traverseStubHeap;
        private DacTraverseModuleMap _traverseModuleMap;
        private DacGetFieldInfo _getFieldInfo;
        private DacGetFieldData _getFieldData;
        private DacGetObjectData _getObjectData;
        private DacGetCCWData _getCCWData;
        private DacGetRCWData _getRCWData;
        private DacGetCharArrayWithArg _getFrameName;
        private DacGetUlongWithArg _getMethodDescPtrFromFrame;
        private DacGetUlongWithArg _getMethodDescPtrFromIP;
        private DacGetCodeHeaderData _getCodeHeaderData;
        private DacGetSyncBlockData _getSyncBlock;
        private DacGetThreadPoolData _getThreadPoolData;
        private DacGetWorkRequestData _getWorkRequestData;
        private DacGetDomainLocalModuleDataFromAppDomain _getDomainLocalModuleDataFromAppDomain;
        private DacGetLocalModuleData _getDomainLocalModuleDataFromModule;
        private DacGetCodeHeaps _getCodeHeaps;
        private DacGetCOMPointers _getCCWInterfaces;
        private DacGetCOMPointers _getRCWInterfaces;
        private DacGetUlongWithArgs _getILForModule;
        private DacGetThreadLocalModuleData _getThreadLocalModuleData;
        private DacGetUlongWithArgs _getMethodTableSlot;
        private DacGetCharArrayWithArg _getMethodDescName;
        private DacGetThreadFromThinLock _getThreadFromThinlockId;
        private DacGetUInt _getTlsIndex;
        private DacGetThreadStoreData _getThreadStoreData;
        private GetMethodDescDataDelegate _getMethodDescData;
        private GetMetaDataImportDelegate _getMetaData;

        public bool GetMethodDescData(ulong md, ulong ip, out V45MethodDescData data)
        {
            InitDelegate(ref _getMethodDescData, VTable->GetMethodDescData);
            return _getMethodDescData(Self, md, ip, out data, 0, null, out int needed) == S_OK;
        }

        public bool GetThreadStoreData(out LegacyThreadStoreData data)
        {
            InitDelegate(ref _getThreadStoreData, VTable->GetThreadStoreData);
            return _getThreadStoreData(Self, out data) == S_OK;
        }


        public uint GetTlsIndex()
        {
            InitDelegate(ref _getTlsIndex, VTable->GetTLSIndex);
            if (_getTlsIndex(Self, out uint index) == S_OK)
                return index;

            return uint.MaxValue;
        }

        public ulong GetThreadFromThinlockId(uint id)
        {
            InitDelegate(ref _getThreadFromThinlockId, VTable->GetThreadFromThinlockID);
            if (_getThreadFromThinlockId(Self, id, out ulong thread) == S_OK)
                return thread;

            return 0;
        }


        public string GetMethodDescName(ulong md)
        {
            if (md == 0)
                return null;

            InitDelegate(ref _getMethodDescName, VTable->GetMethodDescName);

            if (_getMethodDescName(Self, md, 0, null, out int needed) < S_OK)
                return null;

            byte[] buffer = AcquireBuffer(needed * 2);

            if (_getMethodDescName(Self, md, needed, buffer, out int actuallyNeeded) < S_OK)
                return null;

            // Patch for a bug on sos side :
            //  Sometimes, when the target method has parameters with generic types
            //  the first call to GetMethodDescName sets an incorrect value into pNeeded.
            //  In those cases, a second call directly after the first returns the correct value.
            if (needed != actuallyNeeded)
            {
                ReleaseBuffer(buffer);
                buffer = AcquireBuffer(actuallyNeeded * 2);
                if (_getMethodDescName(Self, md, actuallyNeeded, buffer, out actuallyNeeded) < S_OK)
                    return null;
            }

            ReleaseBuffer(buffer);
            return string.Intern(Encoding.Unicode.GetString(buffer, 0, (actuallyNeeded - 1) * 2));
        }

        public ulong GetMethodTableSlot(ulong mt, uint slot)
        {
            if (mt == 0)
                return 0;

            InitDelegate(ref _getMethodTableSlot, VTable->GetMethodTableSlot);

            if (_getMethodTableSlot(Self, mt, slot, out ulong ip) == S_OK)
                return ip;

            return 0;
        }

        public bool GetThreadLocalModuleData(ulong thread, uint index, out V45ThreadLocalModuleData data)
        {
            InitDelegate(ref _getThreadLocalModuleData, VTable->GetThreadLocalModuleData);

            return _getThreadLocalModuleData(Self, thread, index, out data) == S_OK;
        }

        public ulong GetILForModule(ulong moduleAddr, uint rva)
        {
            InitDelegate(ref _getILForModule, VTable->GetILForModule);

            int hr = _getILForModule(Self, moduleAddr, rva, out ulong result);
            return hr == S_OK ? result : 0;
        }

        public COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getCCWInterfaces, VTable->GetCCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getCCWInterfaces(Self, ccw, count, data, out int pNeeded) >= 0)
                return data;

            return null;
        }

        public COMInterfacePointerData[] GetRCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getRCWInterfaces, VTable->GetRCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getRCWInterfaces(Self, ccw, count, data, out int pNeeded) >= 0)
                return data;

            return null;
        }
        public IDomainLocalModuleData GetDomainLocalModuleDataFromModule(ulong module)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromModule, VTable->GetDomainLocalModuleDataFromModule);
            int res = _getDomainLocalModuleDataFromModule(Self, module, out V45DomainLocalModuleData data);
            if (res < 0)
                return null;

            return data;
        }

        public IDomainLocalModuleData GetDomainLocalModuleDataFromAppDomain(ulong appDomain, int id)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromAppDomain, VTable->GetDomainLocalModuleDataFromAppDomain);
            int res = _getDomainLocalModuleDataFromAppDomain(Self, appDomain, id, out V45DomainLocalModuleData data);
            if (res < 0)
                return null;

            return data;
        }

        public bool GetWorkRequestData(ulong request, out V45WorkRequestData data)
        {
            InitDelegate(ref _getWorkRequestData, VTable->GetWorkRequestData);
            int hr = _getWorkRequestData(Self, request, out data);
            return hr == S_OK;
        }

        public IThreadPoolData GetThreadPoolData()
        {
            InitDelegate(ref _getThreadPoolData, VTable->GetThreadpoolData);
            if (_getThreadPoolData(Self, out V45ThreadPoolData data) == S_OK)
                return data;

            return null;
        }
        
        public ISyncBlkData GetSyncBlockData(int index)
        {
            InitDelegate(ref _getSyncBlock, VTable->GetSyncBlockData);
            int hr = _getSyncBlock(Self, index, out LegacySyncBlkData data);

            return hr == S_OK ? (ISyncBlkData)data: null;
        }

        public string GetAppBase(ulong domain)
        {
            InitDelegate(ref _getAppBase, VTable->GetApplicationBase);
            return GetString(_getAppBase, domain);
        }

        public string GetConfigFile(ulong domain)
        {
            InitDelegate(ref _getConfigFile, VTable->GetAppDomainConfigFile);
            return GetString(_getConfigFile, domain);
        }

        public bool GetCodeHeaderData(ulong ip, out CodeHeaderData codeHeaderData)
        {
            if (ip == 0)
            {
                codeHeaderData = new CodeHeaderData();
                return false;
            }

            InitDelegate(ref _getCodeHeaderData, VTable->GetCodeHeaderData);

            int hr = _getCodeHeaderData(Self, ip, out codeHeaderData);
            return hr == S_OK;
        }

        public ulong GetMethodDescPtrFromFrame(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromFrame, VTable->GetMethodDescPtrFromFrame);
            if (_getMethodDescPtrFromFrame(Self, frame, out ulong data) == S_OK)
                return data;

            return 0;
        }

        public ulong GetMethodDescPtrFromIP(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromIP, VTable->GetMethodDescPtrFromIP);
            if (_getMethodDescPtrFromIP(Self, frame, out ulong data) == S_OK)
                return data;

            return 0;
        }

        public string GetFrameName(ulong vtable)
        {
            InitDelegate(ref _getFrameName, VTable->GetFrameName);
            return GetString(_getFrameName, vtable) ?? "Unknown Frame";
        }

        public IFieldInfo GetFieldInfo(ulong mt)
        {
            InitDelegate(ref _getFieldInfo, VTable->GetMethodTableFieldData);
            if (_getFieldInfo(Self, mt, out V4FieldInfo data) == S_OK)
                return data;

            return null;
        }

        public IFieldData GetFieldData(ulong fieldDesc)
        {
            InitDelegate(ref _getFieldData, VTable->GetFieldDescData);
            if (_getFieldData(Self, fieldDesc, out LegacyFieldData data) == S_OK)
                return data;

            return null;
        }

        public IObjectData GetObjectData(ulong obj)
        {
            InitDelegate(ref _getObjectData, VTable->GetObjectData);
            if (_getObjectData(Self, obj, out V45ObjectData data) == S_OK)
                return data;

            return null;
        }

        public ICCWData GetCCWData(ulong ccw)
        {
            InitDelegate(ref _getCCWData, VTable->GetCCWData);
            if (_getCCWData(Self, ccw, out V45CCWData data) == S_OK)
                return data;

            return null;
        }

        public IRCWData GetRCWData(ulong rcw)
        {
            InitDelegate(ref _getRCWData, VTable->GetRCWData);
            if (_getRCWData(Self, rcw, out V45RCWData data) == S_OK)
                return data;

            return null;
        }

        public MetaDataImport GetMetadataImport(ulong module)
        {
            if (module == 0)
                return null;

            InitDelegate(ref _getMetaData, VTable->GetMetaDataImport);
            if (_getMetaData(Self, module, out IntPtr iunk) != S_OK)
                return null;

            return new MetaDataImport(iunk);
        }

        public bool GetCommonMethodTables(out CommonMethodTables commonMTs)
        {
            InitDelegate(ref _getCommonMethodTables, VTable->GetUsefulGlobals);
            return _getCommonMethodTables(Self, out commonMTs) == S_OK;
        }

        public ulong[] GetAssemblyList(ulong appDomain, int count) => GetModuleOrAssembly(appDomain, count, ref _getAssemblyList, VTable->GetAssemblyList);

        public IAssemblyData GetAssemblyData(ulong domain, ulong assembly)
        {
            InitDelegate(ref _getAssemblyData, VTable->GetAssemblyData);

            if (_getAssemblyData(Self, domain, assembly, out LegacyAssemblyData data) != S_OK)
            {
                // The dac seems to have an issue where the assembly data can be filled in for a minidump.
                // If the data is partially filled in, we'll use it.
                if (data.Address != assembly)
                    return null;
            }

            return data;
        }

        public IAppDomainData GetAppDomainData(ulong addr)
        {
            InitDelegate(ref _getAppDomainData, VTable->GetAppDomainData);
            
            if (_getAppDomainData(Self, addr, out LegacyAppDomainData data) != S_OK)
            {
                // We can face an exception while walking domain data if we catch the process
                // at a bad state.  As a workaround we will return partial data if data.Address
                // and data.StubHeap are set.
                if (data.Address != addr && data.StubHeap == 0)
                    return null;
            }

            return data;
        }

        public string GetAppDomainName(ulong appDomain)
        {
            InitDelegate(ref _getAppDomainName, VTable->GetAppDomainName);
            return GetString(_getAppDomainName, appDomain);
        }

        public string GetAssemblyName(ulong assembly)
        {
            InitDelegate(ref _getAssemblyName, VTable->GetAssemblyName);
            return GetString(_getAssemblyName, assembly);
        }

        public IAppDomainStoreData GetAppDomainStoreData()
        {
            InitDelegate(ref _getAppDomainStoreData, VTable->GetAppDomainStoreData);
            if (_getAppDomainStoreData(Self, out LegacyAppDomainStoreData data) < 0)
                return null;

            return data;
        }

        public IMethodTableData GetMethodTableData(ulong addr)
        {
            InitDelegate(ref _getMethodTableData, VTable->GetMethodTableData);
            if (_getMethodTableData(Self, addr, out V45MethodTableData data) == S_OK)
                return data;

            return null;
        }
        

        public string GetMethodTableName(ulong mt)
        {
            InitDelegate(ref _getMethodTableName, VTable->GetMethodTableName);
            return GetString(_getMethodTableName, mt);
        }

        public string GetPEFileName(ulong pefile)
        {
            InitDelegate(ref _getPEFileName, VTable->GetPEFileName);
            return GetString(_getPEFileName, pefile);
        }

        private string GetString(DacGetCharArrayWithArg func, ulong addr)
        {
            int hr = func(Self, addr, 0, null, out int needed);
            if (hr != S_OK)
                return null;

            if (needed == 0)
                return "";
            
            byte[] buffer = AcquireBuffer(needed * 2);
            hr = func(Self, addr, needed, buffer, out needed);
            if (hr != S_OK)
            {
                ReleaseBuffer(buffer);
                return null;
            }

            string result = Encoding.Unicode.GetString(buffer, 0, (needed - 1) * 2);

            ReleaseBuffer(buffer);
            return result;
        }

        private byte[] AcquireBuffer(int size)
        {
            if (_buffer == null)
                _buffer = new byte[256];

            if (size > _buffer.Length)
                return new byte[size];

            byte[] result = _buffer;
            _buffer = null;
            return result;
        }

        private void ReleaseBuffer(byte[] buffer)
        {
            if (buffer.Length == CharBufferSize)
                _buffer = buffer;
        }

        public ulong GetMethodTableByEEClass(ulong eeclass)
        {
            InitDelegate(ref _getMTForEEClass, VTable->GetMethodTableForEEClass);
            if (_getMTForEEClass(Self, eeclass, out ulong data) == S_OK)
                return data;

            return 0;
        }


        public IModuleData GetModuleData(ulong module)
        {
            InitDelegate(ref _getModuleData, VTable->GetModuleData);
            if (_getModuleData(Self, module, out V45ModuleData data) == S_OK)
                return data;

            return null;
        }

        public ulong[] GetModuleList(ulong assembly, int count) => GetModuleOrAssembly(assembly, count, ref _getModuleList, VTable->GetAssemblyModuleList);

        private ulong[] GetModuleOrAssembly(ulong address, int count, ref DacGetUlongArrayWithArg func, IntPtr vtableEntry)
        {
            InitDelegate(ref func, vtableEntry);

            int needed;
            if (count <= 0)
            {
                if (func(Self, address, 0, null, out needed) < 0)
                    return new ulong[0];

                count = needed;
            }

            // We ignore the return value here since the list may be partially filled
            ulong[] modules = new ulong[count];
            func(Self, address, modules.Length, modules, out needed);

            return modules;
        }

        internal ulong[] GetAppDomainList()
        {
            InitDelegate(ref _getAppDomainList, VTable->GetAppDomainList);

            int hr = _getAppDomainList(Self, 0, null, out int needed);
            if (hr != S_OK)
                return new ulong[0];

            ulong[] data = new ulong[needed];
            hr = _getAppDomainList(Self, data.Length, data, out needed);
            return hr == S_OK ? data : new ulong[0];
        }

        public IThreadData GetThreadData(ulong address)
        {
            if (address == 0)
                return null;

            InitDelegate(ref _getThreadData, VTable->GetThreadData);

            int hr = _getThreadData(Self, address, out V4ThreadData data);
            return hr == S_OK ? (IThreadData)data: null;
        }

        public IGCInfo GetGcHeapData()
        {
            InitDelegate(ref _getGCHeapData, VTable->GetGCHeapData);
            if (_getGCHeapData(Self, out LegacyGCInfo data) == S_OK)
                return data;

            return null;
        }

        public ISegmentData GetSegmentData(ulong addr)
        {
            InitDelegate(ref _getSegmentData, VTable->GetHeapSegmentData);
            if (_getSegmentData(Self, addr, out V4SegmentData data) == S_OK)
                return data;

            return null;
        }

        public ulong[] GetHeapList(int heapCount)
        {
            InitDelegate(ref _getGCHeapList, VTable->GetGCHeapList);

            ulong[] refs = new ulong[heapCount];
            int hr = _getGCHeapList(Self, heapCount, refs, out int needed);
            return hr == S_OK ? refs : null;
        }

        public IHeapDetails GetServerHeapDetails(ulong addr)
        {
            InitDelegate(ref _getGCHeapDetails, VTable->GetGCHeapDetails);

            int hr = _getGCHeapDetails(Self, addr, out V4HeapDetails data);
            return hr == S_OK ? (IHeapDetails)data : null;
        }


        public IHeapDetails GetWksHeapDetails()
        {
            InitDelegate(ref _getGCHeapStaticData, VTable->GetGCHeapStaticData);
            int hr = _getGCHeapStaticData(Self, out V4HeapDetails data);
            if (hr == S_OK)
                return data;

            return null;
        }
        
        public LegacyJitManagerInfo[] GetJitManagers()
        {
            InitDelegate(ref _getJitManagers, VTable->GetJitManagerList);
            int hr = _getJitManagers(Self, 0, null, out int needed);
            if (hr != S_OK || needed == 0)
                return new LegacyJitManagerInfo[0];

            LegacyJitManagerInfo[] result = new LegacyJitManagerInfo[needed];
            hr = _getJitManagers(Self, result.Length, result, out needed);

            return hr == S_OK ? result : new LegacyJitManagerInfo[0];
        }

        public LegacyJitCodeHeapInfo[] GetCodeHeapList(ulong jitManager)
        {
            InitDelegate(ref _getCodeHeaps, VTable->GetCodeHeapList);
            int hr = _getCodeHeaps(Self, jitManager, 0, null, out int needed);
            if (hr != S_OK || needed == 0)
                return new LegacyJitCodeHeapInfo[0];

            LegacyJitCodeHeapInfo[] result = new LegacyJitCodeHeapInfo[needed];
            hr = _getCodeHeaps(Self, jitManager, result.Length, result, out needed);

            return hr == S_OK ? result : new LegacyJitCodeHeapInfo[0];
        }

        public bool TraverseModuleMap(int mt, ulong module, ModuleMapTraverse traverse)
        {
            InitDelegate(ref _traverseModuleMap, VTable->TraverseModuleMap);

            int hr = _traverseModuleMap(Self, mt, module, Marshal.GetFunctionPointerForDelegate(traverse), IntPtr.Zero);
            GC.KeepAlive(traverse);
            return hr == S_OK;
        }

        public bool TraverseLoaderHeap(ulong heap, DesktopRuntimeBase.LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseLoaderHeap, VTable->TraverseLoaderHeap);

            int hr = _traverseLoaderHeap(Self, heap, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        public bool TraverseStubHeap(ulong heap, int type, DesktopRuntimeBase.LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseStubHeap, VTable->TraverseVirtCallStubHeap);

            int hr = _traverseStubHeap(Self, heap, type, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr == S_OK;
        }
        

        public SOSHandleEnum EnumerateHandles()
        {
            InitDelegate(ref _getHandleEnum, VTable->GetHandleEnum);

            int hr = _getHandleEnum(Self, out IntPtr ptrEnum);
            return hr == S_OK ? new SOSHandleEnum(ptrEnum) : null;
        }


        public SOSStackRefEnum EnumerateStackRefs(uint osThreadId)
        {
            InitDelegate(ref _getStackRefEnum, VTable->GetStackReferences);

            int hr = _getStackRefEnum(Self, osThreadId, out IntPtr ptrEnum);
            return hr == S_OK ? new SOSStackRefEnum(ptrEnum) : null;
        }

        #region Delegates

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetMethodDescDataDelegate(IntPtr self, ulong md, ulong ip, out V45MethodDescData data, int count, [Out] V45ReJitData[] rejitData, out int needed);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetIntPtr(IntPtr self, out IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetUlongWithArg(IntPtr self, ulong arg, out ulong data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetUlongWithArgs(IntPtr self, ulong arg, uint id, out ulong data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetUInt(IntPtr self, out uint data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetIntPtrWithArg(IntPtr self, ulong addr, out IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetThreadData(IntPtr self, ulong addr, [Out] out V4ThreadData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetHeapDetailsWithArg(IntPtr self, ulong addr, out V4HeapDetails data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetHeapDetails(IntPtr self, out V4HeapDetails data);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetUlongArray(IntPtr self, int count, [Out] ulong[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetUlongArrayWithArg(IntPtr self, ulong arg, int count, [Out] ulong[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCharArrayWithArg(IntPtr self, ulong arg, int count, [Out] byte[] values, [Out] out int needed);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetAssemblyData(IntPtr self, ulong in1, ulong in2, out LegacyAssemblyData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetADStoreData(IntPtr self, out LegacyAppDomainStoreData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetGCInfoData(IntPtr self, out LegacyGCInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCommonMethodTables(IntPtr self, out CommonMethodTables data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetThreadPoolData(IntPtr self, out V45ThreadPoolData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetThreadStoreData(IntPtr self, out LegacyThreadStoreData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetMTData(IntPtr self, ulong addr, out V45MethodTableData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetModuleData(IntPtr self, ulong addr, out V45ModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetSegmentData(IntPtr self, ulong addr, out V4SegmentData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetAppDomainData(IntPtr self, ulong addr, out LegacyAppDomainData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetJitManagerInfo(IntPtr self, ulong addr, out LegacyJitManagerInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetSyncBlockData(IntPtr self, int index, out LegacySyncBlkData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCodeHeaderData(IntPtr self, ulong addr, out CodeHeaderData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetFieldInfo(IntPtr self, ulong addr, out V4FieldInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetFieldData(IntPtr self, ulong addr, out LegacyFieldData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetObjectData(IntPtr self, ulong addr, out V45ObjectData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCCWData(IntPtr self, ulong addr, out V45CCWData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetRCWData(IntPtr self, ulong addr, out V45RCWData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetWorkRequestData(IntPtr self, ulong addr, out V45WorkRequestData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetLocalModuleData(IntPtr self, ulong addr, out V45DomainLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetThreadFromThinLock(IntPtr self, uint id, out ulong data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCodeHeaps(IntPtr self, ulong addr, int count, [Out] LegacyJitCodeHeapInfo[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetCOMPointers(IntPtr self, ulong addr, int count, [Out] COMInterfacePointerData[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetDomainLocalModuleDataFromAppDomain(IntPtr self, ulong appDomainAddr, int moduleID, out V45DomainLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetThreadLocalModuleData(IntPtr self, ulong addr, uint id, out V45ThreadLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacTraverseLoaderHeap(IntPtr self, ulong addr, IntPtr callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacTraverseStubHeap(IntPtr self, ulong addr, int type, IntPtr callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacTraverseModuleMap(IntPtr self, int type, ulong addr, IntPtr callback, IntPtr param);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int DacGetJitManagers(IntPtr self, int count, [Out] LegacyJitManagerInfo[] jitManagers, out int pNeeded);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetMetaDataImportDelegate(IntPtr self, ulong addr, out IntPtr iunk);
        #endregion
    }

#pragma warning disable CS0169
#pragma warning disable CS0649
    internal struct ISOSDacVTable
    {
        // ThreadStore
        public readonly IntPtr GetThreadStoreData;

        // AppDomains
        public readonly IntPtr GetAppDomainStoreData;
        public readonly IntPtr GetAppDomainList;
        public readonly IntPtr GetAppDomainData;
        public readonly IntPtr GetAppDomainName;
        public readonly IntPtr GetDomainFromContext;

        // Assemblies
        public readonly IntPtr GetAssemblyList;
        public readonly IntPtr GetAssemblyData;
        public readonly IntPtr GetAssemblyName;

        // Modules
        public readonly IntPtr GetMetaDataImport;
        public readonly IntPtr GetModuleData;
        public readonly IntPtr TraverseModuleMap;
        public readonly IntPtr GetAssemblyModuleList;
        public readonly IntPtr GetILForModule;

        // Threads

        public readonly IntPtr GetThreadData;
        public readonly IntPtr GetThreadFromThinlockID;
        public readonly IntPtr GetStackLimits;

        // MethodDescs

        public readonly IntPtr GetMethodDescData;
        public readonly IntPtr GetMethodDescPtrFromIP;
        public readonly IntPtr GetMethodDescName;
        public readonly IntPtr GetMethodDescPtrFromFrame;
        public readonly IntPtr GetMethodDescFromToken;
        private readonly IntPtr GetMethodDescTransparencyData;

        // JIT Data
        public readonly IntPtr GetCodeHeaderData;
        public readonly IntPtr GetJitManagerList;
        public readonly IntPtr GetJitHelperFunctionName;
        private readonly IntPtr GetJumpThunkTarget;

        // ThreadPool

        public readonly IntPtr GetThreadpoolData;
        public readonly IntPtr GetWorkRequestData;
        private readonly IntPtr GetHillClimbingLogEntry;

        // Objects
        public readonly IntPtr GetObjectData;
        public readonly IntPtr GetObjectStringData;
        public readonly IntPtr GetObjectClassName;

        // MethodTable
        public readonly IntPtr GetMethodTableName;
        public readonly IntPtr GetMethodTableData;
        public readonly IntPtr GetMethodTableSlot;
        public readonly IntPtr GetMethodTableFieldData;
        private readonly IntPtr GetMethodTableTransparencyData;

        // EEClass
        public readonly IntPtr GetMethodTableForEEClass;

        // FieldDesc
        public readonly IntPtr GetFieldDescData;

        // Frames
        public readonly IntPtr GetFrameName;


        // PEFiles
        public readonly IntPtr GetPEFileBase;
        public readonly IntPtr GetPEFileName;

        // GC
        public readonly IntPtr GetGCHeapData;
        public readonly IntPtr GetGCHeapList; // svr only
        public readonly IntPtr GetGCHeapDetails; // wks only
        public readonly IntPtr GetGCHeapStaticData;
        public readonly IntPtr GetHeapSegmentData;
        private readonly IntPtr GetOOMData;
        private readonly IntPtr GetOOMStaticData;
        private readonly IntPtr GetHeapAnalyzeData;
        private readonly IntPtr GetHeapAnalyzeStaticData;

        // DomainLocal
        private readonly IntPtr GetDomainLocalModuleData;
        public readonly IntPtr GetDomainLocalModuleDataFromAppDomain;
        public readonly IntPtr GetDomainLocalModuleDataFromModule;

        // ThreadLocal
        public readonly IntPtr GetThreadLocalModuleData;

        // SyncBlock
        public readonly IntPtr GetSyncBlockData;
        private readonly IntPtr GetSyncBlockCleanupData;

        // Handles
        public readonly IntPtr GetHandleEnum;
        private readonly IntPtr GetHandleEnumForTypes;
        private readonly IntPtr GetHandleEnumForGC;

        // EH
        private readonly IntPtr TraverseEHInfo;
        private readonly IntPtr GetNestedExceptionData;

        // StressLog
        public readonly IntPtr GetStressLogAddress;

        // Heaps
        public readonly IntPtr TraverseLoaderHeap;
        public readonly IntPtr GetCodeHeapList;
        public readonly IntPtr TraverseVirtCallStubHeap;

        // Other
        public readonly IntPtr GetUsefulGlobals;
        public readonly IntPtr GetClrWatsonBuckets;
        public readonly IntPtr GetTLSIndex;
        public readonly IntPtr GetDacModuleHandle;

        // COM
        public readonly IntPtr GetRCWData;
        public readonly IntPtr GetRCWInterfaces;
        public readonly IntPtr GetCCWData;
        public readonly IntPtr GetCCWInterfaces;
        private readonly IntPtr TraverseRCWCleanupList;

        // GC Reference Functions
        public readonly IntPtr GetStackReferences;
        public readonly IntPtr GetRegisterName;
        public readonly IntPtr GetThreadAllocData;
        public readonly IntPtr GetHeapAllocData;

        // For BindingDisplay plugin

        public readonly IntPtr GetFailedAssemblyList;
        public readonly IntPtr GetPrivateBinPaths;
        public readonly IntPtr GetAssemblyLocation;
        public readonly IntPtr GetAppDomainConfigFile;
        public readonly IntPtr GetApplicationBase;
        public readonly IntPtr GetFailedAssemblyData;
        public readonly IntPtr GetFailedAssemblyLocation;
        public readonly IntPtr GetFailedAssemblyDisplayName;
    }
}
