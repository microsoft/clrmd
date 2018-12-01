// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("5D88A994-6C30-479B-890F-BCEF88B129A5")]
    public interface ICorDebugILFrame2
    {
        void RemapFunction([In] uint newILOffset);

        void EnumerateTypeParameters(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugTypeEnum ppTyParEnum);
    }
}