// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugOutputCallbacks
    {
        DEBUG_OUTCBI OutputInterestFlags { get => DEBUG_OUTCBI.TEXT | DEBUG_OUTCBI.EXPLICIT_FLUSH; }
        void OnFlush() { }
        void OnText(DEBUG_OUTPUT flags, string? text, ulong args);
        void OnDml(DEBUG_OUTCB cbf, string? dml, ulong args) { }
    }


    internal sealed unsafe class DebugOutputCallbacksCOM : ComWrappers
    {
        internal static Guid IID_IOutputCallbacks2 = new("67721fe9-56d2-4a44-a325-2b65513ce6eb");
        private static readonly ComInterfaceEntry* s_wrapperEntry = InitializeComInterfaceEntry();
        public static DebugOutputCallbacksCOM Instance { get; } = new();

        private static ComInterfaceEntry* InitializeComInterfaceEntry()
        {
            GetIUnknownImpl(out IntPtr qi, out IntPtr addRef, out IntPtr release);

            ComInterfaceEntry* wrappers = (ComInterfaceEntry*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DebugEventCallbacksCOM), sizeof(ComInterfaceEntry));
            wrappers[0].IID = IID_IOutputCallbacks2;
            wrappers[0].Vtable = IDebugOutputCallbacksVtbl.Create(qi, addRef, release);

            return wrappers;
        }

        protected override unsafe ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            if (obj is not IDebugOutputCallbacks)
                throw new InvalidOperationException($"Type '{obj.GetType().FullName}' is not an instance of {nameof(IDebugOutputCallbacks)}");

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

        private static unsafe class IDebugOutputCallbacksVtbl
        {
            public static nint Create(nint qi, nint addref, nint release)
            {
                const int total = 6;
                int i = 0;

                nint* vtable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IDebugOutputCallbacksVtbl), sizeof(nint) * total);

                vtable[i++] = qi;
                vtable[i++] = addref;
                vtable[i++] = release;
                vtable[i++] = (nint)(delegate* unmanaged<nint, int>)&Ignore;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_OUTCBI*, int>)&GetInterestMask;
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_OUTCB, DEBUG_OUTPUT, ulong, nint, int>)&Output;

                Debug.Assert(i == total);

                return (nint)vtable;
            }

            [UnmanagedCallersOnly]
            private static int Ignore(nint self) => 0;

            [UnmanagedCallersOnly]
            private static int GetInterestMask(nint self, DEBUG_OUTCBI* pInterest)
            {
                IDebugOutputCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugOutputCallbacks>((ComInterfaceDispatch*)self);
                *pInterest = callbacks.OutputInterestFlags;
                return 0;
            }

            [UnmanagedCallersOnly]
            private static int Output(nint self, DEBUG_OUTCB cb, DEBUG_OUTPUT flags, ulong args, nint textPtr)
            {
                IDebugOutputCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugOutputCallbacks>((ComInterfaceDispatch*)self);
                if (cb == DEBUG_OUTCB.EXPLICIT_FLUSH)
                {
                    callbacks.OnFlush();
                }
                else
                {
                    string? text = Marshal.PtrToStringUni(textPtr);
                    callbacks.OnText(flags, text, args);
                }

                return 0;
            }
        }
    }
}