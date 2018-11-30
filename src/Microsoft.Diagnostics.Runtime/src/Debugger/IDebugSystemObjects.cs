// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6b86fe2c-2c4f-4f0c-9da2-174311acc327")]
    public interface IDebugSystemObjects
    {
        [PreserveSig]
        int GetEventThread(
            [Out] out uint Id);

        [PreserveSig]
        int GetEventProcess(
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentThreadId(
            [Out] out uint Id);

        [PreserveSig]
        int SetCurrentThreadId(
            [In] uint Id);

        [PreserveSig]
        int GetCurrentProcessId(
            [Out] out uint Id);

        [PreserveSig]
        int SetCurrentProcessId(
            [In] uint Id);

        [PreserveSig]
        int GetNumberThreads(
            [Out] out uint Number);

        [PreserveSig]
        int GetTotalNumberThreads(
            [Out] out uint Total,
            [Out] out uint LargestProcess);

        [PreserveSig]
        int GetThreadIdsByIndex(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] SysIds);

        [PreserveSig]
        int GetThreadIdByProcessor(
            [In] uint Processor,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentThreadDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int GetThreadIdByDataOffset(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentThreadTeb(
            [Out] out ulong Offset);

        [PreserveSig]
        int GetThreadIdByTeb(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentThreadSystemId(
            [Out] out uint SysId);

        [PreserveSig]
        int GetThreadIdBySystemId(
            [In] uint SysId,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentThreadHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        int GetThreadIdByHandle(
            [In] ulong Handle,
            [Out] out uint Id);

        [PreserveSig]
        int GetNumberProcesses(
            [Out] out uint Number);

        [PreserveSig]
        int GetProcessIdsByIndex(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] SysIds);

        [PreserveSig]
        int GetCurrentProcessDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int GetProcessIdByDataOffset(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentProcessPeb(
            [Out] out ulong Offset);

        [PreserveSig]
        int GetProcessIdByPeb(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentProcessSystemId(
            [Out] out uint SysId);

        [PreserveSig]
        int GetProcessIdBySystemId(
            [In] uint SysId,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentProcessHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        int GetProcessIdByHandle(
            [In] ulong Handle,
            [Out] out uint Id);

        [PreserveSig]
        int GetCurrentProcessExecutableName(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ExeSize);
    }
}