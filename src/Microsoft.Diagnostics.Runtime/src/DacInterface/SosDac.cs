// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    public sealed unsafe class SOSDac : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac = new Guid("436f00f2-b42a-4b9f-870c-e73db66ae930");
        private ISOSDacVTable* VTable => (ISOSDacVTable*)_vtable;
        private readonly DacLibrary _library;

        public SOSDac(DacLibrary? library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac, ptr)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public SOSDac(DacLibrary lib, CallableCOMWrapper toClone) : base(toClone)
        {
            _library = lib;
        }

        private DacGetIntPtr? _getHandleEnum;
        private GetHandleEnumForTypesDelegate? _getHandleEnumForTypes;
        private DacGetIntPtrWithArg? _getStackRefEnum;
        private DacGetThreadData? _getThreadData;
        private DacGetHeapDetailsWithArg? _getGCHeapDetails;
        private DacGetHeapDetails? _getGCHeapStaticData;
        private DacGetAddrArray? _getGCHeapList;
        private DacGetAddrArray? _getAppDomainList;
        private DacGetAddrArrayWithArg? _getAssemblyList;
        private DacGetAddrArrayWithArg? _getModuleList;
        private DacGetAssemblyData? _getAssemblyData;
        private DacGetADStoreData? _getAppDomainStoreData;
        private DacGetMTData? _getMethodTableData;
        private DacGetAddrWithArg? _getMTForEEClass;
        private DacGetGCInfoData? _getGCHeapData;
        private DacGetCommonMethodTables? _getCommonMethodTables;
        private DacGetCharArrayWithArg? _getMethodTableName;
        private DacGetByteArrayWithArg? _getJitHelperFunctionName;
        private DacGetCharArrayWithArg? _getPEFileName;
        private DacGetCharArrayWithArg? _getAppDomainName;
        private DacGetCharArrayWithArg? _getAssemblyName;
        private DacGetCharArrayWithArg? _getAppBase;
        private DacGetCharArrayWithArg? _getConfigFile;
        private DacGetModuleData? _getModuleData;
        private DacGetSegmentData? _getSegmentData;
        private DacGetAppDomainData? _getAppDomainData;
        private DacGetJitManagers? _getJitManagers;
        private DacTraverseLoaderHeap? _traverseLoaderHeap;
        private DacTraverseStubHeap? _traverseStubHeap;
        private DacTraverseModuleMap? _traverseModuleMap;
        private DacGetFieldInfo? _getFieldInfo;
        private DacGetFieldData? _getFieldData;
        private DacGetObjectData? _getObjectData;
        private DacGetCCWData? _getCCWData;
        private DacGetRCWData? _getRCWData;
        private DacGetCharArrayWithArg? _getFrameName;
        private DacGetAddrWithArg? _getMethodDescPtrFromFrame;
        private DacGetAddrWithArg? _getMethodDescPtrFromIP;
        private DacGetCodeHeaderData? _getCodeHeaderData;
        private DacGetSyncBlockData? _getSyncBlock;
        private DacGetThreadPoolData? _getThreadPoolData;
        private DacGetWorkRequestData? _getWorkRequestData;
        private DacGetDomainLocalModuleDataFromAppDomain? _getDomainLocalModuleDataFromAppDomain;
        private DacGetLocalModuleData? _getDomainLocalModuleDataFromModule;
        private DacGetCodeHeaps? _getCodeHeaps;
        private DacGetCOMPointers? _getCCWInterfaces;
        private DacGetCOMPointers? _getRCWInterfaces;
        private DacGetAddrWithArgs? _getILForModule;
        private DacGetThreadLocalModuleData? _getThreadLocalModuleData;
        private DacGetAddrWithArgs? _getMethodTableSlot;
        private DacGetCharArrayWithArg? _getMethodDescName;
        private DacGetThreadFromThinLock? _getThreadFromThinlockId;
        private DacGetUInt? _getTlsIndex;
        private DacGetThreadStoreData? _getThreadStoreData;
        private GetMethodDescDataDelegate? _getMethodDescData;
        private GetModuleDelegate? _getModule;
        private GetMethodDescFromTokenDelegate? _getMethodDescFromToken;

        public RejitData[] GetRejitData(ulong md, ulong ip = 0)
        {
            InitDelegate(ref _getMethodDescData, VTable->GetMethodDescData);
            int hr = _getMethodDescData(Self, md, ip, out MethodDescData data, 0, null, out int needed);

            if (SUCCEEDED(hr) && needed > 1)
            {
                RejitData[] result = new RejitData[needed];
                hr = _getMethodDescData(Self, md, ip, out data, result.Length, result, out needed);
                if (SUCCEEDED(hr))
                    return result;
            }

            return Array.Empty<RejitData>();
        }

        public bool GetMethodDescData(ulong md, ulong ip, out MethodDescData data)
        {
            InitDelegate(ref _getMethodDescData, VTable->GetMethodDescData);
            int hr = _getMethodDescData(Self, md, ip, out data, 0, null, out int needed);
            return SUCCEEDED(hr);
        }

        public bool GetThreadStoreData(out ThreadStoreData data)
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

        public ClrDataAddress GetThreadFromThinlockId(uint id)
        {
            InitDelegate(ref _getThreadFromThinlockId, VTable->GetThreadFromThinlockID);
            if (_getThreadFromThinlockId(Self, id, out ClrDataAddress thread) == S_OK)
                return thread;

            return default;
        }

        public string? GetMethodDescName(ulong md)
        {
            if (md == 0)
                return null;

            InitDelegate(ref _getMethodDescName, VTable->GetMethodDescName);

            if (_getMethodDescName(Self, md, 0, null, out int needed) < S_OK)
                return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(needed * sizeof(char));
            try
            {
                int actuallyNeeded;
                fixed (byte* bufferPtr = buffer)
                    if (_getMethodDescName(Self, md, needed, bufferPtr, out actuallyNeeded) < S_OK)
                        return null;

                // Patch for a bug on sos side :
                //  Sometimes, when the target method has parameters with generic types
                //  the first call to GetMethodDescName sets an incorrect value into pNeeded.
                //  In those cases, a second call directly after the first returns the correct value.
                if (needed != actuallyNeeded)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = ArrayPool<byte>.Shared.Rent(actuallyNeeded * sizeof(char));
                    fixed (byte* bufferPtr = buffer)
                        if (_getMethodDescName(Self, md, actuallyNeeded, bufferPtr, out actuallyNeeded) < S_OK)
                            return null;
                }

                return Encoding.Unicode.GetString(buffer, 0, (actuallyNeeded - 1) * sizeof(char));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public ulong GetMethodTableSlot(ulong mt, int slot)
        {
            if (mt == 0)
                return 0;

            InitDelegate(ref _getMethodTableSlot, VTable->GetMethodTableSlot);

            if (_getMethodTableSlot(Self, mt, (uint)slot, out ClrDataAddress ip) == S_OK)
                return ip;

            return 0;
        }

        public bool GetThreadLocalModuleData(ulong thread, uint index, out ThreadLocalModuleData data)
        {
            InitDelegate(ref _getThreadLocalModuleData, VTable->GetThreadLocalModuleData);

            return _getThreadLocalModuleData(Self, thread, index, out data) == S_OK;
        }

        public ulong GetILForModule(ulong moduleAddr, uint rva)
        {
            InitDelegate(ref _getILForModule, VTable->GetILForModule);

            int hr = _getILForModule(Self, moduleAddr, rva, out ClrDataAddress result);
            return hr == S_OK ? result : 0ul;
        }

        public COMInterfacePointerData[]? GetCCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getCCWInterfaces, VTable->GetCCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getCCWInterfaces(Self, ccw, count, data, out int pNeeded) >= 0)
                return data;

            return null;
        }

        public COMInterfacePointerData[]? GetRCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getRCWInterfaces, VTable->GetRCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getRCWInterfaces(Self, ccw, count, data, out int pNeeded) >= 0)
                return data;

            return null;
        }

        public bool GetDomainLocalModuleDataFromModule(ulong module, out DomainLocalModuleData data)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromModule, VTable->GetDomainLocalModuleDataFromModule);
            int res = _getDomainLocalModuleDataFromModule(Self, module, out data);
            return SUCCEEDED(res);
        }

        public bool GetDomainLocalModuleDataFromAppDomain(ulong appDomain, int id, out DomainLocalModuleData data)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromAppDomain, VTable->GetDomainLocalModuleDataFromAppDomain);
            int res = _getDomainLocalModuleDataFromAppDomain(Self, appDomain, id, out data);
            return SUCCEEDED(res);
        }

        public bool GetWorkRequestData(ulong request, out WorkRequestData data)
        {
            InitDelegate(ref _getWorkRequestData, VTable->GetWorkRequestData);
            int hr = _getWorkRequestData(Self, request, out data);
            return SUCCEEDED(hr);
        }

        public bool GetThreadPoolData(out ThreadPoolData data)
        {
            InitDelegate(ref _getThreadPoolData, VTable->GetThreadpoolData);
            int hr = _getThreadPoolData(Self, out data);
            return SUCCEEDED(hr);
        }

        public bool GetSyncBlockData(int index, out SyncBlockData data)
        {
            InitDelegate(ref _getSyncBlock, VTable->GetSyncBlockData);
            int hr = _getSyncBlock(Self, index, out data);
            return SUCCEEDED(hr);
        }

        public string? GetAppBase(ulong domain)
        {
            InitDelegate(ref _getAppBase, VTable->GetApplicationBase);
            return GetString(_getAppBase, domain);
        }

        public string? GetConfigFile(ulong domain)
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

        public ClrDataAddress GetMethodDescPtrFromFrame(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromFrame, VTable->GetMethodDescPtrFromFrame);
            if (_getMethodDescPtrFromFrame(Self, frame, out ClrDataAddress data) == S_OK)
                return data;

            return default;
        }

        public ClrDataAddress GetMethodDescPtrFromIP(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromIP, VTable->GetMethodDescPtrFromIP);
            if (_getMethodDescPtrFromIP(Self, frame, out ClrDataAddress data) == S_OK)
                return data;

            return default;
        }

        public string GetFrameName(ulong vtable)
        {
            InitDelegate(ref _getFrameName, VTable->GetFrameName);
            return GetString(_getFrameName, vtable, false) ?? "Unknown Frame";
        }

        public bool GetFieldInfo(ulong mt, out V4FieldInfo data)
        {
            InitDelegate(ref _getFieldInfo, VTable->GetMethodTableFieldData);
            int hr = _getFieldInfo(Self, mt, out data);
            return SUCCEEDED(hr);
        }

        public bool GetFieldData(ulong fieldDesc, out FieldData data)
        {
            InitDelegate(ref _getFieldData, VTable->GetFieldDescData);
            int hr = _getFieldData(Self, fieldDesc, out data);
            return SUCCEEDED(hr);
        }

        public bool GetObjectData(ulong obj, out V45ObjectData data)
        {
            InitDelegate(ref _getObjectData, VTable->GetObjectData);
            int hr = _getObjectData(Self, obj, out data);
            return SUCCEEDED(hr);
        }

        public bool GetCCWData(ulong ccw, out CCWData data)
        {
            InitDelegate(ref _getCCWData, VTable->GetCCWData);
            int hr = _getCCWData(Self, ccw, out data);
            return SUCCEEDED(hr);
        }

        public bool GetRCWData(ulong rcw, out RCWData data)
        {
            InitDelegate(ref _getRCWData, VTable->GetRCWData);
            int hr = _getRCWData(Self, rcw, out data);
            return SUCCEEDED(hr);
        }

        public ClrDataModule? GetClrDataModule(ulong module)
        {
            if (module == 0)
                return null;

            InitDelegate(ref _getModule, VTable->GetModule);
            if (_getModule(Self, module, out IntPtr iunk) != S_OK)
                return null;

            return new ClrDataModule(_library, iunk);
        }

        public MetadataImport? GetMetadataImport(ulong module)
        {
            if (module == 0)
                return null;

            InitDelegate(ref _getModule, VTable->GetModule);
            if (_getModule(Self, module, out IntPtr iunk) != S_OK)
                return null;

            try
            {
                return new MetadataImport(_library, iunk);
            }
            catch (InvalidCastException)
            {
                // QueryInterface on MetaDataImport seems to fail when we don't have full
                // metadata available.
                return null;
            }
        }

        public bool GetCommonMethodTables(out CommonMethodTables commonMTs)
        {
            InitDelegate(ref _getCommonMethodTables, VTable->GetUsefulGlobals);
            return _getCommonMethodTables(Self, out commonMTs) == S_OK;
        }

        public ClrDataAddress[] GetAssemblyList(ulong appDomain)
        {
            return GetAssemblyList(appDomain, 0);
        }

        public ClrDataAddress[] GetAssemblyList(ulong appDomain, int count)
        {
            return GetModuleOrAssembly(appDomain, count, ref _getAssemblyList, VTable->GetAssemblyList);
        }

        public bool GetAssemblyData(ulong domain, ulong assembly, out AssemblyData data)
        {
            InitDelegate(ref _getAssemblyData, VTable->GetAssemblyData);

            // The dac seems to have an issue where the assembly data can be filled in for a minidump.
            // If the data is partially filled in, we'll use it.

            int hr = _getAssemblyData(Self, domain, assembly, out data);
            return SUCCEEDED(hr) || data.Address == assembly;
        }

        public bool GetAppDomainData(ulong addr, out AppDomainData data)
        {
            InitDelegate(ref _getAppDomainData, VTable->GetAppDomainData);

            // We can face an exception while walking domain data if we catch the process
            // at a bad state.  As a workaround we will return partial data if data.Address
            // and data.StubHeap are set.

            int hr = _getAppDomainData(Self, addr, out data);
            return SUCCEEDED(hr) || data.Address == addr && data.StubHeap != 0;
        }

        public string? GetAppDomainName(ulong appDomain)
        {
            InitDelegate(ref _getAppDomainName, VTable->GetAppDomainName);
            return GetString(_getAppDomainName, appDomain);
        }

        public string? GetAssemblyName(ulong assembly)
        {
            InitDelegate(ref _getAssemblyName, VTable->GetAssemblyName);
            return GetString(_getAssemblyName, assembly);
        }

        public bool GetAppDomainStoreData(out AppDomainStoreData data)
        {
            InitDelegate(ref _getAppDomainStoreData, VTable->GetAppDomainStoreData);
            int hr = _getAppDomainStoreData(Self, out data);
            return SUCCEEDED(hr);
        }

        public bool GetMethodTableData(ulong addr, out MethodTableData data)
        {
            InitDelegate(ref _getMethodTableData, VTable->GetMethodTableData);
            int hr = _getMethodTableData(Self, addr, out data);
            return SUCCEEDED(hr);
        }

        public string? GetMethodTableName(ulong mt)
        {
            InitDelegate(ref _getMethodTableName, VTable->GetMethodTableName);
            return GetString(_getMethodTableName, mt);
        }

        public string? GetJitHelperFunctionName(ulong addr)
        {
            InitDelegate(ref _getJitHelperFunctionName, VTable->GetJitHelperFunctionName);
            return GetAsciiString(_getJitHelperFunctionName, addr);
        }

        public string? GetPEFileName(ulong pefile)
        {
            InitDelegate(ref _getPEFileName, VTable->GetPEFileName);
            return GetString(_getPEFileName, pefile);
        }

        private string? GetString(DacGetCharArrayWithArg func, ulong addr, bool skipNull = true)
        {
            int hr = func(Self, addr, 0, null, out int needed);
            if (hr != S_OK)
                return null;

            if (needed == 0)
                return string.Empty;

            byte[]? array = null;
            int size = needed * sizeof(char);
            Span<byte> buffer = size <= 32 ? stackalloc byte[size] : (array = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

            try
            {
                fixed (byte* bufferPtr = buffer)
                    hr = func(Self, addr, needed, bufferPtr, out needed);

                if (hr != S_OK)
                    return null;

                if (skipNull)
                    needed--;

#if NETCOREAPP
                return Encoding.Unicode.GetString(buffer.Slice(0, needed * sizeof(char)));
#else
                fixed (byte* bufferPtr = buffer)
                    return Encoding.Unicode.GetString(bufferPtr, needed * sizeof(char));
#endif
            }
            finally
            {
                if (array != null)
                    ArrayPool<byte>.Shared.Return(array);
            }
        }

        private string? GetAsciiString(DacGetByteArrayWithArg func, ulong addr)
        {
            int hr = func(Self, addr, 0, null, out int needed);
            if (hr != S_OK)
                return null;

            if (needed == 0)
                return string.Empty;

            byte[]? array = null;
            Span<byte> buffer = needed <= 32 ? stackalloc byte[needed] : (array = ArrayPool<byte>.Shared.Rent(needed)).AsSpan(0, needed);

            try
            {
                fixed (byte* bufferPtr = buffer)
                    hr = func(Self, addr, needed, bufferPtr, out needed);

                if (hr != S_OK)
                    return null;

                int len = buffer.IndexOf((byte)'\0');
                if (len >= 0)
                    needed = len;

#if NETCOREAPP
                return Encoding.ASCII.GetString(buffer.Slice(0, needed));
#else
                fixed (byte* bufferPtr = buffer)
                    return Encoding.ASCII.GetString(bufferPtr, needed);
#endif
            }
            finally
            {
                if (array != null)
                    ArrayPool<byte>.Shared.Return(array);
            }
        }

        public ClrDataAddress GetMethodTableByEEClass(ulong eeclass)
        {
            InitDelegate(ref _getMTForEEClass, VTable->GetMethodTableForEEClass);
            if (_getMTForEEClass(Self, eeclass, out ClrDataAddress data) == S_OK)
                return data;

            return default;
        }

        public bool GetModuleData(ulong module, out ModuleData data)
        {
            InitDelegate(ref _getModuleData, VTable->GetModuleData);
            int hr = _getModuleData(Self, module, out data);
            return SUCCEEDED(hr);
        }

        public ClrDataAddress[] GetModuleList(ulong assembly)
        {
            return GetModuleList(assembly, 0);
        }

        public ClrDataAddress[] GetModuleList(ulong assembly, int count)
        {
            return GetModuleOrAssembly(assembly, count, ref _getModuleList, VTable->GetAssemblyModuleList);
        }

        private ClrDataAddress[] GetModuleOrAssembly(ulong address, int count, ref DacGetAddrArrayWithArg? func, IntPtr vtableEntry)
        {
            InitDelegate(ref func, vtableEntry);

            int needed;
            if (count <= 0)
            {
                if (func(Self, address, 0, null, out needed) < 0)
                    return Array.Empty<ClrDataAddress>();

                count = needed;
            }

            // We ignore the return value here since the list may be partially filled
            ClrDataAddress[] modules = new ClrDataAddress[count];
            func(Self, address, modules.Length, modules, out needed);

            return modules;
        }

        public ClrDataAddress[] GetAppDomainList(int count = 0)
        {
            InitDelegate(ref _getAppDomainList, VTable->GetAppDomainList);

            if (count <= 0)
            {
                if (!GetAppDomainStoreData(out AppDomainStoreData addata))
                    return Array.Empty<ClrDataAddress>();

                count = addata.AppDomainCount;
            }

            ClrDataAddress[] data = new ClrDataAddress[count];
            int hr = _getAppDomainList(Self, data.Length, data, out int needed);
            return hr == S_OK ? data : Array.Empty<ClrDataAddress>();
        }

        public bool GetThreadData(ulong address, out ThreadData data)
        {
            if (address == 0)
            {
                data = new ThreadData();
                return false;
            }

            InitDelegate(ref _getThreadData, VTable->GetThreadData);
            int hr = _getThreadData(Self, address, out data);
            return SUCCEEDED(hr);
        }

        public bool GetGCHeapData(out GCInfo data)
        {
            InitDelegate(ref _getGCHeapData, VTable->GetGCHeapData);
            int hr = _getGCHeapData(Self, out data);
            return SUCCEEDED(hr);
        }

        public bool GetSegmentData(ulong addr, out SegmentData data)
        {
            InitDelegate(ref _getSegmentData, VTable->GetHeapSegmentData);
            int hr = _getSegmentData(Self, addr, out data);
            return SUCCEEDED(hr);
        }

        public ClrDataAddress[] GetHeapList(int heapCount)
        {
            InitDelegate(ref _getGCHeapList, VTable->GetGCHeapList);

            ClrDataAddress[] refs = new ClrDataAddress[heapCount];
            int hr = _getGCHeapList(Self, heapCount, refs, out int needed);
            return hr == S_OK ? refs : Array.Empty<ClrDataAddress>();
        }

        public bool GetServerHeapDetails(ulong addr, out HeapDetails data)
        {
            InitDelegate(ref _getGCHeapDetails, VTable->GetGCHeapDetails);
            int hr = _getGCHeapDetails(Self, addr, out data);

            return SUCCEEDED(hr);
        }

        public bool GetWksHeapDetails(out HeapDetails data)
        {
            InitDelegate(ref _getGCHeapStaticData, VTable->GetGCHeapStaticData);
            int hr = _getGCHeapStaticData(Self, out data);
            return SUCCEEDED(hr);
        }

        public JitManagerInfo[] GetJitManagers()
        {
            InitDelegate(ref _getJitManagers, VTable->GetJitManagerList);
            int hr = _getJitManagers(Self, 0, null, out int needed);
            if (hr != S_OK || needed == 0)
                return Array.Empty<JitManagerInfo>();

            JitManagerInfo[] result = new JitManagerInfo[needed];
            hr = _getJitManagers(Self, result.Length, result, out needed);

            return hr == S_OK ? result : Array.Empty<JitManagerInfo>();
        }

        public JitCodeHeapInfo[] GetCodeHeapList(ulong jitManager)
        {
            InitDelegate(ref _getCodeHeaps, VTable->GetCodeHeapList);
            int hr = _getCodeHeaps(Self, jitManager, 0, null, out int needed);
            if (hr != S_OK || needed == 0)
                return Array.Empty<JitCodeHeapInfo>();

            JitCodeHeapInfo[] result = new JitCodeHeapInfo[needed];
            hr = _getCodeHeaps(Self, jitManager, result.Length, result, out needed);

            return hr == S_OK ? result : Array.Empty<JitCodeHeapInfo>();
        }

        public enum ModuleMapTraverseKind
        {
            TypeDefToMethodTable,
            TypeRefToMethodTable
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ModuleMapTraverse(uint index, ulong methodTable, IntPtr token);

        public bool TraverseModuleMap(ModuleMapTraverseKind mt, ulong module, ModuleMapTraverse traverse)
        {
            InitDelegate(ref _traverseModuleMap, VTable->TraverseModuleMap);

            int hr = _traverseModuleMap(Self, (int)mt, module, Marshal.GetFunctionPointerForDelegate(traverse), IntPtr.Zero);
            GC.KeepAlive(traverse);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void LoaderHeapTraverse(ulong address, IntPtr size, int isCurrent);

        public bool TraverseLoaderHeap(ulong heap, LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseLoaderHeap, VTable->TraverseLoaderHeap);

            int hr = _traverseLoaderHeap(Self, heap, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        public bool TraverseStubHeap(ulong heap, int type, LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseStubHeap, VTable->TraverseVirtCallStubHeap);

            int hr = _traverseStubHeap(Self, heap, type, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        public SOSHandleEnum? EnumerateHandles(params ClrHandleKind[] types)
        {
            InitDelegate(ref _getHandleEnumForTypes, VTable->GetHandleEnumForTypes);

            int hr = _getHandleEnumForTypes(Self, types, types.Length, out IntPtr ptrEnum);
            return hr == S_OK ? new SOSHandleEnum(_library, ptrEnum) : null;
        }

        public SOSHandleEnum? EnumerateHandles()
        {
            InitDelegate(ref _getHandleEnum, VTable->GetHandleEnum);

            int hr = _getHandleEnum(Self, out IntPtr ptrEnum);
            return hr == S_OK ? new SOSHandleEnum(_library, ptrEnum) : null;
        }

        public SOSStackRefEnum? EnumerateStackRefs(uint osThreadId)
        {
            InitDelegate(ref _getStackRefEnum, VTable->GetStackReferences);

            int hr = _getStackRefEnum(Self, osThreadId, out IntPtr ptrEnum);
            return hr == S_OK ? new SOSStackRefEnum(_library, ptrEnum) : null;
        }

        public ulong GetMethodDescFromToken(ulong module, uint token)
        {
            InitDelegate(ref _getMethodDescFromToken, VTable->GetMethodDescFromToken);

            int hr = _getMethodDescFromToken(Self, module, token, out ClrDataAddress md);
            return hr == S_OK ? md : 0ul;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetHandleEnumForTypesDelegate(IntPtr self, [In] ClrHandleKind[] types, int count, out IntPtr handleEnum);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMethodDescFromTokenDelegate(IntPtr self, ulong module, uint token, out ClrDataAddress methodDesc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMethodDescDataDelegate(IntPtr self, ulong md, ulong ip, out MethodDescData data, int count, [Out] RejitData[]? rejitData, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetIntPtr(IntPtr self, out IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAddrWithArg(IntPtr self, ulong arg, out ClrDataAddress data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAddrWithArgs(IntPtr self, ulong arg, uint id, out ClrDataAddress data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetUInt(IntPtr self, out uint data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetIntPtrWithArg(IntPtr self, uint addr, out IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetThreadData(IntPtr self, ulong addr, [Out] out ThreadData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetHeapDetailsWithArg(IntPtr self, ulong addr, out HeapDetails data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetHeapDetails(IntPtr self, out HeapDetails data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAddrArray(IntPtr self, int count, [Out] ClrDataAddress[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAddrArrayWithArg(IntPtr self, ulong arg, int count, [Out] ClrDataAddress[]? values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCharArrayWithArg(IntPtr self, ulong arg, int count, byte* values, [Out] out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetByteArrayWithArg(IntPtr self, ulong arg, int count, byte* values, [Out] out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAssemblyData(IntPtr self, ulong in1, ulong in2, out AssemblyData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetADStoreData(IntPtr self, out AppDomainStoreData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetGCInfoData(IntPtr self, out GCInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCommonMethodTables(IntPtr self, out CommonMethodTables data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetThreadPoolData(IntPtr self, out ThreadPoolData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetThreadStoreData(IntPtr self, out ThreadStoreData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetMTData(IntPtr self, ulong addr, out MethodTableData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetModuleData(IntPtr self, ulong addr, out ModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetSegmentData(IntPtr self, ulong addr, out SegmentData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetAppDomainData(IntPtr self, ulong addr, out AppDomainData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetJitManagerInfo(IntPtr self, ulong addr, out JitManagerInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetSyncBlockData(IntPtr self, int index, out SyncBlockData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCodeHeaderData(IntPtr self, ulong addr, out CodeHeaderData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetFieldInfo(IntPtr self, ulong addr, out V4FieldInfo data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetFieldData(IntPtr self, ulong addr, out FieldData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetObjectData(IntPtr self, ulong addr, out V45ObjectData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCCWData(IntPtr self, ulong addr, out CCWData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetRCWData(IntPtr self, ulong addr, out RCWData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetWorkRequestData(IntPtr self, ulong addr, out WorkRequestData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetLocalModuleData(IntPtr self, ulong addr, out DomainLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetThreadFromThinLock(IntPtr self, uint id, out ClrDataAddress data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCodeHeaps(IntPtr self, ulong addr, int count, [Out] JitCodeHeapInfo[]? values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetCOMPointers(IntPtr self, ulong addr, int count, [Out] COMInterfacePointerData[] values, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetDomainLocalModuleDataFromAppDomain(IntPtr self, ulong appDomainAddr, int moduleID, out DomainLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetThreadLocalModuleData(IntPtr self, ulong addr, uint id, out ThreadLocalModuleData data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacTraverseLoaderHeap(IntPtr self, ulong addr, IntPtr callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacTraverseStubHeap(IntPtr self, ulong addr, int type, IntPtr callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacTraverseModuleMap(IntPtr self, int type, ulong addr, IntPtr callback, IntPtr param);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetJitManagers(IntPtr self, int count, [Out] JitManagerInfo[]? jitManagers, out int pNeeded);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleDelegate(IntPtr self, ulong addr, out IntPtr iunk);
    }

#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CA1823

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
        public readonly IntPtr GetModule;
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
        public readonly IntPtr GetHandleEnumForTypes;
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