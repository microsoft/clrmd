// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    internal static class NativeMethods
    {
        private const string Kernel32LibraryName = "kernel32.dll";
        private const string Ole32LibraryName = "ole32.dll";
        private const string ShlwapiLibraryName = "shlwapi.dll";
        private const string ShimLibraryName = "mscoree.dll";

        public const int MAX_PATH = 260;

        [DllImport(Kernel32LibraryName)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern ICorDebug CreateDebuggingInterfaceFromVersion(int iDebuggerVersion, string szDebuggeeVersion);

        [DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void GetVersionFromProcess(
            ProcessSafeHandle hProcess,
            StringBuilder versionString,
            int bufferSize,
            out int dwLength);

        [DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void GetRequestedRuntimeVersion(
            string pExe,
            StringBuilder pVersion,
            int cchBuffer,
            out int dwLength);

        [DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void CLRCreateInstance(
            ref Guid clsid,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object metahostInterface);

        public enum ProcessAccessOptions
        {
            ProcessTerminate = 0x0001,
            ProcessCreateThread = 0x0002,
            ProcessSetSessionID = 0x0004,
            ProcessVMOperation = 0x0008,
            ProcessVMRead = 0x0010,
            ProcessVMWrite = 0x0020,
            ProcessDupHandle = 0x0040,
            ProcessCreateProcess = 0x0080,
            ProcessSetQuota = 0x0100,
            ProcessSetInformation = 0x0200,
            ProcessQueryInformation = 0x0400,
            ProcessSuspendResume = 0x0800,
            Synchronize = 0x100000
        }

        [DllImport(Kernel32LibraryName, PreserveSig = true)]
        public static extern ProcessSafeHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    }
}