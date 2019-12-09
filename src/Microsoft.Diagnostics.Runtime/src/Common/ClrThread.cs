// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a managed thread in the target process.  Note this does not wrap purely native threads
    /// in the target process (that is, threads which have never run managed code before).
    /// </summary>
    public abstract class ClrThread
    {
        /// <summary>
        /// Gets the runtime associated with this thread.
        /// </summary>
        public abstract ClrRuntime Runtime { get; }

        /// <summary>
        /// The suspension state of the thread according to the runtime.
        /// </summary>
        public abstract GcMode GcMode { get; }

        /// <summary>
        /// Returns true if this is the finalizer thread.
        /// </summary>
        public abstract bool IsFinalizer { get; }

        /// <summary>
        /// The address of the underlying datastructure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public abstract bool IsAlive { get; }

        /// <summary>
        /// The OS thread id for the thread.
        /// </summary>
        public abstract uint OSThreadId { get; }

        /// <summary>
        /// The managed thread ID (this is equivalent to System.Threading.Thread.ManagedThreadId
        /// in the target process).
        /// </summary>
        public abstract int ManagedThreadId { get; }

        /// <summary>
        /// The AppDomain the thread is running in.
        /// </summary>
        public abstract ClrAppDomain CurrentAppDomain { get; }

        /// <summary>
        /// The number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public abstract uint LockCount { get; }

        /// <summary>
        /// The base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract ulong StackBase { get; }

        /// <summary>
        /// The limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract ulong StackLimit { get; }

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.  This is equivalent to
        /// EnumerateStackObjects(true).
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<ClrStackRoot> EnumerateStackRoots();

        /// <summary>
        /// Enumerates a stack trace for a given thread.  Note this method may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice.  When enumerating frames
        /// out of this method you should be careful to either set a maximum loop count, or to ensure the stack
        /// unwind is making progress by ensuring that ClrStackFrame.StackPointer is making progress (though it
        /// is expected that sometimes two frames may return the same StackPointer in some corner cases).
        /// </summary>
        /// <returns>An enumeration of stack frames.</returns>
        public abstract IEnumerable<ClrStackFrame> EnumerateStackTrace(bool includeContext = false);

        /// <summary>
        /// Returns the exception currently on the thread.  Note that this field may be null.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public abstract ClrException? CurrentException { get; }

        /// <summary>
        /// Returns true if an abort was requested for this thread (such as Thread.Abort, or AppDomain unload).
        /// </summary>
        public abstract bool IsAbortRequested { get; }

        /// <summary>
        /// Returns true if this thread was aborted.
        /// </summary>
        public abstract bool IsAborted { get; }

        /// <summary>
        /// Returns true if the GC is attempting to suspend this thread.
        /// </summary>
        public abstract bool IsGCSuspendPending { get; }

        /// <summary>
        /// Returns true if the user has suspended the thread (using Thread.Suspend).
        /// </summary>
        public abstract bool IsUserSuspended { get; }

        /// <summary>
        /// Returns true if the debugger has suspended the thread.
        /// </summary>
        public abstract bool IsDebugSuspended { get; }

        /// <summary>
        /// Returns true if this thread is a background thread.  (That is, if the thread does not keep the
        /// managed execution environment alive and running.)
        /// </summary>
        public abstract bool IsBackground { get; }

        /// <summary>
        /// Returns true if this thread was created, but not started.
        /// </summary>
        public abstract bool IsUnstarted { get; }

        /// <summary>
        /// Returns true if the Clr runtime called CoIntialize for this thread.
        /// </summary>
        public abstract bool IsCoInitialized { get; }

        /// <summary>
        /// Returns true if this thread is in a COM single threaded apartment.
        /// </summary>
        public abstract bool IsSTA { get; }

        /// <summary>
        /// Returns true if the thread is a COM multithreaded apartment.
        /// </summary>
        public abstract bool IsMTA { get; }
    }
}