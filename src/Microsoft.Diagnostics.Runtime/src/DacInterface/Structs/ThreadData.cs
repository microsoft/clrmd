// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ThreadData : IThreadData
    {
        public readonly uint ManagedThreadId;
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public readonly ulong AllocationContextPointer;
        public readonly ulong AllocationContextLimit;
        public readonly ulong Context;
        public readonly ulong Domain;
        public readonly ulong Frame;
        public readonly uint LockCount;
        public readonly ulong FirstNestedException;
        public readonly ulong Teb;
        public readonly ulong FiberData;
        public readonly ulong LastThrownObjectHandle;
        public readonly ulong NextThread;

        internal ThreadData(ref ThreadData other)
        {
            this = other;

            // Sign extension issues
            if (IntPtr.Size == 4)
            {
                FixupPointer(ref AllocationContextPointer);
                FixupPointer(ref AllocationContextLimit);
                FixupPointer(ref Context);
                FixupPointer(ref Domain);
                FixupPointer(ref Frame);
                FixupPointer(ref FirstNestedException);
                FixupPointer(ref Teb);
                FixupPointer(ref FiberData);
                FixupPointer(ref LastThrownObjectHandle);
                FixupPointer(ref NextThread);
            }
        }

        private static void FixupPointer(ref ulong ptr)
        {
            ptr = (uint)ptr;
        }

        ulong IThreadData.Next => NextThread;
        ulong IThreadData.AllocPtr => AllocationContextPointer;
        ulong IThreadData.AllocLimit => AllocationContextLimit;
        uint IThreadData.OSThreadID => OSThreadId;
        ulong IThreadData.Teb => Teb;
        ulong IThreadData.AppDomain => Domain;
        uint IThreadData.LockCount => LockCount;
        int IThreadData.State => State;
        ulong IThreadData.ExceptionPtr => LastThrownObjectHandle;
        uint IThreadData.ManagedThreadID => ManagedThreadId;
        bool IThreadData.Preemptive => PreemptiveGCDisabled == 0;
    }
}