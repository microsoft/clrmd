// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("D613F0BB-ACE1-4C19-BD72-E4C08D5DA7F5")]
    [InterfaceType(1)]
    public interface ICorDebugType
    {
        void GetType([Out] out CorElementType ty);

        void GetClass(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugClass ppClass);

        void EnumerateTypeParameters(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugTypeEnum ppTyParEnum);

        void GetFirstTypeParameter(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType value);

        void GetBase(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugType pBase);

        void GetStaticFieldValue(
            [In] uint fieldDef,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFrame pFrame,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppValue);

        void GetRank([Out] out uint pnRank);
    }
}