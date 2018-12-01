// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [InterfaceType(1)]
    [Guid("F0E18809-72B5-11D2-976F-00A0C9B4D50C")]
    public interface ICorDebugErrorInfoEnum : ICorDebugEnum
    {
        new void Skip([In] uint celt);
        new void Reset();

        new void Clone(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next(
            [In] uint celt,
            [Out][ComAliasName("ICorDebugEditAndContinueErrorInfo**")]
            IntPtr errors,
            [Out] out uint pceltFetched);
    }
}