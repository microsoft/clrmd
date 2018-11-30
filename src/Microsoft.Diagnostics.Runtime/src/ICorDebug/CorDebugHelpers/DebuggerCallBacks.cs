// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    internal class DebuggerCallBacks : ICorDebugManagedCallback3, ICorDebugManagedCallback2, ICorDebugManagedCallback
    {
        public virtual void CustomNotification(ICorDebugThread pThread, ICorDebugAppDomain pAppDomain)
        {
            pAppDomain.Continue(0);
        }

        public virtual void FunctionRemapOpportunity(
            ICorDebugAppDomain pAppDomain,
            ICorDebugThread pThread,
            ICorDebugFunction pOldFunction,
            ICorDebugFunction pNewFunction,
            uint oldILOffset)
        {
            pAppDomain.Continue(0);
        }

        public virtual void CreateConnection(ICorDebugProcess pProcess, uint dwConnectionId, ref ushort pConnName)
        {
            pProcess.Continue(0);
        }

        public virtual void ChangeConnection(ICorDebugProcess pProcess, uint dwConnectionId)
        {
            pProcess.Continue(0);
        }

        public virtual void DestroyConnection(ICorDebugProcess pProcess, uint dwConnectionId)
        {
            pProcess.Continue(0);
        }

        public virtual void Exception(
            ICorDebugAppDomain pAppDomain,
            ICorDebugThread pThread,
            ICorDebugFrame pFrame,
            uint nOffset,
            CorDebugExceptionCallbackType dwEventType,
            uint dwFlags)
        {
            pAppDomain.Continue(0);
        }

        public virtual void ExceptionUnwind(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, CorDebugExceptionUnwindCallbackType dwEventType, uint dwFlags)
        {
            pAppDomain.Continue(0);
        }

        public virtual void FunctionRemapComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction)
        {
            pAppDomain.Continue(0);
        }

        public virtual void MDANotification(ICorDebugController pController, ICorDebugThread pThread, ICorDebugMDA pMDA)
        {
            pController.Continue(0);
        }

        public virtual void Breakpoint(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint)
        {
            pAppDomain.Continue(0);
        }

        public virtual void StepComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugStepper pStepper, CorDebugStepReason reason)
        {
            pAppDomain.Continue(0);
        }

        public virtual void Break(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            pAppDomain.Continue(0);
        }

        public virtual void Exception(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int unhandled)
        {
            pAppDomain.Continue(0);
        }

        public virtual void EvalComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval)
        {
            pAppDomain.Continue(0);
        }

        public virtual void EvalException(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval)
        {
            pAppDomain.Continue(0);
        }

        public virtual void CreateProcess(ICorDebugProcess pProcess)
        {
            pProcess.Continue(0);
        }

        public virtual void ExitProcess(ICorDebugProcess pProcess)
        {
            pProcess.Continue(0);
        }

        public virtual void CreateThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            pAppDomain.Continue(0);
        }

        public virtual void ExitThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            pAppDomain.Continue(0);
        }

        public virtual void LoadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule)
        {
            pAppDomain.Continue(0);
        }

        public virtual void UnloadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule)
        {
            pAppDomain.Continue(0);
        }

        public virtual void LoadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c)
        {
            pAppDomain.Continue(0);
        }

        public virtual void UnloadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c)
        {
            pAppDomain.Continue(0);
        }

        public virtual void DebuggerError(ICorDebugProcess pProcess, int errorHR, uint errorCode)
        {
            pProcess.Continue(0);
        }

        public virtual void LogMessage(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, string pLogSwitchName, string pMessage)
        {
            pAppDomain.Continue(0);
        }

        public virtual void LogSwitch(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, uint ulReason, string pLogSwitchName, string pParentName)
        {
            pAppDomain.Continue(0);
        }

        public virtual void CreateAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain)
        {
            pAppDomain.Continue(0);
        }

        public virtual void ExitAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain)
        {
            pAppDomain.Continue(0);
        }

        public virtual void LoadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly)
        {
            pAppDomain.Continue(0);
        }

        public virtual void UnloadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly)
        {
            pAppDomain.Continue(0);
        }

        public virtual void ControlCTrap(ICorDebugProcess pProcess)
        {
            pProcess.Continue(0);
        }

        public virtual void NameChange(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread)
        {
            pAppDomain.Continue(0);
        }

        public virtual void UpdateModuleSymbols(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule, IStream pSymbolStream)
        {
            pAppDomain.Continue(0);
        }

        public virtual void EditAndContinueRemap(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction, int fAccurate)
        {
            pAppDomain.Continue(0);
        }

        public virtual void BreakpointSetError(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint, uint dwError)
        {
            pAppDomain.Continue(0);
        }
    }
}