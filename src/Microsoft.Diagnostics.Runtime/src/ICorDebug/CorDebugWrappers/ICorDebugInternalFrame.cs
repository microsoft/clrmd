// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("B92CC7F7-9D2D-45C4-BC2B-621FCC9DFBF4")]
    public interface ICorDebugInternalFrame : ICorDebugFrame
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

        void GetFrameType([Out] out CorDebugInternalFrameType pType);
    }
}