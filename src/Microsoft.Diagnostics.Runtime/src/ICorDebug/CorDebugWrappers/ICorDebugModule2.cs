// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("7FCC5FB5-49C0-41DE-9938-3B88B5B9ADD7")]
    public interface ICorDebugModule2
    {
        void SetJMCStatus([In] int bIsJustMyCode, [In] uint cTokens, [In] ref uint pTokens);

        void ApplyChanges([In] uint cbMetadata, [In][MarshalAs(UnmanagedType.LPArray)] byte[] pbMetadata, [In] uint cbIL, [In][MarshalAs(UnmanagedType.LPArray)] byte[] pbIL);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int SetJITCompilerFlags([In] uint dwFlags);

        void GetJITCompilerFlags([Out] out uint pdwFlags);

        void ResolveAssembly(
            [In] uint tkAssemblyRef,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugAssembly ppAssembly);
    }
}