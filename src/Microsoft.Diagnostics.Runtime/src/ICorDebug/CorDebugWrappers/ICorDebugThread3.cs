// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("F8544EC3-5E4E-46c7-8D3E-A52B8405B1F5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugThread3
    {
        void CreateStackWalk(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugStackWalk ppStackWalk);

        void GetActiveInternalFrames(
            [In] uint cInternalFrames,
            [Out] out uint pcInternalFrames,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugInternalFrame2[] ppFunctions);
    }
}