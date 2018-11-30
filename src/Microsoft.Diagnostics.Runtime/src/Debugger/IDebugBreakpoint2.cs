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
    [Guid("1b278d20-79f2-426e-a3f9-c1ddf375d48e")]
    public interface IDebugBreakpoint2 : IDebugBreakpoint
    {
        /* IDebugBreakpoint */

        [PreserveSig]
        new int GetId(
            [Out] out uint Id);

        [PreserveSig]
        new int GetType(
            [Out] out DEBUG_BREAKPOINT_TYPE BreakType,
            [Out] out uint ProcType);

        //FIX ME!!! Should try and get an enum for this
        [PreserveSig]
        new int GetAdder(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugClient Adder);

        [PreserveSig]
        new int GetFlags(
            [Out] out DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int AddFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int RemoveFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int SetFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int GetOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int SetOffset(
            [In] ulong Offset);

        [PreserveSig]
        new int GetDataParameters(
            [Out] out uint Size,
            [Out] out DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        new int SetDataParameters(
            [In] uint Size,
            [In] DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        new int GetPassCount(
            [Out] out uint Count);

        [PreserveSig]
        new int SetPassCount(
            [In] uint Count);

        [PreserveSig]
        new int GetCurrentPassCount(
            [Out] out uint Count);

        [PreserveSig]
        new int GetMatchThreadId(
            [Out] out uint Id);

        [PreserveSig]
        new int SetMatchThreadId(
            [In] uint Thread);

        [PreserveSig]
        new int GetCommand(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        new int SetCommand(
            [In][MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int GetOffsetExpression(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ExpressionSize);

        [PreserveSig]
        new int SetOffsetExpression(
            [In][MarshalAs(UnmanagedType.LPStr)] string Expression);

        [PreserveSig]
        new int GetParameters(
            [Out] out DEBUG_BREAKPOINT_PARAMETERS Params);

        /* IDebugBreakpoint2 */

        [PreserveSig]
        int GetCommandWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint CommandSize);

        [PreserveSig]
        int SetCommandWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        int GetOffsetExpressionWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ExpressionSize);

        [PreserveSig]
        int SetOffsetExpressionWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Command);
    }
}