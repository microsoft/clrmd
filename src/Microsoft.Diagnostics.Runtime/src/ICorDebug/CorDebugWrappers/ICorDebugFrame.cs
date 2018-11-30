// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("CC7BCAEF-8A68-11D2-983C-0000F808342D")]
    [InterfaceType(1)]
    public interface ICorDebugFrame
    {
        void GetChain(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugChain ppChain);

        void GetCode(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugCode ppCode);

        void GetFunction(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFunction ppFunction);

        void GetFunctionToken([Out] out uint pToken);

        void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        void GetCaller(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFrame ppFrame);

        void GetCallee(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugFrame ppFrame);

        void CreateStepper(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugStepper ppStepper);
    }
}