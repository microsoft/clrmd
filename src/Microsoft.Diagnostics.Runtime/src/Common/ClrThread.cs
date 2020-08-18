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
        /// Gets the suspension state of the thread according to the runtime.
        /// </summary>
        public abstract GCMode GCMode { get; }

        /// <summary>
        /// Gets a value indicating whether this is the finalizer thread.
        /// </summary>
        public abstract bool IsFinalizer { get; }

        /// <summary>
        /// Gets the address of the underlying datastructure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public abstract bool IsAlive { get; }

        /// <summary>
        /// Gets the OS thread id for the thread.
        /// </summary>
        public abstract uint OSThreadId { get; }

        /// <summary>
        /// Gets the managed thread ID (this is equivalent to <see cref="System.Threading.Thread.ManagedThreadId"/>
        /// in the target process).
        /// </summary>
        public abstract int ManagedThreadId { get; }

        /// <summary>
        /// Gets the AppDomain the thread is running in.
        /// </summary>
        public abstract ClrAppDomain CurrentAppDomain { get; }

        /// <summary>
        /// Gets the number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public abstract uint LockCount { get; }

        /// <summary>
        /// Gets the base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract ulong StackBase { get; }

        /// <summary>
        /// Gets the limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract ulong StackLimit { get; }

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.  The returned IClrRoot may either be an
        /// <see cref="ClrStackRoot"/> or a <see cref="ClrStackInteriorRoot"/>.
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<IClrStackRoot> EnumerateStackRoots();

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
        /// Gets the exception currently on the thread.  Note that this field may be <see langword="null"/>.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public abstract ClrException? CurrentException { get; }

        /// <summary>
        /// Gets a value indicating whether an abort was requested for this thread (such as <see cref="System.Threading.Thread.Abort()"/>, or <see cref="System.AppDomain.Unload"/>).
        /// </summary>
        public abstract bool IsAbortRequested { get; }

        /// <summary>
        /// Gets a value indicating whether this thread was aborted.
        /// </summary>
        public abstract bool IsAborted { get; }

        /// <summary>
        /// Gets a value indicating whether the GC is attempting to suspend this thread.
        /// </summary>
        public abstract bool IsGCSuspendPending { get; }

        /// <summary>
        /// Gets a value indicating whether the user has suspended the thread (using <see cref="System.Threading.Thread.Suspend"/>).
        /// </summary>
        public abstract bool IsUserSuspended { get; }

        /// <summary>
        /// Gets a value indicating whether the debugger has suspended the thread.
        /// </summary>
        public abstract bool IsDebugSuspended { get; }

        /// <summary>
        /// Gets a value indicating whether this thread is a background thread.  (That is, if the thread does not keep the
        /// managed execution environment alive and running.)
        /// </summary>
        public abstract bool IsBackground { get; }

        /// <summary>
        /// Gets a value indicating whether this thread was created, but not started.
        /// </summary>
        public abstract bool IsUnstarted { get; }

        /// <summary>
        /// Gets a value indicating whether the CLR called <c>CoInitialize</c> for this thread.
        /// </summary>
        public abstract bool IsCoInitialized { get; }

        /// <summary>
        /// Gets a value indicating whether this thread is in a COM single threaded apartment.
        /// </summary>
        public abstract bool IsSTA { get; }

        /// <summary>
        /// Gets a value indicating whether the thread is a COM multithreaded apartment.
        /// </summary>
        public abstract bool IsMTA { get; }
    }
}