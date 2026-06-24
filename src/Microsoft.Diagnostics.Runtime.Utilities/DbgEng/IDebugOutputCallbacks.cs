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
                vtable[i++] = (nint)(delegate* unmanaged<nint, DEBUG_OUTCB, DEBUG_OUTCBF, ulong, nint, int>)&Output2;

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
            /// <c>IDebugOutputCallbacks2::Output2(Which, Flags, Arg, Text)</c> dispatcher.
            /// <list type="bullet">
            ///   <item><c>Which</c> (<see cref="DEBUG_OUTCB"/>) selects TEXT / DML / EXPLICIT_FLUSH.</item>
            ///   <item><c>Flags</c> (<see cref="DEBUG_OUTCBF"/>) is the callback bitfield
            ///   (COMBINED_EXPLICIT_FLUSH / DML_HAS_TAGS / DML_HAS_SPECIAL_CHARACTERS) — NOT a severity.</item>
            ///   <item><c>Arg</c> (a <c>ULONG64</c>) carries the <see cref="DEBUG_OUTPUT"/> severity mask
            ///   (NORMAL / ERROR / WARNING / ...) for both TEXT and DML payloads.</item>
            /// </list>
            /// The severity therefore comes from <c>Arg</c>, not <c>Flags</c>. (A previous version passed
            /// <c>Flags</c> as <see cref="IDebugOutputCallbacks.OnText"/>'s severity, which is always
            /// <c>COMBINED_EXPLICIT_FLUSH</c> (0x1) for plain text — coincidentally equal to
            /// <see cref="DEBUG_OUTPUT.NORMAL"/> (0x1) — so WARNING/ERROR text was mis-reported as NORMAL.)
            /// </summary>
            [UnmanagedCallersOnly]
            private static int Output2(nint self, DEBUG_OUTCB which, DEBUG_OUTCBF cbFlags, ulong arg, nint textPtr)
            {
                IDebugOutputCallbacks callbacks = ComInterfaceDispatch.GetInstance<IDebugOutputCallbacks>((ComInterfaceDispatch*)self);
                switch (which)
                {
                    case DEBUG_OUTCB.EXPLICIT_FLUSH:
                        callbacks.OnFlush();
                        break;
                    case DEBUG_OUTCB.DML:
                        callbacks.OnDml(cbFlags, Marshal.PtrToStringUni(textPtr), arg);
                        break;
                    case DEBUG_OUTCB.TEXT:
                    default:
                        callbacks.OnText((DEBUG_OUTPUT)(uint)arg, Marshal.PtrToStringUni(textPtr), arg);
                        break;
                }
                return 0;
            }
        }
    }
}