// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Helpers for ClrThreads.
    ///
    /// This interface is optional, but without it we cannot enumerate
    /// stack traces or roots.
    /// </summary>
    internal interface IAbstractThreadProvider
    {
        /// <summary>
        /// Enumerates the roots of this thread.
        /// </summary>
        /// <param name="osThreadId">The thread to enumerate.</param>
        /// <returns>An enumeration of stack roots.</returns>
        IEnumerable<StackRootInfo> EnumerateStackRoots(uint osThreadId);

        /// <summary>
        /// Enumerates the stack trace of this method.  Note that in the event of bugs or corrupted
        /// state, this can occasionally produce bad data and run "forever", so be sure to break out of
        /// the loop when a threshold is reached when calling it.
        /// </summary>
        /// <param name="osThreadId">The thread to enumerate.</param>
        /// <param name="includeContext">Whether to calculate and include the thread's CONTEXT record or not.
        /// Registers are always in the Windows CONTEXT format, as that's what the OS uses.</param>
        /// <returns>An enumeration of stack frames.</returns>
        IEnumerable<StackFrameInfo> EnumerateStackTrace(uint osThreadId, bool includeContext);
    }

    /// <summary>
    /// Information about a single coreclr!Thread.
    /// </summary>
    internal struct ClrThreadInfo
    {
        /// <summary>
        /// The address of the underlying coreclr!Thread.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The AppDomain that this thread is currently running in.
        /// </summary>
        public ulong AppDomain { get; set; }

        /// <summary>
        /// The thread ID as the Operating System sees it.
        /// </summary>
        public uint OSThreadId { get; set; }

        /// <summary>
        /// The managed thread id.
        /// </summary>
        public int ManagedThreadId { get; set; }

        /// <summary>
        /// The lock count of the Thread, if available.
        /// </summary>
        public uint LockCount { get; set; }

        /// <summary>
        /// The Windows TEB, if available, 0 otherwise.
        /// </summary>
        public ulong Teb { get; set; }

        /// <summary>
        /// The base address range of the stack space for this thread.
        /// </summary>
        public ulong StackBase { get; set; }

        /// <summary>
        /// The limit of the stack space for this thread.
        /// </summary>
        public ulong StackLimit { get; set; }

        /// <summary>
        /// If an exception is in flight on this thread, a pointer directly to
        /// the exception object itself.
        /// </summary>
        public ulong ExceptionInFlight { get; set; }

        /// <summary>
        /// Whether this thread is a finalizer thread or not.
        /// </summary>
        public bool IsFinalizer { get; set; }

        /// <summary>
        /// Whether this thread is a GC thread or not.
        /// </summary>
        public bool IsGC { get; set; }

        /// <summary>
        /// The GCMode of this thread (cooperative, preemptive).
        /// </summary>
        public GCMode GCMode { get; set; }

        /// <summary>
        /// The state of this thread.
        /// </summary>
        public ClrThreadState State { get; set; }
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
    /// Information about a single root on the stack.  For this to work properly, we need
    /// these things to be true:
    ///     OS Stack Frames - IP and SP should match what the OS stack unwinder says where this
    ///                       stack frame is.
    ///     coreclr!Frames  - The StackPointer or the InternalFrame must match the direct
    ///                       pointer to the clr!Frame object
    /// </summary>
    internal struct StackRootInfo
    {
        /// <summary>
        /// The IP associated with this root.  This may be 0 if the root comes from a
        /// clr!Frame.
        /// </summary>
        public ulong InstructionPointer { get; set; }

        /// <summary>
        /// The StackPointer associated with this root.  This may be the SP of the actual
        /// OS frame on the stack or it may be the pointer of the data on the stack.
        /// </summary>
        public ulong StackPointer { get; set; }

        /// <summary>
        /// The coreclr!Frame this root came from, or 0.
        /// </summary>
        public ulong InternalFrame { get; set; }

        /// <summary>
        /// Whether the pointer is "interior" or not.  Interior pointers need not point
        /// to the beginning of the object, and don't even need to point to the GC heap.
        /// </summary>
        public bool IsInterior { get; set; }

        /// <summary>
        /// Whether the pointer is pinned or not.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// The address of the stack slot this root is contained in (maybe 0 if the object is
        /// enregistered).
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// A pointer to the object itself.  This pointer may not point to the beginning of an
        /// object, or even to the GC heap itself if IsInterior == true.
        /// </summary>
        public ulong Object { get; set; }

        /// <summary>
        /// Whether this root is in the thread's register CONTEXT for the frame.
        /// </summary>
        public bool IsEnregistered { get; set; }

        /// <summary>
        /// The register name, if available.
        /// </summary>
        public string? RegisterName { get; set; }

        /// <summary>
        /// The offset from the register.
        /// </summary>
        public int RegisterOffset { get; set; }
    }
}