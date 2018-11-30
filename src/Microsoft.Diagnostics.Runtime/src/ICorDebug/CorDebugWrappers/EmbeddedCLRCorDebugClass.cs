// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("211F1254-BC7E-4AF5-B9AA-067308D83DD1")]
    [ClassInterface((short)0)]
    public class EmbeddedCLRCorDebugClass : ICorDebug, EmbeddedCLRCorDebug
    {
        // Methods

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void CreateProcess(
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine,
            [In] SECURITY_ATTRIBUTES lpProcessAttributes,
            [In] SECURITY_ATTRIBUTES lpThreadAttributes,
            [In] int bInheritHandles,
            [In] uint dwCreationFlags,
            [In] IntPtr lpEnvironment,
            [In][MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            STARTUPINFO lpStartupInfo,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            PROCESS_INFORMATION lpProcessInformation,
            [In] CorDebugCreateProcessFlags debuggingFlags,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void DebugActiveProcess(
            [In] uint id,
            [In] int win32Attach,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void EnumerateProcesses(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcessEnum ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void GetProcess(
            [In] uint dwProcessId,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void SetManagedHandler(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugManagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void SetUnmanagedHandler(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugUnmanagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Terminate();
    }
}