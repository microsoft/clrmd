// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

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
            out uint Id);

        [PreserveSig]
        new int GetEventProcess(
            out uint Id);

        [PreserveSig]
        new int GetCurrentThreadId(
            out uint Id);

        [PreserveSig]
        new int SetCurrentThreadId(
            [In] uint Id);

        [PreserveSig]
        new int GetCurrentProcessId(
            out uint Id);

        [PreserveSig]
        new int SetCurrentProcessId(
            [In] uint Id);

        [PreserveSig]
        new int GetNumberThreads(
            out uint Number);

        [PreserveSig]
        new int GetTotalNumberThreads(
            out uint Total,
            out uint LargestProcess);

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
            out uint Id);

        [PreserveSig]
        new int GetCurrentThreadDataOffset(
            out ulong Offset);

        [PreserveSig]
        new int GetThreadIdByDataOffset(
            [In] ulong Offset,
            out uint Id);

        [PreserveSig]
        new int GetCurrentThreadTeb(
            out ulong Offset);

        [PreserveSig]
        new int GetThreadIdByTeb(
            [In] ulong Offset,
            out uint Id);

        [PreserveSig]
        new int GetCurrentThreadSystemId(
            out uint SysId);

        [PreserveSig]
        new int GetThreadIdBySystemId(
            [In] uint SysId,
            out uint Id);

        [PreserveSig]
        new int GetCurrentThreadHandle(
            out ulong Handle);

        [PreserveSig]
        new int GetThreadIdByHandle(
            [In] ulong Handle,
            out uint Id);

        [PreserveSig]
        new int GetNumberProcesses(
            out uint Number);

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
            out ulong Offset);

        [PreserveSig]
        new int GetProcessIdByDataOffset(
            [In] ulong Offset,
            out uint Id);

        [PreserveSig]
        new int GetCurrentProcessPeb(
            out ulong Offset);

        [PreserveSig]
        new int GetProcessIdByPeb(
            [In] ulong Offset,
            out uint Id);

        [PreserveSig]
        new int GetCurrentProcessSystemId(
            out uint SysId);

        [PreserveSig]
        new int GetProcessIdBySystemId(
            [In] uint SysId,
            out uint Id);

        [PreserveSig]
        new int GetCurrentProcessHandle(
            out ulong Handle);

        [PreserveSig]
        new int GetProcessIdByHandle(
            [In] ulong Handle,
            out uint Id);

        [PreserveSig]
        new int GetCurrentProcessExecutableName(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            out uint ExeSize);

        /* IDebugSystemObjects2 */

        [PreserveSig]
        int GetCurrentProcessUpTime(
            out uint UpTime);

        [PreserveSig]
        int GetImplicitThreadDataOffset(
            out ulong Offset);

        [PreserveSig]
        int SetImplicitThreadDataOffset(
            [In] ulong Offset);

        [PreserveSig]
        int GetImplicitProcessDataOffset(
            out ulong Offset);

        [PreserveSig]
        int SetImplicitProcessDataOffset(
            [In] ulong Offset);

        /* IDebugSystemObjects3 */
        [PreserveSig]
        int GetEventSystem(out uint id);

        [PreserveSig]
        int GetCurrentSystemId(out uint id);

        [PreserveSig]
        int SetCurrentSystemId([In] uint id);

        [PreserveSig]
        int GetNumberSystems(out uint count);

        [PreserveSig]
        int GetSystemIdsByIndex(
            [In] uint start,
            [In] uint count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            uint[] Ids);

        [PreserveSig]
        int GetTotalNumberThreadsAndProcesses(
            out uint totalThreads,
            out uint totalProcesses,
            out uint largestProcessThreads,
            out uint largestSystemThreads,
            out uint largestSystemProcesses);

        [PreserveSig]
        int GetCurrentSystemServer(out ulong server);

        [PreserveSig]
        int GetSystemByServer([In] ulong server, out uint id);

        [PreserveSig]
        int GetCurrentSystemServerName([Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder buffer, [In] uint size, out uint needed);
    }
}