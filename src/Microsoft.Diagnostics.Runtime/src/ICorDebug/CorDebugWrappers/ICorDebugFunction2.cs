// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("EF0C490B-94C3-4E4D-B629-DDC134C532D8")]
    public interface ICorDebugFunction2
    {
        void SetJMCStatus([In] int bIsJustMyCode);

        void GetJMCStatus([Out] out int pbIsJustMyCode);

        void EnumerateNativeCode(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugCodeEnum ppCodeEnum);

        void GetVersionNumber([Out] out uint pnVersion);
    }
}