// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("D5EBB8E2-7BBE-4c1d-98A6-A3C04CBDEF64")]
    [InterfaceType(1)]
    public interface ICorDebugRemote
    {
        //
        void CreateProcessEx(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugRemoteTarget pRemoteTarget,
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine,
            [In] SECURITY_ATTRIBUTES lpProcessAttributes,
            [In] SECURITY_ATTRIBUTES lpThreadAttributes,
            [In] int bInheritHandles,
            [In] uint dwCreationFlags,
            [In] IntPtr lpEnvironment,
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory,
            [In] STARTUPINFO lpStartupInfo,
            [In] PROCESS_INFORMATION lpProcessInformation,
            [In] CorDebugCreateProcessFlags debuggingFlags,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        //
        void DebugActiveProcessEx(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugRemoteTarget pRemoteTarget,
            [In] uint id,
            [In] int win32Attach,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);
    }
}