// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [Guid("0405B0DF-A660-11D2-BD02-0000F80849BD")]
    [InterfaceType(1)]
    public interface ICorDebugArrayValue : ICorDebugHeapValue
    {
        new void GetType([Out] out CorElementType pType);
        new void GetSize([Out] out uint pSize);
        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValueBreakpoint ppBreakpoint);

        new void IsValid([Out] out int pbValid);

        new void CreateRelocBreakpoint(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValueBreakpoint ppBreakpoint);

        void GetElementType([Out] out CorElementType pType);
        void GetRank([Out] out uint pnRank);
        void GetCount([Out] out uint pnCount);

        void GetDimensions(
            [In] uint cdim,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            uint[] dims);

        void HasBaseIndicies([Out] out int pbHasBaseIndicies);

        void GetBaseIndicies(
            [In] uint cdim,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            uint[] indicies);

        void GetElement(
            [In] uint cdim,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            int[] indices,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetElementAtPosition(
            [In] uint nPosition,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);
    }
}