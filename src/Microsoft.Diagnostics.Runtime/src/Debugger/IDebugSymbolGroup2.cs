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
    [Guid("6a7ccc5f-fb5e-4dcc-b41c-6c20307bccc7")]
    public interface IDebugSymbolGroup2 : IDebugSymbolGroup
    {
        /* IDebugSymbolGroup */

        [PreserveSig]
        new int GetNumberSymbols(
            [Out] out uint Number);

        [PreserveSig]
        new int AddSymbol(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [In][Out] ref uint Index);

        [PreserveSig]
        new int RemoveSymbolByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name);

        [PreserveSig]
        new int RemoveSymbolsByIndex(
            [In] uint Index);

        [PreserveSig]
        new int GetSymbolName(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        new int GetSymbolParameters(
            [In] uint Start,
            [In] uint Count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SYMBOL_PARAMETERS[] Params);

        [PreserveSig]
        new int ExpandSymbol(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.Bool)] bool Expand);

        [PreserveSig]
        new int OutputSymbols(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT_SYMBOLS Flags,
            [In] uint Start,
            [In] uint Count);

        [PreserveSig]
        new int WriteSymbol(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Value);

        [PreserveSig]
        new int OutputAsType(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPStr)] string Type);

        /* IDebugSymbolGroup2 */

        [PreserveSig]
        int AddSymbolWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In][Out] ref uint Index);

        [PreserveSig]
        int RemoveSymbolByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name);

        [PreserveSig]
        int GetSymbolNameWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int WriteSymbolWide(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Value);

        [PreserveSig]
        int OutputAsTypeWide(
            [In] uint Index,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Type);

        [PreserveSig]
        int GetSymbolTypeName(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetSymbolTypeNameWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetSymbolSize(
            [In] uint Index,
            [Out] out uint Size);

        [PreserveSig]
        int GetSymbolOffset(
            [In] uint Index,
            [Out] out ulong Offset);

        [PreserveSig]
        int GetSymbolRegister(
            [In] uint Index,
            [Out] out uint Register);

        [PreserveSig]
        int GetSymbolValueText(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetSymbolValueTextWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetSymbolEntryInformation(
            [In] uint Index,
            [Out] out DEBUG_SYMBOL_ENTRY Info);
    }
}