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
    [Guid("1656afa9-19c6-4e3a-97e7-5dc9160cf9c4")]
    public interface IDebugRegisters2 : IDebugRegisters
    {
        [PreserveSig]
        new int GetNumberRegisters(
            [Out] out uint Number);

        [PreserveSig]
        new int GetDescription(
            [In] uint Register,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        new int GetIndexByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out uint Index);

        [PreserveSig]
        new int GetValue(
            [In] uint Register,
            [Out] out DEBUG_VALUE Value);

        [PreserveSig]
        new int SetValue(
            [In] uint Register,
            [In] ref DEBUG_VALUE Value);

        [PreserveSig]
        new int GetValues( //FIX ME!!! This needs to be tested
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Values);

        [PreserveSig]
        new int SetValues(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        new int OutputRegisters(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_REGISTERS Flags);

        [PreserveSig]
        new int GetInstructionOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetStackOffset(
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetFrameOffset(
            [Out] out ulong Offset);

        /* IDebugRegisters2 */

        [PreserveSig]
        int GetDescriptionWide(
            [In] uint Register,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        int GetIndexByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out uint Index);

        [PreserveSig]
        int GetNumberPseudoRegisters(
            [Out] out uint Number
        );

        [PreserveSig]
        int GetPseudoDescription(
            [In] uint Register,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong TypeModule,
            [Out] out uint TypeId
        );

        [PreserveSig]
        int GetPseudoDescriptionWide(
            [In] uint Register,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong TypeModule,
            [Out] out uint TypeId
        );

        [PreserveSig]
        int GetPseudoIndexByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out uint Index
        );

        [PreserveSig]
        int GetPseudoIndexByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out uint Index
        );

        [PreserveSig]
        int GetPseudoValues(
            [In] uint Source,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Values
        );

        [PreserveSig]
        int SetPseudoValues(
            [In] uint Source,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
        );

        [PreserveSig]
        int GetValues2(
            [In] DEBUG_REGSRC Source,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_VALUE[] Values
        );

        [PreserveSig]
        int SetValues2(
            [In] uint Source,
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] uint[] Indices,
            [In] uint Start,
            [In][MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
        );

        [PreserveSig]
        int OutputRegisters2(
            [In] uint OutputControl,
            [In] uint Source,
            [In] uint Flags
        );

        [PreserveSig]
        int GetInstructionOffset2(
            [In] uint Source,
            [Out] out ulong Offset
        );

        [PreserveSig]
        int GetStackOffset2(
            [In] uint Source,
            [Out] out ulong Offset
        );

        [PreserveSig]
        int GetFrameOffset2(
            [In] uint Source,
            [Out] out ulong Offset
        );
    }
}