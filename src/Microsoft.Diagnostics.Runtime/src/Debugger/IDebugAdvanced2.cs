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
    [Guid("716d14c9-119b-4ba5-af1f-0890e672416a")]
    public interface IDebugAdvanced2 : IDebugAdvanced
    {
        /* IDebugAdvanced */

        [PreserveSig]
        new int GetThreadContext(
            [In] IntPtr Context,
            [In] uint ContextSize);

        [PreserveSig]
        new int SetThreadContext(
            [In] IntPtr Context,
            [In] uint ContextSize);

        /* IDebugAdvanced2 */

        [PreserveSig]
        int Request(
            [In] DEBUG_REQUEST Request,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
            byte[] inBuffer,
            [In] int InBufferSize,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            byte[] outBuffer,
            [In] int OutBufferSize,
            [Out] out int OutSize);

        [PreserveSig]
        int GetSourceFileInformation(
            [In] DEBUG_SRCFILE Which,
            [In][MarshalAs(UnmanagedType.LPStr)] string SourceFile,
            [In] ulong Arg64,
            [In] uint Arg32,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)]
            byte[] buffer,
            [In] int BufferSize,
            [Out] out int InfoSize);

        [PreserveSig]
        int FindSourceFileAndToken(
            [In] uint StartElement,
            [In] ulong ModAddr,
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)]
            byte[] buffer,
            [In] int FileTokenSize,
            [Out] out int FoundElement,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out int FoundSize);

        [PreserveSig]
        int GetSymbolInformation(
            [In] DEBUG_SYMINFO Which,
            [In] ulong Arg64,
            [In] uint Arg32,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            byte[] buffer,
            [In] int BufferSize,
            [Out] out int InfoSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder StringBuffer,
            [In] int StringBufferSize,
            [Out] out int StringSize);

        [PreserveSig]
        int GetSystemObjectInformation(
            [In] DEBUG_SYSOBJINFO Which,
            [In] ulong Arg64,
            [In] uint Arg32,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            byte[] buffer,
            [In] int BufferSize,
            [Out] out int InfoSize);
    }
}