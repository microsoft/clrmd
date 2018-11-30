// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("AD1B3588-0EF0-4744-A496-AA09A9F80371")]
    [InterfaceType(1)]
    [ComConversionLoss]
    public interface ICorDebugProcess2
    {
        void GetThreadForTaskID(
            [In] ulong taskid,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThread2 ppThread);

        void GetVersion([Out] out _COR_VERSION version);

        void SetUnmanagedBreakpoint(
            [In] ulong address,
            [In] uint bufsize,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            byte[] buffer,
            [Out] out uint bufLen);

        void ClearUnmanagedBreakpoint([In] ulong address);

        void SetDesiredNGENCompilerFlags([In] uint pdwFlags);

        void GetDesiredNGENCompilerFlags([Out] out uint pdwFlags);

        void GetReferenceValueFromGCHandle(
            [In] IntPtr handle,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugReferenceValue pOutValue);
    }
}