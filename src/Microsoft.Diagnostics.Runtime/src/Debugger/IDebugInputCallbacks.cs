// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("9f50e42c-f136-499e-9a97-73036c94ed2d")]
    public interface IDebugInputCallbacks
    {
        [PreserveSig]
        int StartInput(
            [In] uint BufferSize);

        [PreserveSig]
        int EndInput();
    }
}