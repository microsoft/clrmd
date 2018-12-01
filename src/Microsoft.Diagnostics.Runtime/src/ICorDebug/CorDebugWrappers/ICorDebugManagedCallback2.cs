// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("250E5EEA-DB5C-4C76-B6F3-8C46F12E3203")]
    [InterfaceType(1)]
    public interface ICorDebugManagedCallback2
    {
        void FunctionRemapOpportunity(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pOldFunction,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pNewFunction,
            [In] uint oldILOffset);

        void CreateConnection(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In] uint dwConnectionId,
            [In] ref ushort pConnName);

        void ChangeConnection(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In] uint dwConnectionId);

        void DestroyConnection(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In] uint dwConnectionId);

        void Exception(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFrame pFrame,
            [In] uint nOffset,
            [In] CorDebugExceptionCallbackType dwEventType,
            [In] uint dwFlags);

        void ExceptionUnwind(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In] CorDebugExceptionUnwindCallbackType dwEventType,
            [In] uint dwFlags);

        void FunctionRemapComplete(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pFunction);

        void MDANotification(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugController pController,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugMDA pMDA);
    }
}