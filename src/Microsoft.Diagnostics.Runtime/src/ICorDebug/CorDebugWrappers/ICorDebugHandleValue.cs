// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("029596E8-276B-46A1-9821-732E96BBB00B")]
    [InterfaceType(1)]
    public interface ICorDebugHandleValue : ICorDebugReferenceValue
    {
        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValueBreakpoint ppBreakpoint);

        new void IsNull([Out] out int pbNull);

        new void GetValue([Out] out ulong pValue);

        new void SetValue([In] ulong value);

        new void Dereference(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        new void DereferenceStrong(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetHandleType([Out] out CorDebugHandleType pType);

        void Dispose();
    }
}