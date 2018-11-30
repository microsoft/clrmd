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
    [Guid("5bd9d474-5975-423a-b88b-65a8e7110e65")]
    public interface IDebugBreakpoint
    {
        /* IDebugBreakpoint */

        [PreserveSig]
        int GetId(
            [Out] out uint Id);

        [PreserveSig]
        int GetType(
            [Out] out DEBUG_BREAKPOINT_TYPE BreakType,
            [Out] out uint ProcType);

        //FIX ME!!! Should try and get an enum for this
        [PreserveSig]
        int GetAdder(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugClient Adder);

        [PreserveSig]
        int GetFlags(
            [Out] out DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int AddFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int RemoveFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int SetFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int GetOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        int SetOffset(
            [In] ulong Offset);

        [PreserveSig]
        int GetDataParameters(
            [Out] out uint Size,
            [Out] out DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        int SetDataParameters(
            [In] uint Size,
            [In] DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        int GetPassCount(
            [Out] out uint Count);

        [PreserveSig]
        int SetPassCount(
            [In] uint Count);

        [PreserveSig]
        int GetCurrentPassCount(
            [Out] out uint Count);

        [PreserveSig]
        int GetMatchThreadId(
            [Out] out uint Id);

        [PreserveSig]
        int SetMatchThreadId(
            [In] uint Thread);

        [PreserveSig]
        int GetCommand(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        int SetCommand(
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int GetOffsetExpression(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ExpressionSize);

        [PreserveSig]
        int SetOffsetExpression(
            [In][MarshalAs(UnmanagedType.LPStr)] string Expression);

        [PreserveSig]
        int GetParameters(
            [Out] out DEBUG_BREAKPOINT_PARAMETERS Params);
    }
}