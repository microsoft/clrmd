// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("FB0D9CE7-BE66-4683-9D32-A42A04E2FD91")]
    [InterfaceType(1)]
    public interface ICorDebugEval2
    {
        void CallParameterizedFunction(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pFunction,
            [In] uint nTypeArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugType[] ppTypeArgs,
            [In] uint nArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            ICorDebugValue[] ppArgs);

        void CreateValueForType(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugType pType,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void NewParameterizedObject(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pConstructor,
            [In] uint nTypeArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugType[] ppTypeArgs,
            [In] uint nArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            ICorDebugValue[] ppArgs);

        void NewParameterizedObjectNoConstructor(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass pClass,
            [In] uint nTypeArgs,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ICorDebugType[] ppTypeArgs);

        void NewParameterizedArray(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugType pElementType,
            [In] uint rank,
            [In] ref uint dims,
            [In] ref uint lowBounds);

        void NewStringWithLength([In][MarshalAs(UnmanagedType.LPWStr)] string @string, [In] uint uiLength);
        void RudeAbort();
    }
}