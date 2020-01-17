// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("ce289126-9e84-45a7-937e-67bb18691493")]
    public interface IDebugRegisters
    {
        [PreserveSig]
        int GetNumberRegisters(
            out uint Number);

        [PreserveSig]
        int GetDescription(
            [In] uint Register,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            out uint NameSize,
            out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        int GetIndexByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            out uint Index);

        [PreserveSig]
        int GetValue(
            [In] uint Register,
            out DEBUG_VALUE Value);

        [PreserveSig]
        int SetValue(
            [In] uint Register,
            [In] ref DEBUG_VALUE Value);

        [PreserveSig]
        int GetValues( //FIX ME!!! This needs to be tested
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Values);

        [PreserveSig]
        int SetValues(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        int OutputRegisters(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_REGISTERS Flags);

        [PreserveSig]
        int GetInstructionOffset(
            out ulong Offset);

        [PreserveSig]
        int GetStackOffset(
            out ulong Offset);

        [PreserveSig]
        int GetFrameOffset(
            out ulong Offset);
    }
}