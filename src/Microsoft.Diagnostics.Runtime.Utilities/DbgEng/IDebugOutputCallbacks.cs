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
        /// <summary>
        /// Which dbgeng output channels we want delivered. Default TEXT +
        /// EXPLICIT_FLUSH. Subclasses can override to include DML.
        /// </summary>
        DEBUG_OUTCBI OutputInterestFlags { get => DEBUG_OUTCBI.TEXT | DEBUG_OUTCBI.EXPLICIT_FLUSH; }
        void OnFlush() { }
        void OnText(DEBUG_OUTPUT flags, string? text, ulong args);

        /// <summary>
        /// Called for DML payloads (e.g. <c>!analyze -xml</c>) when DML is
        /// included in <see cref="OutputInterestFlags"/>. Default no-op.
        /// </summary>
        void OnDml(DEBUG_OUTCBF flags, string? dml, ulong args) { }
    }


    internal sealed unsafe class DebugOutputCallbacksCOM : ComWrappers
    {
        internal static Guid IID_IOutputCallbacks  = new("4bf58045-d654-4c40-b0af-683090f356dc");
        internal static Guid IID_IOutputCallbacksWide = new("4c7fd663-c394-4e26-8ef1-34ad5ed3764c");
        internal static Guid IID_IOutputCallbacks2 = new("67721fe9-56d2-4a44-a325-2b65513ce6eb");
        private static readonly ComInterfaceEntry* s_wrapperEntries = InitializeComInterfaceEntries();
        private const int EntryCount = 3;
        public static DebugOutputCallbacksCOM Instance { get; } = new();

        private static ComInterfaceEntry* InitializeComInterfaceEntries()
        {
            GetIUnknownImpl(out IntPtr qi, out IntPtr addRef, out IntPtr release);

            // Expose all three IDebugOutputCallbacks* IIDs from the same
            // managed object. Each IID's vtable is the FULL IDebugOutputCallbacks2
            // layout (6 slots) — dbgeng never calls the legacy ANSI/Wide
            // Output slots (slot 4) when IDebugOutputCallbacks2 is available;
            // it goes straight to Output2 via QI. But dbgeng probes for all
            // three IIDs during SetOutputCallbacks and silently downgrades to
            // "text-only, no DML routing" if any QI fails. Verified by the
            // diagnostic probe at C:\work\tmp\dbgcb-probe.
            ComInterfaceEntry* wrappers = (ComInterfaceEntry*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(DebugOutputCallbacksCOM), sizeof(ComInterfaceEntry) * EntryCount);
            nint vt = IDebugOutputCallbacksVtbl.Create(qi, addRef, release);
            wrappers[0].IID = IID_IOutputCallbacks;
            wrappers[0].Vtable = vt;
            wrappers[1].IID = IID_IOutputCallbacksWide;
            wrappers[1].Vtable = vt;
            wrappers[2].IID = IID_IOutputCallbacks2;
            wrappers[2].Vtable = vt;
            return wrappers;
        }

        protected override unsafe ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            if (obj is not IDebugOutputCallbacks)
                throw new InvalidOperationException($"Type '{obj.GetType().FullName}' is not an instance of {nameof(IDebugOutputCallbacks)}");

            count = EntryCount;
            return s_wrapperEntries;
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
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_OUTCB, DEBUG_OUTPUT, ulong, nint, int>)&Output2;

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

            /// <summary>
            /// IDebugOutputCallbacks2::Output2 dispatcher. The 'Which' arg
            /// (DEBUG_OUTCB) selects TEXT / DML / EXPLICIT_FLUSH; we route
            /// each to the appropriate callback method. The 'Flags' arg is
            /// DEBUG_OUTPUT for TEXT (severity bits) and DEBUG_OUTCBF for
            /// DML (HAS_TAGS / HAS_SPECIAL_CHARACTERS / COMBINED_FLUSH).
            /// We declare the param as DEBUG_OUTPUT and reinterpret for DML
            /// since both are 32-bit and ABI-compatible.
            /// </summary>
            [UnmanagedCallersOnly]
            private static int Output2(nint self, DEBUG_OUTCB which, DEBUG_OUTPUT flags, ulong args, nint textPtr)
            {
                IDebugOutputCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugOutputCallbacks>((ComInterfaceDispatch*)self);
                switch (which)
                {
                    case DEBUG_OUTCB.EXPLICIT_FLUSH:
                        callbacks.OnFlush();
                        break;
                    case DEBUG_OUTCB.DML:
                        callbacks.OnDml((DEBUG_OUTCBF)(uint)flags, Marshal.PtrToStringUni(textPtr), args);
                        break;
                    case DEBUG_OUTCB.TEXT:
                    default:
                        callbacks.OnText(flags, Marshal.PtrToStringUni(textPtr), args);
                        break;
                }
                return 0;
            }
        }
    }
}