// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    /// <summary>
    /// Represents the data and functions for a single thread in the CLR runtime.
    /// </summary>
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

        /// <summary>
        /// The AppDomain that this thread is currently running in.
        /// </summary>
        ulong AppDomain { get; }

        /// <summary>
        /// The thread ID as the Operating System sees it.
        /// </summary>
        uint OSThreadId { get; }

        /// <summary>
        /// The managed thread id.
        /// </summary>
        int ManagedThreadId { get; }

        /// <summary>
        /// The lock count of the Thread, if available.
        /// </summary>
        uint LockCount { get; }

        /// <summary>
        /// The Windows TEB, if available, 0 otherwise.
        /// </summary>
        ulong Teb { get; }

        /// <summary>
        /// The base address range of the stack space for this thread.
        /// </summary>
        ulong StackBase { get; }

        /// <summary>
        /// The limit of the stack space for this thread.
        /// </summary>
        ulong StackLimit { get; }

        /// <summary>
        /// If an exception is in flight on this thread, a pointer directly to
        /// the exception object itself.
        /// </summary>
        ulong ExceptionInFlight { get; }

        /// <summary>
        /// Whether this thread is a finalizer thread or not.
        /// </summary>
        bool IsFinalizer { get; }

        /// <summary>
        /// Whether this thread is a GC thread or not.
        /// </summary>
        bool IsGC { get; }

        /// <summary>
        /// The GCMode of this thread (cooperative, preemptive).
        /// </summary>
        GCMode GCMode { get; }

        /// <summary>
        /// The state of this thread.
        /// </summary>
        ClrThreadState State { get; }

        /// <summary>
        /// Enumerates the roots of this thread.
        /// </summary>
        /// <returns>An enumeration of stack roots.</returns>
        IEnumerable<StackRootInfo> EnumerateStackRoots();

        /// <summary>
        /// Enumerates the stack trace of this method.  Note that in the event of bugs or corrupted
        /// state, this can occasionally produce bad data and run "forever".  Setting <paramref name="maxFrames"/>
        /// to a reasonable number will prevent infinite loops.
        /// </summary>
        /// <param name="includeContext">Whether to calculate and include the thread's CONTEXT record or not.
        /// Registers are always in the Windows CONTEXT format, as that's what the OS uses.</param>
        /// <param name="maxFrames">The maximum number of frames to enumerate.</param>
        /// <returns>An enumeration of stack frames.</returns>
        IEnumerable<StackFrameInfo> EnumerateStackTrace(bool includeContext, int maxFrames);
    }

    /// <summary>
    /// Information about a single stack frame in a stack trace.  This can be a real stack frame as the OS sees
    /// it, or a clr!Frame marker on the stack (internal frame).
    /// </summary>
    internal struct StackFrameInfo
    {
        /// <summary>
        /// The IP of this frame.
        /// </summary>
        public ulong InstructionPointer { get; set; }

        /// <summary>
        /// The SP of this frame.
        /// </summary>
        public ulong StackPointer { get; set; }

        /// <summary>
        /// Whether or not this is a clr!Frame or not.
        /// </summary>
        public readonly bool IsInternalFrame => InternalFrameVTable != 0;

        /// <summary>
        /// The VTable of the Frame.
        /// </summary>
        public ulong InternalFrameVTable { get; set; }

        /// <summary>
        /// The name of the Frame, if available.
        /// </summary>
        public string? InternalFrameName { get; set; }

        /// <summary>
        /// Some clr!Frames have a managed method associated with it.  This is
        /// the method handle (usually MethodDesc) associated with it.
        /// </summary>
        public ulong InnerMethodMethodHandle { get; set; }

        /// <summary>
        /// The thread's CONTEXT record, if requested.
        /// </summary>
        public byte[]? Context { get; set; }
    }

    /// <summary>
    /// Information about a single root on the stack.
    /// </summary>
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