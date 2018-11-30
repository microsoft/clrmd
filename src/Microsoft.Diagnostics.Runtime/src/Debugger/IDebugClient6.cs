// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("e3acb9d7-7ec2-4f0c-a0da-e81e0cbbe628")]
    public interface IDebugClient6 : IDebugClient5
    {
        /* IDebugClient */

        [PreserveSig]
        new int AttachKernel(
            [In] DEBUG_ATTACH Flags,
            [In][MarshalAs(UnmanagedType.LPStr)] string ConnectOptions);

        [PreserveSig]
        new int GetKernelConnectionOptions(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint OptionsSize);

        [PreserveSig]
        new int SetKernelConnectionOptions(
            [In][MarshalAs(UnmanagedType.LPStr)] string Options);

        [PreserveSig]
        new int StartProcessServer(
            [In] DEBUG_CLASS Flags,
            [In][MarshalAs(UnmanagedType.LPStr)] string Options,
            [In] IntPtr Reserved);

        [PreserveSig]
        new int ConnectProcessServer(
            [In][MarshalAs(UnmanagedType.LPStr)] string RemoteOptions,
            [Out] out ulong Server);

        [PreserveSig]
        new int DisconnectProcessServer(
            [In] ulong Server);

        [PreserveSig]
        new int GetRunningProcessSystemIds(
            [In] ulong Server,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids,
            [In] uint Count,
            [Out] out uint ActualCount);

        [PreserveSig]
        new int GetRunningProcessSystemIdByExecutableName(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPStr)] string ExeName,
            [In] DEBUG_GET_PROC Flags,
            [Out] out uint Id);

        [PreserveSig]
        new int GetRunningProcessDescription(
            [In] ulong Server,
            [In] uint SystemId,
            [In] DEBUG_PROC_DESC Flags,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder ExeName,
            [In] int ExeNameSize,
            [Out] out uint ActualExeNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] int DescriptionSize,
            [Out] out uint ActualDescriptionSize);

        [PreserveSig]
        new int AttachProcess(
            [In] ulong Server,
            [In] uint ProcessID,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int CreateProcess(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS Flags);

        [PreserveSig]
        new int CreateProcessAndAttach(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS Flags,
            [In] uint ProcessId,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int GetProcessOptions(
            [Out] out DEBUG_PROCESS Options);

        [PreserveSig]
        new int AddProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int RemoveProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int SetProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int OpenDumpFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string DumpFile);

        [PreserveSig]
        new int WriteDumpFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string DumpFile,
            [In] DEBUG_DUMP Qualifier);

        [PreserveSig]
        new int ConnectSession(
            [In] DEBUG_CONNECT_SESSION Flags,
            [In] uint HistoryLimit);

        [PreserveSig]
        new int StartServer(
            [In][MarshalAs(UnmanagedType.LPStr)] string Options);

        [PreserveSig]
        new int OutputServer(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPStr)] string Machine,
            [In] DEBUG_SERVERS Flags);

        [PreserveSig]
        new int TerminateProcesses();

        [PreserveSig]
        new int DetachProcesses();

        [PreserveSig]
        new int EndSession(
            [In] DEBUG_END Flags);

        [PreserveSig]
        new int GetExitCode(
            [Out] out uint Code);

        [PreserveSig]
        new int DispatchCallbacks(
            [In] uint Timeout);

        [PreserveSig]
        new int ExitDispatch(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugClient Client);

        [PreserveSig]
        new int CreateClient(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugClient Client);

        [PreserveSig]
        new int GetInputCallbacks(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugInputCallbacks Callbacks);

        [PreserveSig]
        new int SetInputCallbacks(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugInputCallbacks Callbacks);

        /* GetOutputCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetOutputCallbacks(
            [Out] out IDebugOutputCallbacks Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetOutputCallbacks(
            [In] IDebugOutputCallbacks Callbacks);

        [PreserveSig]
        new int GetOutputMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetOutputMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int GetOtherOutputMask(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugClient Client,
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetOtherOutputMask(
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugClient Client,
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int GetOutputWidth(
            [Out] out uint Columns);

        [PreserveSig]
        new int SetOutputWidth(
            [In] uint Columns);

        [PreserveSig]
        new int GetOutputLinePrefix(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PrefixSize);

        [PreserveSig]
        new int SetOutputLinePrefix(
            [In][MarshalAs(UnmanagedType.LPStr)] string Prefix);

        [PreserveSig]
        new int GetIdentity(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint IdentitySize);

        [PreserveSig]
        new int OutputIdentity(
            [In] DEBUG_OUTCTL OutputControl,
            [In] uint Flags,
            [In][MarshalAs(UnmanagedType.LPStr)] string Format);

        /* GetEventCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetEventCallbacks(
            [Out] out IDebugEventCallbacks Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetEventCallbacks(
            [In] IDebugEventCallbacks Callbacks);

        [PreserveSig]
        new int FlushCallbacks();

        /* IDebugClient2 */

        [PreserveSig]
        new int WriteDumpFile2(
            [In][MarshalAs(UnmanagedType.LPStr)] string DumpFile,
            [In] DEBUG_DUMP Qualifier,
            [In] DEBUG_FORMAT FormatFlags,
            [In][MarshalAs(UnmanagedType.LPStr)] string Comment);

        [PreserveSig]
        new int AddDumpInformationFile(
            [In][MarshalAs(UnmanagedType.LPStr)] string InfoFile,
            [In] DEBUG_DUMP_FILE Type);

        [PreserveSig]
        new int EndProcessServer(
            [In] ulong Server);

        [PreserveSig]
        new int WaitForProcessServerEnd(
            [In] uint Timeout);

        [PreserveSig]
        new int IsKernelDebuggerEnabled();

        [PreserveSig]
        new int TerminateCurrentProcess();

        [PreserveSig]
        new int DetachCurrentProcess();

        [PreserveSig]
        new int AbandonCurrentProcess();

        /* IDebugClient3 */

        [PreserveSig]
        new int GetRunningProcessSystemIdByExecutableNameWide(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ExeName,
            [In] DEBUG_GET_PROC Flags,
            [Out] out uint Id);

        [PreserveSig]
        new int GetRunningProcessDescriptionWide(
            [In] ulong Server,
            [In] uint SystemId,
            [In] DEBUG_PROC_DESC Flags,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder ExeName,
            [In] int ExeNameSize,
            [Out] out uint ActualExeNameSize,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Description,
            [In] int DescriptionSize,
            [Out] out uint ActualDescriptionSize);

        [PreserveSig]
        new int CreateProcessWide(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPWStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS CreateFlags);

        [PreserveSig]
        new int CreateProcessAndAttachWide(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPWStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS CreateFlags,
            [In] uint ProcessId,
            [In] DEBUG_ATTACH AttachFlags);

        /* IDebugClient4 */

        [PreserveSig]
        new int OpenDumpFileWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string FileName,
            [In] ulong FileHandle);

        [PreserveSig]
        new int WriteDumpFileWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string DumpFile,
            [In] ulong FileHandle,
            [In] DEBUG_DUMP Qualifier,
            [In] DEBUG_FORMAT FormatFlags,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Comment);

        [PreserveSig]
        new int AddDumpInformationFileWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string FileName,
            [In] ulong FileHandle,
            [In] DEBUG_DUMP_FILE Type);

        [PreserveSig]
        new int GetNumberDumpFiles(
            [Out] out uint Number);

        [PreserveSig]
        new int GetDumpFile(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Handle,
            [Out] out uint Type);

        [PreserveSig]
        new int GetDumpFileWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Handle,
            [Out] out uint Type);

        /* IDebugClient5 */

        [PreserveSig]
        new int AttachKernelWide(
            [In] DEBUG_ATTACH Flags,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ConnectOptions);

        [PreserveSig]
        new int GetKernelConnectionOptionsWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint OptionsSize);

        [PreserveSig]
        new int SetKernelConnectionOptionsWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Options);

        [PreserveSig]
        new int StartProcessServerWide(
            [In] DEBUG_CLASS Flags,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Options,
            [In] IntPtr Reserved);

        [PreserveSig]
        new int ConnectProcessServerWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string RemoteOptions,
            [Out] out ulong Server);

        [PreserveSig]
        new int StartServerWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Options);

        [PreserveSig]
        new int OutputServersWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Machine,
            [In] DEBUG_SERVERS Flags);

        /* GetOutputCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetOutputCallbacksWide(
            [Out] out IDebugOutputCallbacksWide Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetOutputCallbacksWide(
            [In] IDebugOutputCallbacksWide Callbacks);

        [PreserveSig]
        new int GetOutputLinePrefixWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PrefixSize);

        [PreserveSig]
        new int SetOutputLinePrefixWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Prefix);

        [PreserveSig]
        new int GetIdentityWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint IdentitySize);

        [PreserveSig]
        new int OutputIdentityWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In] uint Flags,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Machine);

        /* GetEventCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetEventCallbacksWide(
            [Out] out IDebugEventCallbacksWide Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetEventCallbacksWide(
            [In] IDebugEventCallbacksWide Callbacks);

        [PreserveSig]
        new int CreateProcess2(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] ref DEBUG_CREATE_PROCESS_OPTIONS OptionsBuffer,
            [In] uint OptionsBufferSize,
            [In][MarshalAs(UnmanagedType.LPStr)] string InitialDirectory,
            [In][MarshalAs(UnmanagedType.LPStr)] string Environment);

        [PreserveSig]
        new int CreateProcess2Wide(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPWStr)] string CommandLine,
            [In] ref DEBUG_CREATE_PROCESS_OPTIONS OptionsBuffer,
            [In] uint OptionsBufferSize,
            [In][MarshalAs(UnmanagedType.LPWStr)] string InitialDirectory,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Environment);

        [PreserveSig]
        new int CreateProcessAndAttach2(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] ref DEBUG_CREATE_PROCESS_OPTIONS OptionsBuffer,
            [In] uint OptionsBufferSize,
            [In][MarshalAs(UnmanagedType.LPStr)] string InitialDirectory,
            [In][MarshalAs(UnmanagedType.LPStr)] string Environment,
            [In] uint ProcessId,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int CreateProcessAndAttach2Wide(
            [In] ulong Server,
            [In][MarshalAs(UnmanagedType.LPWStr)] string CommandLine,
            [In] ref DEBUG_CREATE_PROCESS_OPTIONS OptionsBuffer,
            [In] uint OptionsBufferSize,
            [In][MarshalAs(UnmanagedType.LPWStr)] string InitialDirectory,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Environment,
            [In] uint ProcessId,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int PushOutputLinePrefix(
            [In][MarshalAs(UnmanagedType.LPStr)] string NewPrefix,
            [Out] out ulong Handle);

        [PreserveSig]
        new int PushOutputLinePrefixWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string NewPrefix,
            [Out] out ulong Handle);

        [PreserveSig]
        new int PopOutputLinePrefix(
            [In] ulong Handle);

        [PreserveSig]
        new int GetNumberInputCallbacks(
            [Out] out uint Count);

        [PreserveSig]
        new int GetNumberOutputCallbacks(
            [Out] out uint Count);

        [PreserveSig]
        new int GetNumberEventCallbacks(
            [In] DEBUG_EVENT Flags,
            [Out] out uint Count);

        [PreserveSig]
        new int GetQuitLockString(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize);

        [PreserveSig]
        new int SetQuitLockString(
            [In][MarshalAs(UnmanagedType.LPStr)] string LockString);

        [PreserveSig]
        new int GetQuitLockStringWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize);

        [PreserveSig]
        new int SetQuitLockStringWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string LockString);

        /* IDebugClient6 */

        [PreserveSig]
        int SetEventContextCallbacks(
            [In] IDebugEventContextCallbacks Callbacks);
    }
}