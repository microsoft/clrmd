// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("976A6278-134A-4a81-81A3-8F277943F4C3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugEnumBlockingObject : ICorDebugEnum
    {
        new void Skip([In] uint countElements);
        new void Reset();

        new void Clone(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugEnum enumerator);

        new void GetCount([Out] out uint countElements);

        [PreserveSig]
        int Next(
            [In] uint countElements,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            CorDebugBlockingObject[] blockingObjects,
            [Out] out uint countElementsFetched);
    }
}