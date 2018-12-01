// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [InterfaceType(1)]
    [Guid("CC7BCAF6-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugEval
    {
        void CallFunction(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pFunction,
            [In] uint nArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugValue[] ppArgs);

        void NewObject(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pConstructor,
            [In] uint nArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugValue[] ppArgs);

        void NewObjectNoConstructor(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass pClass);

        void NewString([In][MarshalAs(UnmanagedType.LPWStr)] string @string);

        void NewArray(
            [In] CorElementType elementType,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass pElementClass,
            [In] uint rank,
            [In] ref uint dims,
            [In] ref uint lowBounds);

        void IsActive([Out] out int pbActive);

        void Abort();

        void GetResult(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppResult);

        void GetThread(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThread ppThread);

        void CreateValue(
            [In] CorElementType elementType,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass pElementClass,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);
    }
}