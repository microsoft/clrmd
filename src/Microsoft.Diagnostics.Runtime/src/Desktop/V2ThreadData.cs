// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2ThreadData : IThreadData
    {
        public uint corThreadId;
        public uint osThreadId;
        public int state;
        public uint preemptiveGCDisabled;
        public ulong allocContextPtr;
        public ulong allocContextLimit;
        public ulong context;
        public ulong domain;
        public ulong sharedStaticData;
        public ulong unsharedStaticData;
        public ulong pFrame;
        public uint lockCount;
        public ulong firstNestedException;
        public ulong teb;
        public ulong fiberData;
        public ulong lastThrownObjectHandle;
        public ulong nextThread;

        public ulong Next => IntPtr.Size == 8 ? nextThread : (uint)nextThread;
        public ulong AllocPtr => IntPtr.Size == 8 ? allocContextPtr : (uint)allocContextPtr;
        public ulong AllocLimit => IntPtr.Size == 8 ? allocContextLimit : (uint)allocContextLimit;
        public uint OSThreadId => osThreadId;
        public ulong Teb => IntPtr.Size == 8 ? teb : (uint)teb;
        public ulong AppDomain => domain;
        public uint LockCount => lockCount;
        public int State => state;
        public ulong ExceptionPtr => lastThrownObjectHandle;
        public uint ManagedThreadId => corThreadId;
        public bool Preemptive => preemptiveGCDisabled == 0;
    }
}