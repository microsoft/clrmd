// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("B008EA8D-7AB1-43F7-BB20-FBB5A04038AE")]
    [InterfaceType(1)]
    public interface ICorDebugClass2
    {
        void GetParameterizedType(
            [In] CorElementType elementType,
            [In] uint nTypeArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugType[] ppTypeArgs,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType ppType);

        void SetJMCStatus([In] int bIsJustMyCode);
    }
}