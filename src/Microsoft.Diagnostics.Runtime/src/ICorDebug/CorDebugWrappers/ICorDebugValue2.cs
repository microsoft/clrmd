// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("5E0B54E7-D88A-4626-9420-A691E0A78B49")]
    [InterfaceType(1)]
    public interface ICorDebugValue2
    {
        void GetExactType(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType ppType);
    }
}