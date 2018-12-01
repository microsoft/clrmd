// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("E3AC4D6C-9CB7-43E6-96CC-B21540E5083C")]
    public interface ICorDebugHeapValue2
    {
        void CreateHandle(
            [In] CorDebugHandleType type,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugHandleValue ppHandle);
    }
}