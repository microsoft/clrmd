// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("10F27499-9DF2-43CE-8333-A321D7C99CB4")]
    [InterfaceType(1)]
    [ComConversionLoss]
    public interface ICorDebugTypeEnum : ICorDebugEnum
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
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ICorDebugType[] values,
            [Out] out uint pceltFetched);
    }
}