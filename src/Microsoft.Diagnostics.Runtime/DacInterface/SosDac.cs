// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    internal sealed unsafe class SOSDac : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac = new("436f00f2-b42a-4b9f-870c-e73db66ae930");

        private readonly DacLibrary _library;
        private volatile Dictionary<int, string>? _regNames;
        private volatile Dictionary<ClrDataAddress, string>? _frameNames;

        // CLRDATA_REQUEST_REVISION version from the DAC
        public int DacVersion { get; set; }

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

        public RejitData[] GetRejitData(ClrDataAddress md, ClrDataAddress ip = default)
        {
            HResult hr = VTable.GetMethodDescData(Self, md.ToInteropAddress(), ip.ToInteropAddress(), out _, 0, null, out int needed);
            if (hr && needed >= 1)
            {
                RejitData[] result = new RejitData[needed];
                hr = VTable.GetMethodDescData(Self, md.ToInteropAddress(), ip.ToInteropAddress(), out _, result.Length, result, out _);
                if (hr)
                    return result;
            }

            return Array.Empty<RejitData>();
        }

        public HResult GetMethodDescData(ClrDataAddress md, ClrDataAddress ip, out MethodDescData data)
        {
            return VTable.GetMethodDescData(Self, md.ToInteropAddress(), ip.ToInteropAddress(), out data, 0, null, out _);
        }

        public HResult GetThreadStoreData(out ThreadStoreData data)
        {
            return VTable.GetThreadStoreData(Self, out data);
        }

        public string? GetRegisterName(int index)
        {
            Dictionary<int, string> regNames = _regNames ??= new();
            lock (regNames)
                if (regNames.TryGetValue(index, out string? cached))
                    return cached;

            // Register names shouldn't be big.
            Span<char> buffer = stackalloc char[32];

            fixed (char* ptr = buffer)
            {
                HResult hr = VTable.GetRegisterName(Self, index, buffer.Length, ptr, out int needed);
                if (!hr)
                    return null;

                if (needed == 0)
                    return string.Empty;

                int len = buffer.IndexOf((char)0);
                if (len >= 0)
                    buffer = buffer.Slice(0, len);

                string result = new(ptr, 0, buffer.Length);
                lock (regNames)
                    regNames[index] = result;

                return result;
            }
        }

        public uint GetTlsIndex()
        {
            HResult hr = VTable.GetTLSIndex(Self, out uint index);
            if (hr)
                return index;

            return uint.MaxValue;
        }

        public ClrDataAddress GetThreadFromThinlockId(uint id)
        {
            HResult hr = VTable.GetThreadFromThinlockID(Self, id, out ClrDataAddress thread);
            if (hr)
                return thread;

            return ClrDataAddress.Null;
        }

        public string? GetMethodDescName(ClrDataAddress md)
        {
            if (md.IsNull)
                return null;

            HResult hr = VTable.GetMethodDescName(Self, md.ToInteropAddress(), 0, null, out int needed);
            if (!hr)
                return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(needed * sizeof(char));
            try
            {
                int actuallyNeeded;
                fixed (byte* bufferPtr = buffer)
                {
                    hr = VTable.GetMethodDescName(Self, md.ToInteropAddress(), needed, bufferPtr, out actuallyNeeded);
                    if (!hr)
                        return null;
                }

                // Patch for a bug on sos side :
                //  Sometimes, when the target method has parameters with generic types
                //  the first call to GetMethodDescName sets an incorrect value into pNeeded.
                //  In those cases, a second call directly after the first returns the correct value.
                if (needed != actuallyNeeded)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = ArrayPool<byte>.Shared.Rent(actuallyNeeded * sizeof(char));
                    fixed (byte* bufferPtr = buffer)
                    {
                        hr = VTable.GetMethodDescName(Self, md.ToInteropAddress(), actuallyNeeded, bufferPtr, out actuallyNeeded);
                        if (!hr)
                            return null;
                    }
                }

                return Encoding.Unicode.GetString(buffer, 0, (actuallyNeeded - 1) * sizeof(char));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public ClrDataAddress GetMethodTableSlot(ClrDataAddress mt, uint slot)
        {
            if (mt.IsNull)
                return ClrDataAddress.Null;

            HResult hr = VTable.GetMethodTableSlot(Self, mt.ToInteropAddress(), slot, out ClrDataAddress ip);
            if (hr)
                return ip;

            return ClrDataAddress.Null;
        }

        public HResult GetThreadLocalModuleData(ClrDataAddress thread, uint index, out ThreadLocalModuleData data)
        {
            return VTable.GetThreadLocalModuleData(Self, thread.ToInteropAddress(), index, out data);
        }

        public ClrDataAddress GetILForModule(ClrDataAddress moduleAddr, uint rva)
        {
            HResult hr = VTable.GetILForModule(Self, moduleAddr.ToInteropAddress(), rva, out ClrDataAddress result);
            if (hr)
                return result;

            return ClrDataAddress.Null;
        }

        public COMInterfacePointerData[]? GetCCWInterfaces(ClrDataAddress ccw, int count)
        {
            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            fixed (COMInterfacePointerData* ptr = data)
            {
                HResult hr = VTable.GetCCWInterfaces(Self, ccw.ToInteropAddress(), count, ptr, out int pNeeded);
                if (hr)
                    return data;
            }

            return null;
        }

        public COMInterfacePointerData[]? GetRCWInterfaces(ClrDataAddress ccw, int count)
        {
            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            fixed (COMInterfacePointerData* ptr = data)
            {
                HResult hr = VTable.GetRCWInterfaces(Self, ccw.ToInteropAddress(), count, ptr, out int pNeeded);
                if (hr)
                    return data;
            }

            return null;
        }

        public HResult GetDomainLocalModuleDataFromModule(ClrDataAddress module, out DomainLocalModuleData data)
        {
            return VTable.GetDomainLocalModuleDataFromModule(Self, module.ToInteropAddress(), out data);
        }

        public HResult GetDomainLocalModuleDataFromAppDomain(ClrDataAddress appDomain, int id, out DomainLocalModuleData data)
        {
            return VTable.GetDomainLocalModuleDataFromAppDomain(Self, appDomain.ToInteropAddress(), id, out data);
        }

        public HResult GetWorkRequestData(ClrDataAddress request, out WorkRequestData data)
        {
            return VTable.GetWorkRequestData(Self, request.ToInteropAddress(), out data);
        }

        public HResult GetThreadPoolData(out ThreadPoolData data)
        {
            return VTable.GetThreadpoolData(Self, out data);
        }

        public HResult GetSyncBlockData(int index, out SyncBlockData data)
        {
            return VTable.GetSyncBlockData(Self, index, out data);
        }

        public string? GetAppBase(ClrDataAddress domain)
        {
            return GetString(VTable.GetApplicationBase, domain);
        }

        public string? GetConfigFile(ClrDataAddress domain)
        {
            return GetString(VTable.GetAppDomainConfigFile, domain);
        }

        public HResult GetCodeHeaderData(ClrDataAddress ip, out CodeHeaderData codeHeaderData)
        {
            if (ip.IsNull)
            {
                codeHeaderData = default;
                return HResult.E_INVALIDARG;
            }
            return VTable.GetCodeHeaderData(Self, ip.ToInteropAddress(), out codeHeaderData);
        }

        public ClrDataAddress GetMethodDescPtrFromFrame(ClrDataAddress frame)
        {
            HResult hr = VTable.GetMethodDescPtrFromFrame(Self, frame.ToInteropAddress(), out ClrDataAddress data);
            if (hr)
                return data;

            return ClrDataAddress.Null;
        }

        public ClrDataAddress GetMethodDescPtrFromIP(ClrDataAddress frame)
        {
            HResult hr = VTable.GetMethodDescPtrFromIP(Self, frame.ToInteropAddress(), out ClrDataAddress data);
            if (hr)
                return data;

            return ClrDataAddress.Null;
        }

        public string GetFrameName(ClrDataAddress vtable)
        {
            Dictionary<ClrDataAddress, string> frameNames = _frameNames ??= new();
            lock (frameNames)
            {
                if (_frameNames.TryGetValue(vtable, out string? cached))
                    return cached;
            }

            string? result = GetString(VTable.GetFrameName, vtable, false);
            if (result is not null)
            {
                lock (frameNames)
                    _frameNames[vtable] = result;

                return result;
            }

            // Don't cache failed lookups.  We might have a bad stackwalk where we get 1000s of bad
            // frame vtables and we won't want to eat up memory storing those in the cache.
            return "Unknown Frame";
        }

        public HResult GetFieldInfo(ClrDataAddress mt, out MethodTableFieldInfo data)
        {
            if (mt.IsNull)
            {
                data = default;
                return HResult.E_INVALIDARG;
            }

            return VTable.GetMethodTableFieldData(Self, mt.ToInteropAddress(), out data);
        }

        public HResult GetFieldData(ClrDataAddress fieldDesc, out FieldData data)
        {
            return VTable.GetFieldDescData(Self, fieldDesc.ToInteropAddress(), out data);
        }

        public HResult GetObjectData(ClrDataAddress obj, out ObjectData data)
        {
            return VTable.GetObjectData(Self, obj.ToInteropAddress(), out data);
        }

        public HResult GetCCWData(ClrDataAddress ccw, out CcwData data)
        {
            return VTable.GetCCWData(Self, ccw.ToInteropAddress(), out data);
        }

        public HResult GetRCWData(ClrDataAddress rcw, out RcwData data)
        {
            return VTable.GetRCWData(Self, rcw.ToInteropAddress(), out data);
        }

        public IEnumerable<(ClrDataAddress Rcw, ClrDataAddress Context, ClrDataAddress Thread, bool IsFreeThreaded)> EnumerateRCWCleanup(ClrDataAddress cleanupList)
        {
            List<(ClrDataAddress, ClrDataAddress, ClrDataAddress, bool)> result = new();
            RcwCleanupTraverse traverse = (rcw, context, thread, freeThreaded, token) =>
            {
                result.Add((rcw, context, thread, freeThreaded != 0));

                return result.Count > 16384 ? 0 : 1u;
            };

            VTable.TraverseRCWCleanupList(Self, cleanupList.ToInteropAddress(), Marshal.GetFunctionPointerForDelegate(traverse), 0);

            GC.KeepAlive(traverse);
            return result;
        }

        public HResult GetSyncBlockCleanupData(ClrDataAddress syncBlockCleanupPointer, out SyncBlockCleanupData data)
        {
            return VTable.GetSyncBlockCleanupData(Self, syncBlockCleanupPointer.ToInteropAddress(), out data);
        }

        public delegate uint RcwCleanupTraverse(ClrDataAddress rcw, ClrDataAddress context, ClrDataAddress thread, uint isFreeThreaded, IntPtr token);

        public ClrDataModule? GetClrDataModule(ClrDataAddress module)
        {
            if (module.IsNull)
                return null;

            HResult hr = VTable.GetModule(Self, module.ToInteropAddress(), out IntPtr iunk);
            if (hr)
                return new ClrDataModule(_library, iunk);

            return null;
        }

        public MetadataImport? GetMetadataImport(ClrDataAddress module)
        {
            if (module.IsNull)
                return null;

            HResult hr = VTable.GetModule(Self, module.ToInteropAddress(), out IntPtr iunk);
            if (!hr)
                return null;

            // Make sure we can successfully QueryInterface for IMetaDataImport.  This may fail if
            // we do not have all of the relevant metadata mapped into memory either through the dump
            // or via the binary locator.
            if (QueryInterface(iunk, MetadataImport.IID_IMetaDataImport, out IntPtr pTmp))
                Release(pTmp);
            else
                return null;

            try
            {
                return new MetadataImport(_library, iunk);
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        public HResult GetCommonMethodTables(out CommonMethodTables commonMTs)
        {
            return VTable.GetUsefulGlobals(Self, out commonMTs);
        }

        public ClrDataAddress[] GetAssemblyList(ClrDataAddress appDomain) => GetAssemblyList(appDomain, 0);

        public ClrDataAddress[] GetAssemblyList(ClrDataAddress appDomain, int count) => GetModuleOrAssembly(appDomain, count, VTable.GetAssemblyList);

        public ClrDataAddress[] GetModuleList(ClrDataAddress assembly) => GetModuleList(assembly, 0);

        public ClrDataAddress[] GetModuleList(ClrDataAddress assembly, int count) => GetModuleOrAssembly(assembly, count, VTable.GetAssemblyModuleList);

        public HResult GetAssemblyData(ClrDataAddress domain, ClrDataAddress assembly, out AssemblyData data)
        {
            // The dac seems to have an issue where the assembly data can be filled in for a minidump.
            // If the data is partially filled in, we'll use it.

            HResult hr = VTable.GetAssemblyData(Self, domain.ToInteropAddress(), assembly.ToInteropAddress(), out data);
            if (!hr && data.Address == assembly)
                return HResult.S_FALSE;

            return hr;
        }

        public HResult GetAppDomainData(ClrDataAddress addr, out AppDomainData data)
        {
            // We can face an exception while walking domain data if we catch the process
            // at a bad state.  As a workaround we will return partial data if data.Address
            // and data.StubHeap are set.

            HResult hr = VTable.GetAppDomainData(Self, addr.ToInteropAddress(), out data);
            if (!hr && data.Address == addr && !data.StubHeap.IsNull)
                return HResult.S_FALSE;

            return hr;
        }

        public string? GetAppDomainName(ClrDataAddress appDomain)
        {
            if (appDomain.IsNull)
            {
                return null;
            }

            return GetString(VTable.GetAppDomainName, appDomain);
        }

        public string? GetAssemblyName(ClrDataAddress assembly)
        {
            return GetString(VTable.GetAssemblyName, assembly);
        }

        public HResult GetAppDomainStoreData(out AppDomainStoreData data)
        {
            return VTable.GetAppDomainStoreData(Self, out data);
        }

        public HResult GetMethodTableData(ClrDataAddress addr, out MethodTableData data)
        {
            // If the 2nd bit is set it means addr is actually a TypeHandle (which GetMethodTable does not support).
            if (addr.IsNull || (addr.ToInteropAddress() & 2) == 2)
            {
                data = default;
                return HResult.E_INVALIDARG;
            }
            return VTable.GetMethodTableData(Self, addr.ToInteropAddress(), out data);
        }

        public string? GetMethodTableName(ClrDataAddress mt)
        {
            return GetString(VTable.GetMethodTableName, mt);
        }

        public string? GetJitHelperFunctionName(ClrDataAddress addr)
        {
            return GetAsciiString(VTable.GetJitHelperFunctionName, addr);
        }

        public string? GetPEFileName(ClrDataAddress pefile)
        {
            return GetString(VTable.GetPEFileName, pefile);
        }

        private string? GetString(delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> func, ClrDataAddress addr, bool skipNull = true)
        {
            HResult hr = func(Self, addr.ToInteropAddress(), 0, null, out int needed);
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
                    hr = func(Self, addr.ToInteropAddress(), needed, bufferPtr, out needed);

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

        private string? GetAsciiString(delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> func, ClrDataAddress addr)
        {
            HResult hr = func(Self, addr.ToInteropAddress(), 0, null, out int needed);
            if (!hr)
                return null;

            if (needed == 0)
                return string.Empty;

            byte[]? array = null;
            Span<byte> buffer = needed <= 32 ? stackalloc byte[needed] : (array = ArrayPool<byte>.Shared.Rent(needed)).AsSpan(0, needed);

            try
            {
                fixed (byte* bufferPtr = buffer)
                    hr = func(Self, addr.ToInteropAddress(), needed, bufferPtr, out needed);

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

        public ClrDataAddress GetMethodTableByEEClass(ClrDataAddress eeclass)
        {
            HResult hr = VTable.GetMethodTableForEEClass(Self, eeclass.ToInteropAddress(), out ClrDataAddress data);
            if (hr)
                return data;

            return ClrDataAddress.Null;
        }

        public HResult GetModuleData(ClrDataAddress module, out ModuleData data)
        {
            return VTable.GetModuleData(Self, module.ToInteropAddress(), out data);
        }

        private ClrDataAddress[] GetModuleOrAssembly(ClrDataAddress address, int count, delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, ClrDataAddress*, out int, int> func)
        {
            int needed;
            if (count <= 0)
            {
                if (func(Self, address.ToInteropAddress(), 0, null, out needed) < 0)
                    return Array.Empty<ClrDataAddress>();

                count = needed;
            }

            // We ignore the return value here since the list may be partially filled
            ClrDataAddress[] modules = new ClrDataAddress[count];
            fixed (ClrDataAddress* ptr = modules)
                func(Self, address.ToInteropAddress(), modules.Length, ptr, out needed);

            return modules;
        }

        public ClrDataAddress[] GetAppDomainList(int count = 0)
        {
            if (count <= 0)
            {
                if (!GetAppDomainStoreData(out AppDomainStoreData addata))
                    return Array.Empty<ClrDataAddress>();

                count = addata.AppDomainCount;
            }

            ClrDataAddress[] data = new ClrDataAddress[count];
            fixed (ClrDataAddress* ptr = data)
            {
                HResult hr = VTable.GetAppDomainList(Self, data.Length, ptr, out int needed);
                return hr ? data : Array.Empty<ClrDataAddress>();
            }
        }

        public HResult GetThreadData(ClrDataAddress address, out ThreadData data)
        {
            if (address.IsNull)
            {
                data = default;
                return HResult.E_INVALIDARG;
            }
            return VTable.GetThreadData(Self, address.ToInteropAddress(), out data);
        }

        public HResult GetGCHeapData(out GCInfo data)
        {
            return VTable.GetGCHeapData(Self, out data);
        }

        public HResult GetOOMData(out DacOOMData oomData) => VTable.GetOOMStaticData(Self, out oomData);

        public HResult GetOOMData(ClrDataAddress address, out DacOOMData oomData) => VTable.GetOOMData(Self, address.ToInteropAddress(), out oomData);

        public HResult GetHeapAnalyzeData(out DacHeapAnalyzeData analyzeData) => VTable.GetHeapAnalyzeStaticData(Self, out analyzeData);

        public HResult GetHeapAnalyzeData(ClrDataAddress address, out DacHeapAnalyzeData analyzeData) => VTable.GetHeapAnalyzeData(Self, address.ToInteropAddress(), out analyzeData);

        public HResult GetSegmentData(ClrDataAddress addr, out SegmentData data)
        {
            return VTable.GetHeapSegmentData(Self, addr.ToInteropAddress(), out data);
        }

        public ClrDataAddress[] GetHeapList(int heapCount)
        {
            ClrDataAddress[] refs = new ClrDataAddress[heapCount];
            fixed (ClrDataAddress* ptr = refs)
            {
                HResult hr = VTable.GetGCHeapList(Self, heapCount, ptr, out int needed);
                return hr ? refs : Array.Empty<ClrDataAddress>();
            }
        }

        public HResult GetServerHeapDetails(ClrDataAddress addr, out HeapDetails data)
        {
            return VTable.GetGCHeapDetails(Self, addr.ToInteropAddress(), out data);
        }

        public HResult GetWksHeapDetails(out HeapDetails data)
        {
            return VTable.GetGCHeapStaticData(Self, out data);
        }

        public JitManagerData[] GetJitManagers()
        {
            HResult hr = VTable.GetJitManagerList(Self, 0, null, out int needed);
            if (!hr || needed == 0)
                return Array.Empty<JitManagerData>();

            JitManagerData[] result = new JitManagerData[needed];
            fixed (JitManagerData* ptr = result)
            {
                hr = VTable.GetJitManagerList(Self, result.Length, ptr, out needed);
                return hr ? result : Array.Empty<JitManagerData>();
            }
        }

        public JitCodeHeapInfo[] GetCodeHeapList(ClrDataAddress jitManager)
        {
            HResult hr = VTable.GetCodeHeapList(Self, jitManager.ToInteropAddress(), 0, null, out int needed);
            if (!hr || needed == 0)
                return Array.Empty<JitCodeHeapInfo>();

            JitCodeHeapInfo[] result = new JitCodeHeapInfo[needed];
            fixed (JitCodeHeapInfo* ptr = result)
            {
                hr = VTable.GetCodeHeapList(Self, jitManager.ToInteropAddress(), result.Length, ptr, out needed);
                return hr ? result : Array.Empty<JitCodeHeapInfo>();
            }
        }

        public enum ModuleMapTraverseKind
        {
            TypeDefToMethodTable,
            TypeRefToMethodTable
        }

        public delegate void ModuleMapTraverse(int index, ulong methodTable, IntPtr token);

        public HResult TraverseModuleMap(ModuleMapTraverseKind mt, ClrDataAddress module, ModuleMapTraverse traverse)
        {
            if (module.IsNull)
                return HResult.E_INVALIDARG;

            HResult hr = VTable.TraverseModuleMap(Self, mt, module.ToInteropAddress(), Marshal.GetFunctionPointerForDelegate(traverse), IntPtr.Zero);
            GC.KeepAlive(traverse);
            return hr;
        }

        public delegate void LoaderHeapTraverse(ulong address, IntPtr size, int isCurrent);

        public HResult TraverseLoaderHeap(ClrDataAddress heap, LoaderHeapTraverse callback)
        {
            HResult hr = VTable.TraverseLoaderHeap(Self, heap.ToInteropAddress(), Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr;
        }

        public enum VCSHeapType
        {
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap,
            VtableHeap
        }

        public HResult TraverseStubHeap(ClrDataAddress heap, VCSHeapType type, LoaderHeapTraverse callback)
        {
            if (heap.IsNull)
                return HResult.E_INVALIDARG;

            HResult hr = VTable.TraverseVirtCallStubHeap(Self, heap.ToInteropAddress(), type, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr;
        }

        // Prior to CLRDATA_REQUEST_REVISION 10, the DAC's DefaultCOMImpl::Release() used
        // post-decrement (mRef--) instead of pre-decrement (--mRef), meaning the ref count
        // never reached zero and objects were never freed. The CallableCOMWrapper constructor
        // calls QueryInterface (AddRef) then Release on the original pointer, leaving us with
        // one extra ref from the DAC's leak. This method releases that extra ref on old DACs.
        // With the fix (version >= 10) the DAC correctly frees at zero so we must not Release.
        private void ReleaseLeakedDacRef(CallableCOMWrapper wrapper, string apiName)
        {
            if (DacVersion < 10)
            {
                int count = wrapper.Release();
                if (count == 0)
                    throw new InvalidOperationException($"We expected to borrow a reference from {apiName}, but instead fully released the object!");
            }
        }

        public SOSHandleEnum? EnumerateHandles(params ClrHandleKind[] types)
        {
            fixed (ClrHandleKind* ptr = types)
            {
                HResult hr = VTable.GetHandleEnumForTypes(Self, ptr, types.Length, out IntPtr ptrEnum);
                if (hr)
                {
                    SOSHandleEnum result = new(_library, ptrEnum);
                    ReleaseLeakedDacRef(result, nameof(ISOSDacVTable.GetHandleEnumForTypes));
                    return result;
                }
            }

            return null;
        }

        public SOSHandleEnum? EnumerateHandles()
        {
            HResult hr = VTable.GetHandleEnum(Self, out IntPtr ptrEnum);
            if (hr)
            {
                SOSHandleEnum result = new(_library, ptrEnum);
                ReleaseLeakedDacRef(result, nameof(ISOSDacVTable.GetHandleEnum));
                return result;
            }

            return null;
        }

        public SOSStackRefEnum? EnumerateStackRefs(uint osThreadId)
        {
            HResult hr = VTable.GetStackReferences(Self, osThreadId, out IntPtr ptrEnum);

            if (hr)
            {
                SOSStackRefEnum result = new(_library, ptrEnum);
                ReleaseLeakedDacRef(result, nameof(ISOSDacVTable.GetStackReferences));
                return result;
            }
            else
            {
                Debug.WriteLine($"EnumerateStackRefs for OSThreadId:{osThreadId:x} failed with hr={hr}");
            }

            return null;
        }

        public ClrDataAddress GetMethodDescFromToken(ClrDataAddress module, int token)
        {
            HResult hr = VTable.GetMethodDescFromToken(Self, module.ToInteropAddress(), token, out ClrDataAddress md);
            if (hr)
                return md;

            return ClrDataAddress.Null;
        }
        private delegate HResult DacGetJitManagerInfo(IntPtr self, ClrDataAddress addr, out JitManagerData data);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct ISOSDacVTable
    {
        // ThreadStore
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out ThreadStoreData, int> GetThreadStoreData;

        // AppDomains
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out AppDomainStoreData, int> GetAppDomainStoreData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, ClrDataAddress*, out int, int> GetAppDomainList;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out AppDomainData, int> GetAppDomainData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetAppDomainName;
        public readonly IntPtr GetDomainFromContext;

        // Assemblies
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, ClrDataAddress*, out int, int> GetAssemblyList;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, ulong /*ClrDataAddress*/, out AssemblyData, int> GetAssemblyData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetAssemblyName;

        // Modules
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out IntPtr, int> GetModule;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ModuleData, int> GetModuleData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, SOSDac.ModuleMapTraverseKind, ulong /*ClrDataAddress*/, IntPtr, IntPtr, int> TraverseModuleMap;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, ClrDataAddress*, out int, int> GetAssemblyModuleList;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, uint, out ClrDataAddress, int> GetILForModule;

        // Threads

        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ThreadData, int> GetThreadData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, out ClrDataAddress, int> GetThreadFromThinlockID;
        public readonly IntPtr GetStackLimits;

        // MethodDescs

        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, ulong /*ClrDataAddress*/, out MethodDescData, int, RejitData[]?, out int, int> GetMethodDescData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ClrDataAddress, int> GetMethodDescPtrFromIP;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetMethodDescName;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ClrDataAddress, int> GetMethodDescPtrFromFrame;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, out ClrDataAddress, int> GetMethodDescFromToken;
        private readonly IntPtr GetMethodDescTransparencyData;

        // JIT Data
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out CodeHeaderData, int> GetCodeHeaderData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, JitManagerData*, out int, int> GetJitManagerList;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetJitHelperFunctionName;
        private readonly IntPtr GetJumpThunkTarget;

        // ThreadPool

        public readonly delegate* unmanaged[Stdcall]<IntPtr, out ThreadPoolData, int> GetThreadpoolData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out WorkRequestData, int> GetWorkRequestData;
        private readonly IntPtr GetHillClimbingLogEntry;

        // Objects
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ObjectData, int> GetObjectData;
        public readonly IntPtr GetObjectStringData;
        public readonly IntPtr GetObjectClassName;

        // MethodTable
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetMethodTableName;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out MethodTableData, int> GetMethodTableData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, uint, out ClrDataAddress, int> GetMethodTableSlot;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out MethodTableFieldInfo, int> GetMethodTableFieldData;
        private readonly IntPtr GetMethodTableTransparencyData;

        // EEClass
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out ClrDataAddress, int> GetMethodTableForEEClass;

        // FieldDesc
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out FieldData, int> GetFieldDescData;

        // Frames
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetFrameName;

        // PEFiles
        public readonly IntPtr GetPEFileBase;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetPEFileName;

        // GC
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out GCInfo, int> GetGCHeapData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, ClrDataAddress*, out int, int> GetGCHeapList; // svr only
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out HeapDetails, int> GetGCHeapDetails; // wks only
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out HeapDetails, int> GetGCHeapStaticData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out SegmentData, int> GetHeapSegmentData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out DacOOMData, int> GetOOMData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out DacOOMData, int> GetOOMStaticData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out DacHeapAnalyzeData, int> GetHeapAnalyzeData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out DacHeapAnalyzeData, int> GetHeapAnalyzeStaticData;

        // DomainLocal
        private readonly IntPtr GetDomainLocalModuleData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, out DomainLocalModuleData, int> GetDomainLocalModuleDataFromAppDomain;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out DomainLocalModuleData, int> GetDomainLocalModuleDataFromModule;

        // ThreadLocal
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, uint, out ThreadLocalModuleData, int> GetThreadLocalModuleData;

        // SyncBlock
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, out SyncBlockData, int> GetSyncBlockData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out SyncBlockCleanupData, int> GetSyncBlockCleanupData;

        // Handles
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int> GetHandleEnum;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ClrHandleKind*, int, out IntPtr, int> GetHandleEnumForTypes;
        private readonly IntPtr GetHandleEnumForGC;

        // EH
        private readonly IntPtr TraverseEHInfo;
        private readonly IntPtr GetNestedExceptionData;

        // StressLog
        public readonly IntPtr GetStressLogAddress;

        // Heaps
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, IntPtr, int> TraverseLoaderHeap;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, JitCodeHeapInfo*, out int, int> GetCodeHeapList;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, SOSDac.VCSHeapType, IntPtr, int> TraverseVirtCallStubHeap;

        // Other
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out CommonMethodTables, int> GetUsefulGlobals;
        public readonly IntPtr GetClrWatsonBuckets;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out uint, int> GetTLSIndex;
        public readonly IntPtr GetDacModuleHandle;

        // COM
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out RcwData, int> GetRCWData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, COMInterfacePointerData*, out int, int> GetRCWInterfaces;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out CcwData, int> GetCCWData;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, COMInterfacePointerData*, out int, int> GetCCWInterfaces;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, nint, nint, int> TraverseRCWCleanupList;

        // GC Reference Functions
        public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, out IntPtr, int> GetStackReferences;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, int, char*, out int, int> GetRegisterName;
        public readonly IntPtr GetThreadAllocData;
        public readonly IntPtr GetHeapAllocData;

        // For BindingDisplay plugin

        public readonly IntPtr GetFailedAssemblyList;
        public readonly IntPtr GetPrivateBinPaths;
        public readonly IntPtr GetAssemblyLocation;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetAppDomainConfigFile;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, int, byte*, out int, int> GetApplicationBase;
        public readonly IntPtr GetFailedAssemblyData;
        public readonly IntPtr GetFailedAssemblyLocation;
        public readonly IntPtr GetFailedAssemblyDisplayName;
    }
}
