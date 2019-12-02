// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ThreadData
    {
        public readonly uint ManagedThreadId;
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public ulong AllocationContextPointer;
        public ulong AllocationContextLimit;
        public ulong Context;
        public ulong Domain;
        public ulong Frame;
        public readonly uint LockCount;
        public ulong FirstNestedException;
        public ulong Teb;
        public ulong FiberData;
        public ulong LastThrownObjectHandle;
        public ulong NextThread;

        public static void Fixup(ref ThreadData data)
        {
            // Sign extension issues
            if (IntPtr.Size == 4)
            {
                FixupPointer(ref data.AllocationContextPointer);
                FixupPointer(ref data.AllocationContextLimit);
                FixupPointer(ref data.Context);
                FixupPointer(ref data.Domain);
                FixupPointer(ref data.Frame);
                FixupPointer(ref data.FirstNestedException);
                FixupPointer(ref data.Teb);
                FixupPointer(ref data.FiberData);
                FixupPointer(ref data.LastThrownObjectHandle);
                FixupPointer(ref data.NextThread);
            }
        }

        private static void FixupPointer(ref ulong ptr)
        {
            ptr = (uint)ptr;
        }
    }
}