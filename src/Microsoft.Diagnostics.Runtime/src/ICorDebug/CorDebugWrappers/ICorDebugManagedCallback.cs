// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("3D6F5F60-7538-11D3-8D5B-00104B35E7EF")]
    [InterfaceType(1)]
    public interface ICorDebugManagedCallback
    {
        void Breakpoint(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugBreakpoint pBreakpoint);

        void StepComplete(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugStepper pStepper,
            [In] CorDebugStepReason reason);

        void Break(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread thread);

        void Exception(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In] int unhandled);

        void EvalComplete(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugEval pEval);

        void EvalException(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugEval pEval);

        void CreateProcess(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess);

        void ExitProcess(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess);

        void CreateThread(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread thread);

        void ExitThread(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread thread);

        void LoadModule(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugModule pModule);

        void UnloadModule(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugModule pModule);

        void LoadClass(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass c);

        void UnloadClass(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugClass c);

        void DebuggerError(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In][MarshalAs(UnmanagedType.Error)] int errorHR,
            [In] uint errorCode);

        void LogMessage(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In] int lLevel,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pMessage);

        void LogSwitch(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In] int lLevel,
            [In] uint ulReason,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pParentName);

        void CreateAppDomain(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain);

        void ExitAppDomain(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain);

        void LoadAssembly(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAssembly pAssembly);

        void UnloadAssembly(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAssembly pAssembly);

        void ControlCTrap(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugProcess pProcess);

        void NameChange(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread);

        void UpdateModuleSymbols(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugModule pModule,
            [In][MarshalAs(UnmanagedType.Interface)]
            IStream pSymbolStream);

        void EditAndContinueRemap(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugFunction pFunction,
            [In] int fAccurate);

        void BreakpointSetError(
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugAppDomain pAppDomain,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugThread pThread,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICorDebugBreakpoint pBreakpoint,
            [In] uint dwError);
    }
}