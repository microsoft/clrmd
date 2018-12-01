// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [InterfaceType(1)]
    [Guid("21e9d9c0-fcb8-11df-8cff-0800200c9a66")]
    public interface ICorDebugProcess5
    {
        void GetGCHeapInformation([Out] out COR_HEAPINFO pHeapInfo);

        void EnumerateHeap(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugHeapEnum ppObjects);

        void EnumerateHeapRegions(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugHeapSegmentEnum ppRegions);

        void GetObject(
            [In] ulong addr,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugObjectValue ppObject);

        void EnumerateGCReferences(
            [In] int bEnumerateWeakReferences,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugGCReferenceEnum ppEnum);

        void EnumerateHandles(
            [In] uint types,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugGCReferenceEnum ppEnum);

        // This is used for fast Heap dumping.  You have to keep track of field layout but you can bulk copy everything. 
        void GetTypeID([In] ulong objAddr, [Out] out COR_TYPEID pId);
        void GetTypeForTypeID([In] COR_TYPEID id, [Out] out ICorDebugType type);
        void GetArrayLayout([In] COR_TYPEID id, [Out] out COR_ARRAY_LAYOUT layout);
        void GetTypeLayout([In] COR_TYPEID id, [Out] out COR_TYPE_LAYOUT layout);

        void GetTypeFields(
            [In] COR_TYPEID id,
            int celt,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            COR_FIELD[] fields,
            [Out] out int pceltNeeded);

        void EnableNGENPolicy(CorDebugNGENPolicyFlags ePolicy);
    }
}