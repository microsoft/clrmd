// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IClrThreadData
    {
        /// <summary>
        /// The address of the underlying coreclr!Thread.  This is never 0.
        /// </summary>
        ulong Address { get; }

        /// <summary>
        /// Returns whether we successfully requested this thread's data or not.
        /// If HasData == false, it means all members of this interface (other than Address)
        /// are invalid.
        /// </summary>
        bool HasData { get; }

        ulong AppDomain { get; }
        uint OSThreadId { get; }
        int ManagedThreadId { get; }
        uint LockCount { get; }
        ulong Teb { get; }
        ulong ExceptionInFlight { get; }
        bool IsFinalizer { get; }
        bool IsGC { get; }
        GCMode GCMode { get; }
        ClrThreadState ThreadState { get; }
        ulong StackBase { get; }
        ulong StackLimit { get; }

        IEnumerable<StackRootInfo> EnumerateStackRoots(ClrThread thread);
        IEnumerable<StackFrameInfo> EnumerateStackTrace(ClrThread thread, bool includeContext, int maxFrames);
    }

    internal struct StackFrameInfo
    {
        public ulong InstructionPointer { get; set; }
        public ulong StackPointer { get; set; }

        public readonly bool IsInternalFrame => InternalFrameVTable != 0;
        public ulong InternalFrameVTable { get; set; }
        public string? InternalFrameName { get; set; }
        public ulong InnerMethodMethodHandle { get; set; }

        public byte[]? Context { get; set; }
    }

    internal struct StackRootInfo
    {
        public ulong Source { get; set; }
        public ulong StackPointer { get; set; }

        public bool IsInterior { get; set; }
        public bool IsPinned { get; set; }

        public ulong Address { get; set; }
        public ulong Object { get; set; }

        public bool IsEnregistered { get; set; }
        public string? RegisterName { get; set; }
        public int RegisterOffset { get; set; }
    }
}