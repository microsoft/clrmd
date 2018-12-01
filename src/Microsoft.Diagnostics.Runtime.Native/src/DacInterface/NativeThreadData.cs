// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeThreadData
    {
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public readonly ulong AllocContextPointer;
        public readonly ulong AllocContextLimit;
        public readonly ulong Context;
        public readonly ulong Teb;
        public readonly ulong NextThread;
    }
}