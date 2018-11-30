// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("61a4905b-23f9-4247-b3c5-53d087529ab7")]
    public unsafe interface IDebugEventContextCallbacks
    {
        [PreserveSig]
        int GetInterestMask(
            [Out] out DEBUG_EVENT Mask);

        [PreserveSig]
        int Breakpoint(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugBreakpoint2 Bp,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int Exception(
            [In] ref EXCEPTION_RECORD64 Exception,
            [In] uint FirstChance,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int CreateThread(
            [In] ulong Handle,
            [In] ulong DataOffset,
            [In] ulong StartOffset,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int ExitThread(
            [In] uint ExitCode,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int CreateProcess(
            [In] ulong ImageFileHandle,
            [In] ulong Handle,
            [In] ulong BaseOffset,
            [In] uint ModuleSize,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ImageName,
            [In] uint CheckSum,
            [In] uint TimeDateStamp,
            [In] ulong InitialThreadHandle,
            [In] ulong ThreadDataOffset,
            [In] ulong StartOffset,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int ExitProcess(
            [In] uint ExitCode,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int LoadModule(
            [In] ulong ImageFileHandle,
            [In] ulong BaseOffset,
            [In] uint ModuleSize,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ImageName,
            [In] uint CheckSum,
            [In] uint TimeDateStamp,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int UnloadModule(
            [In][MarshalAs(UnmanagedType.LPWStr)] string ImageBaseName,
            [In] ulong BaseOffset,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int SystemError(
            [In] uint Error,
            [In] uint Level,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int SessionStatus(
            [In] DEBUG_SESSION Status);

        [PreserveSig]
        int ChangeDebuggeeState(
            [In] DEBUG_CDS Flags,
            [In] ulong Argument,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int ChangeEngineState(
            [In] DEBUG_CES Flags,
            [In] ulong Argument,
            [In] DEBUG_EVENT_CONTEXT* Context,
            [In] uint ContextSize);

        [PreserveSig]
        int ChangeSymbolState(
            [In] DEBUG_CSS Flags,
            [In] ulong Argument);
    }
}