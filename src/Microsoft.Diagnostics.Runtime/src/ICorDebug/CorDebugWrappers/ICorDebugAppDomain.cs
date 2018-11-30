// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [ComConversionLoss]
    [InterfaceType(1)]
    [Guid("3D6F5F63-7538-11D3-8D5B-00104B35E7EF")]
    public interface ICorDebugAppDomain : ICorDebugController
    {
        new void Stop([In] uint dwTimeout);

        new void Continue([In] int fIsOutOfBand);

        new void IsRunning([Out] out int pbRunning);

        new void HasQueuedCallbacks(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [Out] out int pbQueued);

        new void EnumerateThreads(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugThreadEnum ppThreads);

        new void SetAllThreadsDebugState(
            [In] CorDebugThreadState state,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pExceptThisThread);

        new void Detach();

        new void Terminate([In] uint exitCode);

        new void CanCommitChanges(
            [In] uint cSnapshots,
            [In][MarshalAs(UnmanagedType.Interface)]
            ref ICorDebugEditAndContinueSnapshot pSnapshots,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugErrorInfoEnum pError);

        new void CommitChanges(
            [In] uint cSnapshots,
            [In][MarshalAs(UnmanagedType.Interface)]
            ref ICorDebugEditAndContinueSnapshot pSnapshots,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugErrorInfoEnum pError);

        void GetProcess(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugProcess ppProcess);

        void EnumerateAssemblies(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugAssemblyEnum ppAssemblies);

        void GetModuleFromMetaDataInterface(
            [In][MarshalAs(UnmanagedType.IUnknown)]
            object pIMetaData,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugModule ppModule);

        void EnumerateBreakpoints(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugBreakpointEnum ppBreakpoints);

        void EnumerateSteppers(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugStepperEnum ppSteppers);

        void IsAttached([Out] out int pbAttached);

        void GetName([In] uint cchName, [Out] out uint pcchName, [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        void GetObject(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out ICorDebugValue ppObject);

        void Attach();

        void GetID([Out] out uint pId);
    }
}