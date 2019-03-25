// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public unsafe class SOSNative : CallableCOMWrapper
    {
        public static Guid IID_ISOSNative = new Guid("90456375-3774-4c70-999a-a6fa78aab107");

        private ISOSNativeVTable* VTable => (ISOSNativeVTable*)_vtable;

        public SOSNative(DacLibrary library, IntPtr pUnk) : base(library.OwningLibrary, ref IID_ISOSNative, pUnk)
        {
        }

        public bool GetThreadStoreData(out NativeThreadStoreData data)
        {
            InitDelegate(ref _getThreadStoreData, VTable->GetThreadStoreData);
            int hr = _getThreadStoreData(Self, out data);
            return hr == S_OK;
        }

        public bool GetThreadData(ulong addr, out NativeThreadData data)
        {
            InitDelegate(ref _getThreadData, VTable->GetThreadData);
            int hr = _getThreadData(Self, addr, out data);
            return hr == S_OK;
        }

        public ulong GetCurrentExceptionObject(ulong thread)
        {
            InitDelegate(ref _getCurrentExceptionObject, VTable->GetCurrentExceptionObject);
            int hr = _getCurrentExceptionObject(Self, thread, out ulong result);

            return hr == S_OK ? result : 0;
        }

        public bool GetEETypeData(ulong addr, out EETypeData data)
        {
            InitDelegate(ref _getEETypeData, VTable->GetEETypeData);
            int hr = _getEETypeData(Self, addr, out data);
            return hr == S_OK;
        }

        public bool GetGCHeapData(out GCInfo data)
        {
            InitDelegate(ref _getGCHeapData, VTable->GetGCHeapData);
            int hr = _getGCHeapData(Self, out data);
            return hr == S_OK;
        }

        public ulong[] GetGCHeapList()
        {
            InitDelegate(ref _getGCHeapList, VTable->GetGCHeapList);
            int hr = _getGCHeapList(Self, 0, null, out int needed);

            if (hr != S_OK || needed <= 0)
                return new ulong[0];

            ulong[] heaps = new ulong[needed];
            hr = _getGCHeapList(Self, heaps.Length, heaps, out needed);

            // even if hr != S_OK, maybe we partially read the data
            return heaps;
        }

        public bool GetServerGCHeapDetails(ulong heap, out NativeHeapDetails details)
        {
            InitDelegate(ref _getGCHeapDetails, VTable->GetGCHeapDetails);
            int hr = _getGCHeapDetails(Self, heap, out details);
            return hr == S_OK;
        }

        public bool GetWorkstationGCHeapDetails(out NativeHeapDetails details)
        {
            InitDelegate(ref _getGCHeapStaticData, VTable->GetGCHeapStaticData);
            int hr = _getGCHeapStaticData(Self, out details);
            return hr == S_OK;
        }

        public bool GetGCHeapSegmentData(ulong segment, out NativeSegementData data)
        {
            InitDelegate(ref _getGCHeapSegmentData, VTable->GetGCHeapSegmentData);
            int hr = _getGCHeapSegmentData(Self, segment, out data);
            return hr == S_OK;
        }

        public ulong GetFreeEEType()
        {
            InitDelegate(ref _getFreeEEType, VTable->GetFreeEEType);
            int hr = _getFreeEEType(Self, out ulong free);
            return hr == S_OK ? free : 0;
        }

        public bool GetObjectData(ulong addr, out NativeObjectData data)
        {
            InitDelegate(ref _getObjectData, VTable->GetObjectData);
            int hr = _getObjectData(Self, addr, out data);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ThreadRootCallback(IntPtr token, ulong symbol, ulong address, ulong obj, int pinned, int interior);

        public bool TraverseStackRoots(ulong threadAddr, IntPtr initialContext, int contextSize, ThreadRootCallback callback, IntPtr token)
        {
            InitDelegate(ref _traverseStackRoots, VTable->TraverseStackRoots);

            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
            int hr = _traverseStackRoots(Self, threadAddr, initialContext, contextSize, ptr, token);

            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void HandleCallback(IntPtr token, ulong handleAddress, ulong dependentTarget, int handleType, uint refCount, int strong);

        public bool TraverseHandleTable(HandleCallback callback, IntPtr token)
        {
            InitDelegate(ref _traverseHandleTable, VTable->TraverseHandleTable);

            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
            int hr = _traverseHandleTable(Self, ptr, token);

            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void StaticRootCallback(IntPtr token, ulong addr, ulong obj, int pinned, int interior);

        public bool TraverseStaticRoots(StaticRootCallback callback, IntPtr token)
        {
            InitDelegate(ref _traverseStaticRoots, VTable->TraverseStaticRoots);

            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
            int hr = _traverseStaticRoots(Self, ptr, token);

            GC.KeepAlive(callback);
            return hr == S_OK;
        }

        public bool GetCodeHeaderData(ulong ip, out NativeCodeHeader data)
        {
            InitDelegate(ref _getCodeHeaderData, VTable->GetCodeHeaderData);
            int hr = _getCodeHeaderData(Self, ip, out data);
            return hr == S_OK;
        }

        public ulong[] GetModuleList()
        {
            InitDelegate(ref _getModuleList, VTable->GetModuleList);
            int hr = _getModuleList(Self, 0, null, out int needed);

            if (hr == 0 || needed == 0)
                return new ulong[0];

            ulong[] result = new ulong[needed];
            hr = _getModuleList(Self, result.Length, result, out needed);

            // Ignore hr, as we may have a partial list even on failure.
            return result;
        }

        private GetThreadStoreDataDelegate _getThreadStoreData;
        private GetThreadDataDelegate _getThreadData;
        private GetCurrentExceptionObjectDelegate _getCurrentExceptionObject;
        private GetEETypeDataDelegate _getEETypeData;

        private GetGCHeapDataDelegate _getGCHeapData;
        private GetGCHeapListDelegate _getGCHeapList;
        private GetGCHeapDetailsDelegate _getGCHeapDetails;
        private GetGCHeapStaticDataDelegate _getGCHeapStaticData;
        private GetGCHeapSegmentDataDelegate _getGCHeapSegmentData;

        private GetObjectDataDelegate _getObjectData;
        private GetFreeEETypeDelegate _getFreeEEType;

        private TraverseStackRootsDelegate _traverseStackRoots;
        private TraverseStaticRootsDelegate _traverseStaticRoots;
        private TraverseHandleTableDelegate _traverseHandleTable;

        private GetCodeHeaderDataDelegate _getCodeHeaderData;
        private GetModuleListDelegate _getModuleList;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetThreadStoreDataDelegate(IntPtr self, out NativeThreadStoreData pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetThreadDataDelegate(IntPtr self, ulong addr, out NativeThreadData pThread);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetCurrentExceptionObjectDelegate(IntPtr self, ulong thread, out ulong pExceptionRefAddress);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetObjectDataDelegate(IntPtr self, ulong addr, out NativeObjectData pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetEETypeDataDelegate(IntPtr self, ulong addr, out EETypeData pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGCHeapDataDelegate(IntPtr self, out GCInfo pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGCHeapListDelegate(
            IntPtr self,
            int count,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            ulong[] heaps,
            out int pNeeded);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGCHeapDetailsDelegate(IntPtr self, ulong heap, out NativeHeapDetails details);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGCHeapStaticDataDelegate(IntPtr self, out NativeHeapDetails data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGCHeapSegmentDataDelegate(IntPtr self, ulong segment, out NativeSegementData pSegmentData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetFreeEETypeDelegate(IntPtr self, out ulong freeType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TraverseStackRootsDelegate(IntPtr self, ulong threadAddr, IntPtr pInitialContext, int initialContextSize, IntPtr pCallback, IntPtr token);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TraverseStaticRootsDelegate(IntPtr self, IntPtr pCallback, IntPtr token);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TraverseHandleTableDelegate(IntPtr self, IntPtr pCallback, IntPtr token);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetCodeHeaderDataDelegate(IntPtr self, ulong ip, out NativeCodeHeader pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleListDelegate(
            IntPtr self,
            int count,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            ulong[] modules,
            out int pNeeded);
    }
}