// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("49E4A320-4A9B-4ECA-B105-229FB7D5009F")]
    [InterfaceType(1)]
    public interface ICorDebugObjectValue2
    {
        void GetVirtualMethodAndType(
            [In] uint memberRef,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFunction ppFunction,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType ppType);
    }
}