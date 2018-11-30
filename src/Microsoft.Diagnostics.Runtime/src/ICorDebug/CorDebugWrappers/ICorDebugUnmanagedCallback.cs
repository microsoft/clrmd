// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("5263E909-8CB5-11D3-BD2F-0000F80849BD")]
    [InterfaceType(1)]
    public interface ICorDebugUnmanagedCallback
    {
        void DebugEvent([In] IntPtr pDebugEvent, [In] int fOutOfBand);
    }
}