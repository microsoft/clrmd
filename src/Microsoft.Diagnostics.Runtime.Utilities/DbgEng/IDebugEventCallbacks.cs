// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    /// <see cref="https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/dbgeng/nn-dbgeng-idebugeventcallbackswide"/>
    public interface IDebugEventCallbacks
    {
        DEBUG_EVENT EventInterestMask { get => DEBUG_EVENT.NONE; }
        DEBUG_STATUS OnBreakpoint(nint breakpoint) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnException(in EXCEPTION_RECORD64 exception, bool firstChance) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnCreateThread(ulong handle, ulong dataOffset, ulong startOffset) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnExitThread(int exitCode) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnCreateProcess(ulong imageFileHandle, ulong handle, ulong baseAddress, uint moduleSize, string? moduleName, string? imageName, uint checkSum, uint timeDateStamp, ulong initialThreadHandle, ulong threadDataOffset, ulong startOffset) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnExitProcess(int exitCode) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnLoadModule(ulong imageFileHandle, ulong baseAddress, uint moduleSize, string? moduleName, string? imageName, uint checkSum, uint timeDateStamp) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnUnloadModule(string? imageBaseName, ulong baseAddress) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnSystemError(uint error, uint level) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnSessionStatus(DEBUG_SESSION status) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnDebuggeeChangeState(DEBUG_CDS flags, ulong argument) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnEngineChangeState(DEBUG_CES flags, ulong argument) => DEBUG_STATUS.NO_CHANGE;
        DEBUG_STATUS OnSymbolChangeState(DEBUG_CSS flags, ulong argument) => DEBUG_STATUS.NO_CHANGE;
    }

    internal sealed unsafe class DebugEventCallbacksCOM : ComWrappers
    {
        internal static Guid IID_IDebugEventCallbacksWide = new("0690e046-9c23-45ac-a04f-987ac29ad0d3");
        private static readonly ComInterfaceEntry* s_wrapperEntry = InitializeComInterfaceEntry();
        public static DebugEventCallbacksCOM Instance { get; } = new();

        private static ComInterfaceEntry* InitializeComInterfaceEntry()
        {
            GetIUnknownImpl(out IntPtr qi, out IntPtr addRef, out IntPtr release);

            ComInterfaceEntry* wrappers = (ComInterfaceEntry*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DebugEventCallbacksCOM), sizeof(ComInterfaceEntry));
            wrappers[0].IID = IID_IDebugEventCallbacksWide;
            wrappers[0].Vtable = IDebugEventCallbacksWideVtable.Create(qi, addRef, release);

            return wrappers;
        }

        protected override unsafe ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            if (obj is not IDebugEventCallbacks)
                throw new InvalidOperationException($"Type '{obj.GetType().FullName}' is not an instance of {nameof(IDebugEventCallbacks)}");

            count = 1;
            return s_wrapperEntry;
        }

        protected override object? CreateObject(IntPtr externalComObject, CreateObjectFlags flags)
        {
            throw new NotImplementedException();
        }

        protected override void ReleaseObjects(IEnumerable objects)
        {
            throw new NotImplementedException();
        }

        private static unsafe class IDebugEventCallbacksWideVtable
        {
            public static nint Create(nint qi, nint addref, nint release)
            {
                const int total = 17;
                int i = 0;

                nint* vtable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IDebugEventCallbacksWideVtable), sizeof(nint) * total);

                vtable[i++] = qi;
                vtable[i++] = addref;
                vtable[i++] = release;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_EVENT*, int>)&GetInterestMask;
                vtable[i++] = (nint)(delegate* unmanaged<nint, nint, DEBUG_STATUS>)&Breakpoint;
                vtable[i++] = (nint)(delegate* unmanaged<nint, EXCEPTION_RECORD64*, uint, DEBUG_STATUS>)&Exception;
                vtable[i++] = (nint)(delegate* unmanaged<nint, ulong, ulong, ulong, DEBUG_STATUS>)&CreateThread;
                vtable[i++] = (nint)(delegate* unmanaged<nint, int, DEBUG_STATUS>)&ExitThread;
                vtable[i++] = (nint)(delegate* unmanaged<nint, ulong, ulong, ulong, uint, nint, nint, uint, uint, ulong, ulong, ulong, DEBUG_STATUS>)&CreateProcess;
                vtable[i++] = (nint)(delegate* unmanaged<nint, int, DEBUG_STATUS>)&ExitProcess;
                vtable[i++] = (nint)(delegate* unmanaged<nint, ulong, ulong, uint, nint, nint, uint, uint, DEBUG_STATUS>)&LoadModule;
                vtable[i++] = (nint)(delegate* unmanaged<nint, nint, ulong, DEBUG_STATUS>)&UnloadModule;
                vtable[i++] = (nint)(delegate* unmanaged<nint, uint, uint, DEBUG_STATUS>)&SystemError;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_SESSION, DEBUG_STATUS>)&SessionStatus;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_CDS, ulong, DEBUG_STATUS>)&ChangeDebuggeeState;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_CES, ulong, DEBUG_STATUS>)&ChangeEngineState;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_CSS, ulong, DEBUG_STATUS>)&ChangeSymbolState;

                Debug.Assert(i == total);

                return (nint)vtable;
            }

            [UnmanagedCallersOnly]
            private static int GetInterestMask(nint self, DEBUG_EVENT* pMask)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                *pMask = callbacks.EventInterestMask;
                return 0;
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS Breakpoint(nint self, nint pBreakpoint)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnBreakpoint(pBreakpoint);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS Exception(nint self, EXCEPTION_RECORD64* pException, uint firstChance)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnException(*pException, firstChance != 0);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS CreateThread(nint self, ulong handle, ulong dataOffset, ulong startOffset)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnCreateThread(handle, dataOffset, startOffset);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS ExitThread(nint self, int exitCode)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnExitThread(exitCode);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS CreateProcess(nint self, ulong imageFileHandle, ulong handle, ulong baseAddress, uint moduleSize,
                nint moduleNamePtr, nint imageNamePtr, uint checkSum, uint timeDateStamp, ulong initialThreadHandle, ulong threadDataOffset, ulong startOffset)
            {
                string? moduleName = Marshal.PtrToStringUni(moduleNamePtr);
                string? imageName = Marshal.PtrToStringUni(imageNamePtr);

                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnCreateProcess(imageFileHandle, handle, baseAddress, moduleSize, moduleName, imageName, checkSum, timeDateStamp, initialThreadHandle, threadDataOffset, startOffset);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS ExitProcess(nint self, int exitCode)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnExitProcess(exitCode);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS LoadModule(nint self, ulong imageFileHandle, ulong baseAddress, uint moduleSize, nint moduleNamePtr, nint imageNamePtr, uint checkSum, uint timeDateStamp)
            {
                string? moduleName = Marshal.PtrToStringUni(moduleNamePtr);
                string? imageName = Marshal.PtrToStringUni(imageNamePtr);

                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnLoadModule(imageFileHandle, baseAddress, moduleSize, moduleName, imageName, checkSum, timeDateStamp);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS UnloadModule(nint self, nint imageBaseNamePtr, ulong baseAddress)
            {
                string? imageBaseName = Marshal.PtrToStringUni(imageBaseNamePtr);

                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnUnloadModule(imageBaseName, baseAddress);

            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS SystemError(nint self, uint error, uint level)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnSystemError(error, level);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS SessionStatus(nint self, DEBUG_SESSION status)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnSessionStatus(status);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS ChangeDebuggeeState(nint self, DEBUG_CDS flags, ulong argument)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnDebuggeeChangeState(flags, argument);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS ChangeEngineState(nint self, DEBUG_CES flags, ulong argument)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnEngineChangeState(flags, argument);
            }

            [UnmanagedCallersOnly]
            private static DEBUG_STATUS ChangeSymbolState(nint self, DEBUG_CSS flags, ulong argument)
            {
                IDebugEventCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugEventCallbacks>((ComInterfaceDispatch*)self);
                return callbacks.OnSymbolChangeState(flags, argument);
            }
        }
    }
}