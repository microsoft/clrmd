// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [Guid("CC7BCB02-8A68-11D2-983C-0000F808342D")]
    [InterfaceType(1)]
    public interface ICorDebugObjectEnum : ICorDebugEnum
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
            ulong[] objects,
            [Out] out uint pceltFetched);
    }
}