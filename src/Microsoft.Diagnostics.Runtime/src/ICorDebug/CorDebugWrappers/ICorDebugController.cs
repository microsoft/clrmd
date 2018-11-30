// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("3D6F5F62-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(1)]
    public interface ICorDebugController
    {
        void Stop([In] uint dwTimeout);
        void Continue([In] int fIsOutOfBand);
        void IsRunning([Out] out int pbRunning);

        void HasQueuedCallbacks(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [Out] out int pbQueued);

        void EnumerateThreads(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThreadEnum ppThreads);

        void SetAllThreadsDebugState(
            [In] CorDebugThreadState state,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pExceptThisThread);

        void Detach();

        void Terminate([In] uint exitCode);

        void CanCommitChanges(
            [In] uint cSnapshots,
            [In][MarshalAs(UnmanagedType.Interface)]
            ref ICorDebugEditAndContinueSnapshot pSnapshots,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugErrorInfoEnum pError);

        void CommitChanges(
            [In] uint cSnapshots,
            [In][MarshalAs(UnmanagedType.Interface)]
            ref ICorDebugEditAndContinueSnapshot pSnapshots,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugErrorInfoEnum pError);
    }
}