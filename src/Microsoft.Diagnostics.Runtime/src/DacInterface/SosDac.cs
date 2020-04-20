// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
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

        private readonly DacLibrary _library;

        public SOSDac(DacLibrary? library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac, ptr)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        private ref readonly ISOSDacVTable VTable => ref Unsafe.AsRef<ISOSDacVTable>(_vtable);

        public SOSDac(DacLibrary lib, CallableCOMWrapper toClone) : base(toClone)
        {
            _library = lib;
        }

        public RejitData[] GetRejitData(ulong md, ulong ip = 0)
        {
            InitDelegate(ref _getMethodDescData, VTable.GetMethodDescData);
            HResult hr = _getMethodDescData(Self, md, ip, out MethodDescData data, 0, null, out int needed);
            if (hr && needed >= 1)
            {
                RejitData[] result = new RejitData[needed];
                hr = _getMethodDescData(Self, md, ip, out data, result.Length, result, out needed);
                if (hr)
                    return result;
            }

            return Array.Empty<RejitData>();
        }

        public HResult GetMethodDescData(ulong md, ulong ip, out MethodDescData data)
        {
            InitDelegate(ref _getMethodDescData, VTable.GetMethodDescData);
            return _getMethodDescData(Self, md, ip, out data, 0, null, out int needed);
        }

        public HResult GetThreadStoreData(out ThreadStoreData data)
        {
            InitDelegate(ref _getThreadStoreData, VTable.GetThreadStoreData);
            return _getThreadStoreData(Self, out data);
        }

        public uint GetTlsIndex()
        {
            InitDelegate(ref _getTlsIndex, VTable.GetTLSIndex);
            if (_getTlsIndex(Self, out uint index))
                return index;

            return uint.MaxValue;
        }

        public ClrDataAddress GetThreadFromThinlockId(uint id)
        {
            InitDelegate(ref _getThreadFromThinlockId, VTable.GetThreadFromThinlockID);
            if (_getThreadFromThinlockId(Self, id, out ClrDataAddress thread))
                return thread;

            return default;
        }

        public string? GetMethodDescName(ulong md)
        {
            if (md == 0)
                return null;

            InitDelegate(ref _getMethodDescName, VTable.GetMethodDescName);

            if (!_getMethodDescName(Self, md, 0, null, out int needed))
                return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(needed * sizeof(char));
            try
            {
                int actuallyNeeded;
                fixed (byte* bufferPtr = buffer)
                    if (!_getMethodDescName(Self, md, needed, bufferPtr, out actuallyNeeded))
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
                        if (!_getMethodDescName(Self, md, actuallyNeeded, bufferPtr, out actuallyNeeded))
                            return null;
                }

                return Encoding.Unicode.GetString(buffer, 0, (actuallyNeeded - 1) * sizeof(char));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public ulong GetMethodTableSlot(ulong mt, uint slot)
        {
            if (mt == 0)
                return 0;

            InitDelegate(ref _getMethodTableSlot, VTable.GetMethodTableSlot);

            if (_getMethodTableSlot(Self, mt, slot, out ClrDataAddress ip))
                return ip;

            return 0;
        }

        public HResult GetThreadLocalModuleData(ulong thread, uint index, out ThreadLocalModuleData data)
        {
            InitDelegate(ref _getThreadLocalModuleData, VTable.GetThreadLocalModuleData);

            return _getThreadLocalModuleData(Self, thread, index, out data);
        }

        public ulong GetILForModule(ulong moduleAddr, uint rva)
        {
            InitDelegate(ref _getILForModule, VTable.GetILForModule);

            if (_getILForModule(Self, moduleAddr, rva, out ClrDataAddress result))
                return result;

            return 0;
        }

        public COMInterfacePointerData[]? GetCCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getCCWInterfaces, VTable.GetCCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getCCWInterfaces(Self, ccw, count, data, out int pNeeded))
                return data;

            return null;
        }

        public COMInterfacePointerData[]? GetRCWInterfaces(ulong ccw, int count)
        {
            InitDelegate(ref _getRCWInterfaces, VTable.GetRCWInterfaces);

            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            if (_getRCWInterfaces(Self, ccw, count, data, out int pNeeded))
                return data;

            return null;
        }

        public HResult GetDomainLocalModuleDataFromModule(ulong module, out DomainLocalModuleData data)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromModule, VTable.GetDomainLocalModuleDataFromModule);
            return _getDomainLocalModuleDataFromModule(Self, module, out data);
        }

        public HResult GetDomainLocalModuleDataFromAppDomain(ulong appDomain, int id, out DomainLocalModuleData data)
        {
            InitDelegate(ref _getDomainLocalModuleDataFromAppDomain, VTable.GetDomainLocalModuleDataFromAppDomain);
            return _getDomainLocalModuleDataFromAppDomain(Self, appDomain, id, out data);
        }

        public HResult GetWorkRequestData(ulong request, out WorkRequestData data)
        {
            InitDelegate(ref _getWorkRequestData, VTable.GetWorkRequestData);
            return _getWorkRequestData(Self, request, out data);
        }

        public HResult GetThreadPoolData(out ThreadPoolData data)
        {
            InitDelegate(ref _getThreadPoolData, VTable.GetThreadpoolData);
            return _getThreadPoolData(Self, out data);
        }

        public HResult GetSyncBlockData(int index, out SyncBlockData data)
        {
            InitDelegate(ref _getSyncBlock, VTable.GetSyncBlockData);
            return _getSyncBlock(Self, index, out data);
        }

        public string? GetAppBase(ulong domain)
        {
            InitDelegate(ref _getAppBase, VTable.GetApplicationBase);
            return GetString(_getAppBase, domain);
        }

        public string? GetConfigFile(ulong domain)
        {
            InitDelegate(ref _getConfigFile, VTable.GetAppDomainConfigFile);
            return GetString(_getConfigFile, domain);
        }

        public HResult GetCodeHeaderData(ulong ip, out CodeHeaderData codeHeaderData)
        {
            if (ip == 0)
            {
                codeHeaderData = default;
                return HResult.E_INVALIDARG;
            }

            InitDelegate(ref _getCodeHeaderData, VTable.GetCodeHeaderData);
            return _getCodeHeaderData(Self, ip, out codeHeaderData);
        }

        public ClrDataAddress GetMethodDescPtrFromFrame(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromFrame, VTable.GetMethodDescPtrFromFrame);
            if (_getMethodDescPtrFromFrame(Self, frame, out ClrDataAddress data))
                return data;

            return default;
        }

        public ClrDataAddress GetMethodDescPtrFromIP(ulong frame)
        {
            InitDelegate(ref _getMethodDescPtrFromIP, VTable.GetMethodDescPtrFromIP);
            if (_getMethodDescPtrFromIP(Self, frame, out ClrDataAddress data))
                return data;

            return default;
        }

        public string GetFrameName(ulong vtable)
        {
            InitDelegate(ref _getFrameName, VTable.GetFrameName);
            return GetString(_getFrameName, vtable, false) ?? "Unknown Frame";
        }

        public HResult GetFieldInfo(ulong mt, out V4FieldInfo data)
        {
            InitDelegate(ref _getFieldInfo, VTable.GetMethodTableFieldData);
            return _getFieldInfo(Self, mt, out data);
        }

        public HResult GetFieldData(ulong fieldDesc, out FieldData data)
        {
            InitDelegate(ref _getFieldData, VTable.GetFieldDescData);
            return _getFieldData(Self, fieldDesc, out data);
        }

        public HResult GetObjectData(ulong obj, out V45ObjectData data)
        {
            InitDelegate(ref _getObjectData, VTable.GetObjectData);
            return _getObjectData(Self, obj, out data);
        }

        public HResult GetCCWData(ulong ccw, out CcwData data)
        {
            InitDelegate(ref _getCCWData, VTable.GetCCWData);
            return _getCCWData(Self, ccw, out data);
        }

        public HResult GetRCWData(ulong rcw, out RcwData data)
        {
            InitDelegate(ref _getRCWData, VTable.GetRCWData);
            return _getRCWData(Self, rcw, out data);
        }

        public ClrDataModule? GetClrDataModule(ulong module)
        {
            if (module == 0)
                return null;

            InitDelegate(ref _getModule, VTable.GetModule);
            if (_getModule(Self, module, out IntPtr iunk))
                return new ClrDataModule(_library, iunk);

            return null;
        }

        public MetadataImport? GetMetadataImport(ulong module)
        {
            if (module == 0)
                return null;

            InitDelegate(ref _getModule, VTable.GetModule);
            if (!_getModule(Self, module, out IntPtr iunk))
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

        public HResult GetCommonMethodTables(out CommonMethodTables commonMTs)
        {
            InitDelegate(ref _getCommonMethodTables, VTable.GetUsefulGlobals);
            return _getCommonMethodTables(Self, out commonMTs);
        }

        public ClrDataAddress[] GetAssemblyList(ulong appDomain) => GetAssemblyList(appDomain, 0);
        public ClrDataAddress[] GetAssemblyList(ulong appDomain, int count) =>  GetModuleOrAssembly(appDomain, count, ref _getAssemblyList, VTable.GetAssemblyList);
        public ClrDataAddress[] GetModuleList(ulong assembly) => GetModuleList(assembly, 0);
        public ClrDataAddress[] GetModuleList(ulong assembly, int count) => GetModuleOrAssembly(assembly, count, ref _getModuleList, VTable.GetAssemblyModuleList);

        public HResult GetAssemblyData(ulong domain, ulong assembly, out AssemblyData data)
        {
            InitDelegate(ref _getAssemblyData, VTable.GetAssemblyData);

            // The dac seems to have an issue where the assembly data can be filled in for a minidump.
            // If the data is partially filled in, we'll use it.

            HResult hr = _getAssemblyData(Self, domain, assembly, out data);
            if (!hr && data.Address == assembly)
                return HResult.S_FALSE;

            return hr;
        }

        public HResult GetAppDomainData(ulong addr, out AppDomainData data)
        {
            InitDelegate(ref _getAppDomainData, VTable.GetAppDomainData);

            // We can face an exception while walking domain data if we catch the process
            // at a bad state.  As a workaround we will return partial data if data.Address
            // and data.StubHeap are set.

            HResult hr = _getAppDomainData(Self, addr, out data);
            if (!hr && data.Address == addr && data.StubHeap != 0)
                return HResult.S_FALSE;

            return hr;
        }

        public string? GetAppDomainName(ulong appDomain)
        {
            InitDelegate(ref _getAppDomainName, VTable.GetAppDomainName);
            return GetString(_getAppDomainName, appDomain);
        }

        public string? GetAssemblyName(ulong assembly)
        {
            InitDelegate(ref _getAssemblyName, VTable.GetAssemblyName);
            return GetString(_getAssemblyName, assembly);
        }

        public HResult GetAppDomainStoreData(out AppDomainStoreData data)
        {
            InitDelegate(ref _getAppDomainStoreData, VTable.GetAppDomainStoreData);
            return _getAppDomainStoreData(Self, out data);
        }

        public HResult GetMethodTableData(ulong addr, out MethodTableData data)
        {
            // If the 2nd bit is set it means addr is actually a TypeHandle (which GetMethodTable does not support).
            if ((addr & 2) == 2)
            {
                data = default;
                return HResult.E_INVALIDARG;
            }

            InitDelegate(ref _getMethodTableData, VTable.GetMethodTableData);
            return _getMethodTableData(Self, addr, out data);
        }

        public string? GetMethodTableName(ulong mt)
        {
            InitDelegate(ref _getMethodTableName, VTable.GetMethodTableName);
            return GetString(_getMethodTableName, mt);
        }

        public string? GetJitHelperFunctionName(ulong addr)
        {
            InitDelegate(ref _getJitHelperFunctionName, VTable.GetJitHelperFunctionName);
            return GetAsciiString(_getJitHelperFunctionName, addr);
        }

        public string? GetPEFileName(ulong pefile)
        {
            InitDelegate(ref _getPEFileName, VTable.GetPEFileName);
            return GetString(_getPEFileName, pefile);
        }

        private string? GetString(DacGetCharArrayWithArg func, ulong addr, bool skipNull = true)
        {
            HResult hr = func(Self, addr, 0, null, out int needed);
            if (!hr)
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

                if (!hr)
                    return null;

                if (skipNull)
                    needed--;

                return Encoding.Unicode.GetString(buffer.Slice(0, needed * sizeof(char)));
            }
            finally
            {
                if (array != null)
                    ArrayPool<byte>.Shared.Return(array);
            }
        }

        private string? GetAsciiString(DacGetByteArrayWithArg func, ulong addr)
        {
            HResult hr = func(Self, addr, 0, null, out int needed);
            if (!hr)
                return null;

            if (needed == 0)
                return string.Empty;

            byte[]? array = null;
            Span<byte> buffer = needed <= 32 ? stackalloc byte[needed] : (array = ArrayPool<byte>.Shared.Rent(needed)).AsSpan(0, needed);

            try
            {
                fixed (byte* bufferPtr = buffer)
                    hr = func(Self, addr, needed, bufferPtr, out needed);

                if (!hr)
                    return null;

                int len = buffer.IndexOf((byte)'\0');
                if (len >= 0)
                    needed = len;

                return Encoding.ASCII.GetString(buffer.Slice(0, needed));
            }
            finally
            {
                if (array != null)
                    ArrayPool<byte>.Shared.Return(array);
            }
        }

        public ClrDataAddress GetMethodTableByEEClass(ulong eeclass)
        {
            InitDelegate(ref _getMTForEEClass, VTable.GetMethodTableForEEClass);
            if (_getMTForEEClass(Self, eeclass, out ClrDataAddress data))
                return data;

            return default;
        }

        public HResult GetModuleData(ulong module, out ModuleData data)
        {
            InitDelegate(ref _getModuleData, VTable.GetModuleData);
            return _getModuleData(Self, module, out data);
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
            InitDelegate(ref _getAppDomainList, VTable.GetAppDomainList);

            if (count <= 0)
            {
                if (!GetAppDomainStoreData(out AppDomainStoreData addata))
                    return Array.Empty<ClrDataAddress>();

                count = addata.AppDomainCount;
            }

            ClrDataAddress[] data = new ClrDataAddress[count];
            HResult hr = _getAppDomainList(Self, data.Length, data, out int needed);
            return hr ? data : Array.Empty<ClrDataAddress>();
        }

        public HResult GetThreadData(ulong address, out ThreadData data)
        {
            if (address == 0)
            {
                data = default;
                return HResult.E_INVALIDARG;
            }

            InitDelegate(ref _getThreadData, VTable.GetThreadData);
            return _getThreadData(Self, address, out data);
        }

        public HResult GetGCHeapData(out GCInfo data)
        {
            InitDelegate(ref _getGCHeapData, VTable.GetGCHeapData);
            return _getGCHeapData(Self, out data);
        }

        public HResult GetSegmentData(ulong addr, out SegmentData data)
        {
            InitDelegate(ref _getSegmentData, VTable.GetHeapSegmentData);
            return _getSegmentData(Self, addr, out data);
        }

        public ClrDataAddress[] GetHeapList(int heapCount)
        {
            InitDelegate(ref _getGCHeapList, VTable.GetGCHeapList);

            ClrDataAddress[] refs = new ClrDataAddress[heapCount];
            HResult hr = _getGCHeapList(Self, heapCount, refs, out int needed);
            return hr ? refs : Array.Empty<ClrDataAddress>();
        }

        public HResult GetServerHeapDetails(ulong addr, out HeapDetails data)
        {
            InitDelegate(ref _getGCHeapDetails, VTable.GetGCHeapDetails);
            return _getGCHeapDetails(Self, addr, out data);
        }

        public HResult GetWksHeapDetails(out HeapDetails data)
        {
            InitDelegate(ref _getGCHeapStaticData, VTable.GetGCHeapStaticData);
            return _getGCHeapStaticData(Self, out data);
        }

        public JitManagerInfo[] GetJitManagers()
        {
            InitDelegate(ref _getJitManagers, VTable.GetJitManagerList);
            HResult hr = _getJitManagers(Self, 0, null, out int needed);
            if (!hr || needed == 0)
                return Array.Empty<JitManagerInfo>();

            JitManagerInfo[] result = new JitManagerInfo[needed];
            hr = _getJitManagers(Self, result.Length, result, out needed);

            return hr ? result : Array.Empty<JitManagerInfo>();
        }

        public JitCodeHeapInfo[] GetCodeHeapList(ulong jitManager)
        {
            InitDelegate(ref _getCodeHeaps, VTable.GetCodeHeapList);
            HResult hr = _getCodeHeaps(Self, jitManager, 0, null, out int needed);
            if (!hr || needed == 0)
                return Array.Empty<JitCodeHeapInfo>();

            JitCodeHeapInfo[] result = new JitCodeHeapInfo[needed];
            hr = _getCodeHeaps(Self, jitManager, result.Length, result, out needed);

            return hr ? result : Array.Empty<JitCodeHeapInfo>();
        }

        public enum ModuleMapTraverseKind
        {
            TypeDefToMethodTable,
            TypeRefToMethodTable
        }

        public delegate void ModuleMapTraverse(int index, ulong methodTable, IntPtr token);

        public HResult TraverseModuleMap(ModuleMapTraverseKind mt, ulong module, ModuleMapTraverse traverse)
        {
            InitDelegate(ref _traverseModuleMap, VTable.TraverseModuleMap);

            HResult hr = _traverseModuleMap(Self, mt, module, Marshal.GetFunctionPointerForDelegate(traverse), IntPtr.Zero);
            GC.KeepAlive(traverse);
            return hr;
        }

        public delegate void LoaderHeapTraverse(ulong address, IntPtr size, int isCurrent);

        public HResult TraverseLoaderHeap(ulong heap, LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseLoaderHeap, VTable.TraverseLoaderHeap);

            HResult hr = _traverseLoaderHeap(Self, heap, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr;
        }

        public HResult TraverseStubHeap(ulong heap, int type, LoaderHeapTraverse callback)
        {
            InitDelegate(ref _traverseStubHeap, VTable.TraverseVirtCallStubHeap);

            HResult hr = _traverseStubHeap(Self, heap, type, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr;
        }

        public SOSHandleEnum? EnumerateHandles(params ClrHandleKind[] types)
        {
            InitDelegate(ref _getHandleEnumForTypes, VTable.GetHandleEnumForTypes);

            HResult hr = _getHandleEnumForTypes(Self, types, types.Length, out IntPtr ptrEnum);
            return hr ? new SOSHandleEnum(_library, ptrEnum) : null;
        }

        public SOSHandleEnum? EnumerateHandles()
        {
            InitDelegate(ref _getHandleEnum, VTable.GetHandleEnum);

            HResult hr = _getHandleEnum(Self, out IntPtr ptrEnum);
            return hr ? new SOSHandleEnum(_library, ptrEnum) : null;
        }

        public SOSStackRefEnum? EnumerateStackRefs(uint osThreadId)
        {
            InitDelegate(ref _getStackRefEnum, VTable.GetStackReferences);

            HResult hr = _getStackRefEnum(Self, osThreadId, out IntPtr ptrEnum);
            return hr ? new SOSStackRefEnum(_library, ptrEnum) : null;
        }

        public ulong GetMethodDescFromToken(ulong module, int token)
        {
            InitDelegate(ref _getMethodDescFromToken, VTable.GetMethodDescFromToken);

            if (_getMethodDescFromToken(Self, module, token, out ClrDataAddress md))
                return md;

            return 0;
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

        private delegate HResult GetHandleEnumForTypesDelegate(IntPtr self, [In] ClrHandleKind[] types, int count, out IntPtr handleEnum);
        private delegate HResult GetMethodDescFromTokenDelegate(IntPtr self, ClrDataAddress module, int token, out ClrDataAddress methodDesc);
        private delegate HResult GetMethodDescDataDelegate(IntPtr self, ClrDataAddress md, ulong ip, out MethodDescData data, int count, [Out] RejitData[]? rejitData, out int needed);
        private delegate HResult DacGetIntPtr(IntPtr self, out IntPtr data);
        private delegate HResult DacGetAddrWithArg(IntPtr self, ClrDataAddress arg, out ClrDataAddress data);
        private delegate HResult DacGetAddrWithArgs(IntPtr self, ClrDataAddress arg, uint id, out ClrDataAddress data);
        private delegate HResult DacGetUInt(IntPtr self, out uint data);
        private delegate HResult DacGetIntPtrWithArg(IntPtr self, uint addr, out IntPtr data);
        private delegate HResult DacGetThreadData(IntPtr self, ClrDataAddress addr, [Out] out ThreadData data);
        private delegate HResult DacGetHeapDetailsWithArg(IntPtr self, ClrDataAddress addr, out HeapDetails data);
        private delegate HResult DacGetHeapDetails(IntPtr self, out HeapDetails data);
        private delegate HResult DacGetAddrArray(IntPtr self, int count, [Out] ClrDataAddress[] values, out int needed);
        private delegate HResult DacGetAddrArrayWithArg(IntPtr self, ClrDataAddress arg, int count, [Out] ClrDataAddress[]? values, out int needed);
        private delegate HResult DacGetCharArrayWithArg(IntPtr self, ClrDataAddress arg, int count, byte* values, [Out] out int needed);
        private delegate HResult DacGetByteArrayWithArg(IntPtr self, ClrDataAddress arg, int count, byte* values, [Out] out int needed);
        private delegate HResult DacGetAssemblyData(IntPtr self, ClrDataAddress in1, ClrDataAddress in2, out AssemblyData data);
        private delegate HResult DacGetADStoreData(IntPtr self, out AppDomainStoreData data);
        private delegate HResult DacGetGCInfoData(IntPtr self, out GCInfo data);
        private delegate HResult DacGetCommonMethodTables(IntPtr self, out CommonMethodTables data);
        private delegate HResult DacGetThreadPoolData(IntPtr self, out ThreadPoolData data);
        private delegate HResult DacGetThreadStoreData(IntPtr self, out ThreadStoreData data);
        private delegate HResult DacGetMTData(IntPtr self, ClrDataAddress addr, out MethodTableData data);
        private delegate HResult DacGetModuleData(IntPtr self, ClrDataAddress addr, out ModuleData data);
        private delegate HResult DacGetSegmentData(IntPtr self, ClrDataAddress addr, out SegmentData data);
        private delegate HResult DacGetAppDomainData(IntPtr self, ClrDataAddress addr, out AppDomainData data);
        private delegate HResult DacGetJitManagerInfo(IntPtr self, ClrDataAddress addr, out JitManagerInfo data);
        private delegate HResult DacGetSyncBlockData(IntPtr self, int index, out SyncBlockData data);
        private delegate HResult DacGetCodeHeaderData(IntPtr self, ClrDataAddress addr, out CodeHeaderData data);
        private delegate HResult DacGetFieldInfo(IntPtr self, ClrDataAddress addr, out V4FieldInfo data);
        private delegate HResult DacGetFieldData(IntPtr self, ClrDataAddress addr, out FieldData data);
        private delegate HResult DacGetObjectData(IntPtr self, ClrDataAddress addr, out V45ObjectData data);
        private delegate HResult DacGetCCWData(IntPtr self, ClrDataAddress addr, out CcwData data);
        private delegate HResult DacGetRCWData(IntPtr self, ClrDataAddress addr, out RcwData data);
        private delegate HResult DacGetWorkRequestData(IntPtr self, ClrDataAddress addr, out WorkRequestData data);
        private delegate HResult DacGetLocalModuleData(IntPtr self, ClrDataAddress addr, out DomainLocalModuleData data);
        private delegate HResult DacGetThreadFromThinLock(IntPtr self, uint id, out ClrDataAddress data);
        private delegate HResult DacGetCodeHeaps(IntPtr self, ClrDataAddress addr, int count, [Out] JitCodeHeapInfo[]? values, out int needed);
        private delegate HResult DacGetCOMPointers(IntPtr self, ClrDataAddress addr, int count, [Out] COMInterfacePointerData[] values, out int needed);
        private delegate HResult DacGetDomainLocalModuleDataFromAppDomain(IntPtr self, ClrDataAddress appDomainAddr, int moduleID, out DomainLocalModuleData data);
        private delegate HResult DacGetThreadLocalModuleData(IntPtr self, ClrDataAddress addr, uint id, out ThreadLocalModuleData data);
        private delegate HResult DacTraverseLoaderHeap(IntPtr self, ClrDataAddress addr, IntPtr callback);
        private delegate HResult DacTraverseStubHeap(IntPtr self, ClrDataAddress addr, int type, IntPtr callback);
        private delegate HResult DacTraverseModuleMap(IntPtr self, ModuleMapTraverseKind type, ClrDataAddress addr, IntPtr callback, IntPtr param);
        private delegate HResult DacGetJitManagers(IntPtr self, int count, [Out] JitManagerInfo[]? jitManagers, out int pNeeded);
        private delegate HResult GetModuleDelegate(IntPtr self, ClrDataAddress addr, out IntPtr iunk);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ISOSDacVTable
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