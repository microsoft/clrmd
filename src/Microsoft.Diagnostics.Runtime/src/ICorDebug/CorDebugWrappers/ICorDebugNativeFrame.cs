// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("03E26314-4F76-11D3-88C6-006097945418")]
    public interface ICorDebugNativeFrame : ICorDebugFrame
    {
        new void GetChain(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugChain ppChain);

        new void GetCode(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugCode ppCode);

        new void GetFunction(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFunction ppFunction);

        new void GetFunctionToken([Out] out uint pToken);

        new void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        new void GetCaller(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFrame ppFrame);

        new void GetCallee(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFrame ppFrame);

        new void CreateStepper(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugStepper ppStepper);

        void GetIP([Out] out uint pnOffset);

        void SetIP([In] uint nOffset);

        void GetRegisterSet(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugRegisterSet ppRegisters);

        void GetLocalRegisterValue(
            [In] CorDebugRegister reg,
            [In] uint cbSigBlob,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            uint pvSigBlob,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetLocalDoubleRegisterValue(
            [In] CorDebugRegister highWordReg,
            [In] CorDebugRegister lowWordReg,
            [In] uint cbSigBlob,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            uint pvSigBlob,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetLocalMemoryValue(
            [In] ulong address,
            [In] uint cbSigBlob,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            uint pvSigBlob,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetLocalRegisterMemoryValue(
            [In] CorDebugRegister highWordReg,
            [In] ulong lowWordAddress,
            [In] uint cbSigBlob,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            uint pvSigBlob,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetLocalMemoryRegisterValue(
            [In] ulong highWordAddress,
            [In] CorDebugRegister lowWordRegister,
            [In] uint cbSigBlob,
            [In][ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")]
            uint pvSigBlob,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int CanSetIP([In] uint nOffset);
    }
}