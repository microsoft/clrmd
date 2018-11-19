// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Runtime.InteropServices;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("d50b1d22-dc01-4d68-b71d-761f9d49f980")]
    internal interface ISerializedExceptionEnumerator
    {
        bool HasNext();
        ISerializedException Next();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("d681b4fd-87e1-42ec-af44-4938e62bd266")]
    internal interface ISerializedException
    {
        ulong ExceptionId { get; }
        ulong InnerExceptionId { get; }
        ulong ThreadId { get; }
        ulong NestingLevel { get; }
        ulong ExceptionCCWPtr { get; }
        ulong ExceptionEEType { get; }
        ulong HResult { get; }
        ISerializedStackFrameEnumerator StackFrames { get; }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("9141efb3-370c-4a7a-bdf5-7f2f7d6dc2f4")]
    internal interface ISOSNativeSerializedExceptionSupport
    {
        ISerializedExceptionEnumerator GetSerializedExceptions();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4bf18ce1-8166-4dfc-b540-4a79dd1ebe19")]
    internal interface ISerializedStackFrame
    {
        ulong IP { get; }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6091d53a-9371-4573-ae00-93b61d17ca04")]
    internal interface ISerializedStackFrameEnumerator
    {
        bool HasNext();
        ISerializedStackFrame Next();
    }
    
    // This is unsupported and undocumented.
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

        #region Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetThreadStoreDataDelegate(IntPtr self, out NativeThreadStoreData pData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetThreadDataDelegate(IntPtr self, ulong addr, out NativeThreadData pThread);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetCurrentExceptionObjectDelegate(IntPtr self, ulong thread, out ulong pExceptionRefAddress);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetObjectDataDelegate(IntPtr self, ulong addr, out NativeObjectData pData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetEETypeDataDelegate(IntPtr self, ulong addr, out EETypeData pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetGCHeapDataDelegate(IntPtr self, out GCInfo pData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetGCHeapListDelegate(IntPtr self, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] heaps, out int pNeeded);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetGCHeapDetailsDelegate(IntPtr self, ulong heap, out NativeHeapDetails details);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetGCHeapStaticDataDelegate(IntPtr self, out NativeHeapDetails data);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetGCHeapSegmentDataDelegate(IntPtr self, ulong segment, out NativeSegementData pSegmentData);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetFreeEETypeDelegate(IntPtr self, out ulong freeType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int TraverseStackRootsDelegate(IntPtr self, ulong threadAddr, IntPtr pInitialContext, int initialContextSize, IntPtr pCallback, IntPtr token);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int TraverseStaticRootsDelegate(IntPtr self, IntPtr pCallback, IntPtr token);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int TraverseHandleTableDelegate(IntPtr self, IntPtr pCallback, IntPtr token);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetCodeHeaderDataDelegate(IntPtr self, ulong ip, out NativeCodeHeader pData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetModuleListDelegate(IntPtr self, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] modules, out int pNeeded);
        #endregion
    }

#pragma warning disable 0169
    internal readonly struct ISOSNativeVTable
    {
        public readonly IntPtr Flush;
        
        public readonly IntPtr GetThreadStoreData;
        private readonly IntPtr GetThreadAddress;
        public readonly IntPtr  GetThreadData;
        public readonly IntPtr GetCurrentExceptionObject;

        public readonly IntPtr GetObjectData;
        public readonly IntPtr GetEETypeData;


        private readonly IntPtr GetGcHeapAnalyzeData;
        public readonly IntPtr GetGCHeapData;
        public readonly IntPtr GetGCHeapList;
        public readonly IntPtr GetGCHeapDetails;
        public readonly IntPtr GetGCHeapStaticData;
        public readonly IntPtr GetGCHeapSegmentData;
        public readonly IntPtr GetFreeEEType;
        
        private readonly IntPtr DumpGCInfo;
        private readonly IntPtr DumpEHInfo;

        private readonly IntPtr DumpStackObjects;
        public readonly IntPtr TraverseStackRoots;
        public readonly IntPtr TraverseStaticRoots;
        public readonly IntPtr TraverseHandleTable;
        private readonly IntPtr TraverseHandleTableFiltered;

        public readonly IntPtr GetCodeHeaderData;
        public readonly IntPtr GetModuleList;
        
        private readonly IntPtr GetStressLogAddress;
        private readonly IntPtr GetStressLogData;
        private readonly IntPtr EnumStressLogMessages;
        private readonly IntPtr EnumStressLogMemRanges;
        
        private readonly IntPtr UpdateDebugEventFilter;

        private readonly IntPtr UpdateCurrentExceptionNotificationFrame;
        private readonly IntPtr EnumGcStressStatsInfo;
    }
    
    public readonly struct NativeCodeHeader
    {
        public readonly ulong GCInfo;
        public readonly ulong EHInfo;
        public readonly ulong MethodStart;
        public readonly uint MethodSize;
    }

    public enum DacpObjectType
    {
        OBJ_FREE = 0,
        OBJ_OBJECT = 1,
        OBJ_VALUETYPE = 2,
        OBJ_ARRAY = 3,
        OBJ_OTHER = 4
    }

    public readonly struct NativeObjectData
    {
        public readonly ulong MethodTable;
        public readonly DacpObjectType ObjectType;
        public readonly uint Size;
        public readonly ulong ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint dwRank;
        public readonly uint dwNumComponents;
        public readonly uint dwComponentSize;
        public readonly ulong ArrayDataPointer;
        public readonly ulong ArrayBoundsPointer;
        public readonly ulong ArrayLowerBoundsPointer;
    }

    public readonly struct NativeThreadStoreData
    {
        public readonly int ThreadCount;
        public readonly ulong FirstThread;
        public readonly ulong FinalizerThread;
        public readonly ulong GCThread;
    }

    public readonly struct NativeThreadData
    {
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public readonly ulong AllocContextPointer;
        public readonly ulong AllocContextLimit;
        public readonly ulong Context;
        public readonly ulong Teb;
        public readonly ulong NextThread;
    }

    public readonly struct EETypeData
    {
        public readonly uint ObjectType; // everything else is NULL if this is true.
        public readonly ulong CanonicalMethodTable;
        public readonly ulong ParentMethodTable;
        public readonly ushort NumInterfaces;
        public readonly ushort NumVTableSlots;
        public readonly uint BaseSize;
        public readonly uint ComponentSize;
        public readonly uint SizeOfMethodTable;
        public readonly uint ContainsPointers;
        public readonly ulong ElementTypeHandle;
    }

    public readonly struct NativeSegementData
    {
        public readonly ulong Address;
        public readonly ulong Allocated;
        public readonly ulong Committed;
        public readonly ulong Reserved;
        public readonly ulong Used;
        public readonly ulong Mem;
        public readonly ulong Next;
        public readonly ulong Heap;
        public readonly ulong HighAllocMark;
        public readonly uint IsReadOnly;
    }

    public readonly struct NativeHeapDetails
    {
        public readonly ulong Address;

        public readonly ulong AllocAllocated;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly GenerationData[] GenerationTable;
        public readonly ulong EphemeralHeapSegment;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public readonly ulong[] FinalizationFillPointers;

        public readonly ulong LowestAddress;
        public readonly ulong HighestAddress;
        public readonly ulong CardTable;
    }
}
