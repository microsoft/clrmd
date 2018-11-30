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

#if false
        [DllImport(Kernel32LibraryName, CharSet = CharSet.Unicode, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(ProcessSafeHandle hProcess,
                                                            int dwFlags,
                                                            StringBuilder lpExeName,
                                                            ref int lpdwSize);

        [DllImport(Ole32LibraryName, PreserveSig = false)]
        public static extern void CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter,
                                                   Int32 dwClsContext,
                                                   ref Guid riid, // must use "typeof(ICorDebug).GUID"
                                                   [MarshalAs(UnmanagedType.Interface)]out ICorDebug debuggingInterface
                                                   );

        public enum Stgm
        {
            StgmRead = 0x00000000,
            StgmWrite = 0x00000001,
            StgmReadWrite = 0x00000002,
            StgmShareDenyNone = 0x00000040,
            StgmShareDenyRead = 0x00000030,
            StgmShareDenyWrite = 0x00000020,
            StgmShareExclusive = 0x00000010,
            StgmPriority = 0x00040000,
            StgmCreate = 0x00001000,
            StgmConvert = 0x00020000,
            StgmFailIfThere = 0x00000000,
            StgmDirect = 0x00000000,
            StgmTransacted = 0x00010000,
            StgmNoScratch = 0x00100000,
            StgmNoSnapshot = 0x00200000,
            StgmSimple = 0x08000000,
            StgmDirectSwmr = 0x00400000,
            StgmDeleteOnRelease = 0x04000000
        }

        // SHCreateStreamOnFile* is used to create IStreams to pass to ICLRMetaHostPolicy::GetRequestedRuntime().
        // Since we can't count on the EX version being available, we have SHCreateStreamOnFile as a fallback.
        [DllImport(ShlwapiLibraryName, PreserveSig = false)]
        // Only in version 6 and later
        public static extern void SHCreateStreamOnFileEx([MarshalAs(UnmanagedType.LPWStr)]string file,
                                                        Stgm dwMode,
                                                        Int32 dwAttributes, // Used if a file is created.  Identical to dwFlagsAndAttributes param of CreateFile.
                                                        bool create,
                                                        IntPtr pTemplate,   // Reserved, always pass null.
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

        [DllImport(ShlwapiLibraryName, PreserveSig = false)]
        public static extern void SHCreateStreamOnFile(string file,
                                                        Stgm dwMode,
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

#endif
    }
}