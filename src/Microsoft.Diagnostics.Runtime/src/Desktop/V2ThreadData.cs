// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2ThreadData : IThreadData
    {
        public readonly uint CorThreadId;
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public readonly ulong AllocContextPtr;
        public readonly ulong AllocContextLimit;
        public readonly ulong Context;
        public readonly ulong Domain;
        public readonly ulong SharedStaticData;
        public readonly ulong UnsharedStaticData;
        public readonly ulong Frame;
        public readonly uint LockCount;
        public readonly ulong FirstNestedException;
        public readonly ulong Teb;
        public readonly ulong FiberData;
        public readonly ulong LastThrownObjectHandle;
        public readonly ulong NextThread;

        ulong IThreadData.Next => IntPtr.Size == 8 ? NextThread : (uint)NextThread;
        ulong IThreadData.AllocPtr => IntPtr.Size == 8 ? AllocContextPtr : (uint)AllocContextPtr;
        ulong IThreadData.AllocLimit => IntPtr.Size == 8 ? AllocContextLimit : (uint)AllocContextLimit;
        uint IThreadData.OSThreadID => OSThreadId;
        ulong IThreadData.Teb => IntPtr.Size == 8 ? Teb : (uint)Teb;
        ulong IThreadData.AppDomain => Domain;
        uint IThreadData.LockCount => LockCount;
        int IThreadData.State => State;
        ulong IThreadData.ExceptionPtr => LastThrownObjectHandle;
        uint IThreadData.ManagedThreadID => CorThreadId;
        bool IThreadData.Preemptive => PreemptiveGCDisabled == 0;
    }
}