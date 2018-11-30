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
    [Guid("e9676e2f-e286-4ea3-b0f9-dfe5d9fc330e")]
    public interface IDebugSystemObjects3 : IDebugSystemObjects
    {
        /* IDebugSystemObjects */

        [PreserveSig]
        new int GetEventThread(
            [Out] out uint Id);

        [PreserveSig]
        new int GetEventProcess(
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentThreadId(
            [Out] out uint Id);

        [PreserveSig]
        new int SetCurrentThreadId(
            [In] uint Id);

        [PreserveSig]
        new int GetCurrentProcessId(
            [Out] out uint Id);

        [PreserveSig]
        new int SetCurrentProcessId(
            [In] uint Id);

        [PreserveSig]
        new int GetNumberThreads(
            [Out] out uint Number);

        [PreserveSig]
        new int GetTotalNumberThreads(
            [Out] out uint Total,
            [Out] out uint LargestProcess);

        [PreserveSig]
        new int GetThreadIdsByIndex(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] SysIds);

        [PreserveSig]
        new int GetThreadIdByProcessor(
            [In] uint Processor,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentThreadDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetThreadIdByDataOffset(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentThreadTeb(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetThreadIdByTeb(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentThreadSystemId(
            [Out] out uint SysId);

        [PreserveSig]
        new int GetThreadIdBySystemId(
            [In] uint SysId,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentThreadHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        new int GetThreadIdByHandle(
            [In] ulong Handle,
            [Out] out uint Id);

        [PreserveSig]
        new int GetNumberProcesses(
            [Out] out uint Number);

        [PreserveSig]
        new int GetProcessIdsByIndex(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] SysIds);

        [PreserveSig]
        new int GetCurrentProcessDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetProcessIdByDataOffset(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentProcessPeb(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetProcessIdByPeb(
            [In] ulong Offset,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentProcessSystemId(
            [Out] out uint SysId);

        [PreserveSig]
        new int GetProcessIdBySystemId(
            [In] uint SysId,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentProcessHandle(
            [Out] out ulong Handle);

        [PreserveSig]
        new int GetProcessIdByHandle(
            [In] ulong Handle,
            [Out] out uint Id);

        [PreserveSig]
        new int GetCurrentProcessExecutableName(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ExeSize);

        /* IDebugSystemObjects2 */

        [PreserveSig]
        int GetCurrentProcessUpTime(
            [Out] out uint UpTime);

        [PreserveSig]
        int GetImplicitThreadDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int SetImplicitThreadDataOffset(
            [In] ulong Offset);

        [PreserveSig]
        int GetImplicitProcessDataOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int SetImplicitProcessDataOffset(
            [In] ulong Offset);

        /* IDebugSystemObjects3 */
        [PreserveSig]
        int GetEventSystem([Out] out uint id);

        [PreserveSig]
        int GetCurrentSystemId([Out] out uint id);

        [PreserveSig]
        int SetCurrentSystemId([In] uint id);

        [PreserveSig]
        int GetNumberSystems([Out] out uint count);

        [PreserveSig]
        int GetSystemIdsByIndex(
            [In] uint start,
            [In] uint count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids);

        [PreserveSig]
        int GetTotalNumberThreadsAndProcesses(
            [Out] out uint totalThreads,
            [Out] out uint totalProcesses,
            [Out] out uint largestProcessThreads,
            [Out] out uint largestSystemThreads,
            [Out] out uint largestSystemProcesses);

        [PreserveSig]
        int GetCurrentSystemServer([Out] out ulong server);

        [PreserveSig]
        int GetSystemByServer([In] ulong server, [Out] out uint id);

        [PreserveSig]
        int GetCurrentSystemServerName([Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder buffer, [In] uint size, [Out] out uint needed);
    }
}