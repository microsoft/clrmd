// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("FE06DC28-49FB-4636-A4A3-E80DB4AE116C")]
    public interface ICorDebugDataTarget
    {
        CorDebugPlatform GetPlatform();

        uint ReadVirtual(
            ulong address,
            IntPtr buffer,
            uint bytesRequested);

        void GetThreadContext(
            uint threadId,
            uint contextFlags,
            uint contextSize,
            IntPtr context);
    }
}