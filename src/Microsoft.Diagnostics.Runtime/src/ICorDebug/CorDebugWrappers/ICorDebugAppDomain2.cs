// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("096E81D5-ECDA-4202-83F5-C65980A9EF75")]
    public interface ICorDebugAppDomain2
    {
        void GetArrayOrPointerType(
            [In] CorElementType elementType,
            [In] uint nRank,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugType pTypeArg,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType ppType);

        void GetFunctionPointerType(
            [In] uint nTypeArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            ICorDebugType[] ppTypeArgs,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType ppType);
    }
}