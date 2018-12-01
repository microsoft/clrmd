// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebug
    {
        //
        void Initialize();

        //
        void Terminate();

        //
        void SetManagedHandler(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugManagedCallback pCallback);

        //
        void SetUnmanagedHandler(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugUnmanagedCallback pCallback);

        //
        void CreateProcess(
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
        void DebugActiveProcess(
            [In] uint id,
            [In] int win32Attach,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        //
        void EnumerateProcesses(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcessEnum ppProcess);

        //
        void GetProcess(
            [In] uint dwProcessId,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        //
        void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);
    }
}