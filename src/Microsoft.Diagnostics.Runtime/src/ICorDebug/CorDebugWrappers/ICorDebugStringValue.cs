// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("CC7BCAFD-8A68-11D2-983C-0000F808342D")]
    [ComConversionLoss]
    [InterfaceType(1)]
    public interface ICorDebugStringValue : ICorDebugHeapValue
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

        void GetLength([Out] out uint pcchString);

        void GetString([In] uint cchString, [Out] out uint pcchString, [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder szString);
    }
}