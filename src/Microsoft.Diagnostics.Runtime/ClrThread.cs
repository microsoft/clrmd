// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a managed thread in the target process.  Note this does not wrap purely native threads
    /// in the target process (that is, threads which have never run managed code before).
    /// </summary>
    public sealed class ClrThread : IClrThread
    {
        private readonly IClrThreadHelpers _helpers;
        private readonly ulong _exceptionHandle;

        internal ClrThread(IClrThreadHelpers helpers, ClrRuntime runtime, ClrAppDomain? currentDomain, ulong address, in ThreadData data)
        {
            _helpers = helpers;
            Runtime = runtime;
            Address = address;
            OSThreadId = data.OSThreadId;
            ManagedThreadId = (int)data.ManagedThreadId;
            CurrentAppDomain = currentDomain;
            LockCount = data.LockCount;
            State = (ClrThreadState)data.State;
            _exceptionHandle = data.LastThrownObjectHandle;

            if (data.Teb != 0)
            {
                IMemoryReader reader = _helpers.DataReader;
                uint pointerSize = (uint)reader.PointerSize;
                StackBase = reader.ReadPointer(data.Teb + pointerSize);
                StackLimit = reader.ReadPointer(data.Teb + pointerSize * 2);
            }
            GCMode = data.PreemptiveGCDisabled == 0 ? GCMode.Preemptive : GCMode.Cooperative;
        }
        /// <summary>
        /// Gets the runtime associated with this thread.
        /// </summary>
        public ClrRuntime Runtime { get; }

        IClrRuntime IClrThread.Runtime => Runtime;

        /// <summary>
        /// Gets the suspension state of the thread according to the runtime.
        /// </summary>
        public GCMode GCMode { get; }

        /// <summary>
        /// Gets the address of the underlying datastructure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public ulong Address { get; }

        public ClrThreadState State { get; }

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public bool IsAlive => OSThreadId != 0 && (State & (ClrThreadState.TS_Unstarted | ClrThreadState.TS_Dead)) == 0;

        /// <summary>
        /// Gets the OS thread id for the thread.
        /// </summary>
        public uint OSThreadId { get; }

        /// <summary>
        /// Gets the managed thread ID (this is equivalent to <see cref="System.Threading.Thread.ManagedThreadId"/>
        /// in the target process).
        /// </summary>
        public int ManagedThreadId { get; }

        /// <summary>
        /// Gets the AppDomain the thread is running in.
        /// </summary>
        public ClrAppDomain? CurrentAppDomain { get; }

        IClrAppDomain? IClrThread.CurrentAppDomain => CurrentAppDomain;

        /// <summary>
        /// Gets the number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public uint LockCount { get; }

        /// <summary>
        /// Gets the base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public ulong StackBase { get; }

        /// <summary>
        /// Gets the limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public ulong StackLimit { get; }

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.  The returned IClrRoot may either be an
        /// <see cref="ClrStackRoot"/> or a <see cref="ClrStackInteriorRoot"/>.
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public IEnumerable<IClrStackRoot> EnumerateStackRoots() => _helpers.EnumerateStackRoots(this);

        /// <summary>
        /// Enumerates a stack trace for a given thread.  Note this method may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice.  When enumerating frames
        /// out of this method you should be careful to either set a maximum loop count, or to ensure the stack
        /// unwind is making progress by ensuring that ClrStackFrame.StackPointer is making progress (though it
        /// is expected that sometimes two frames may return the same StackPointer in some corner cases).
        /// </summary>
        /// <returns>An enumeration of stack frames.</returns>
        public IEnumerable<ClrStackFrame> EnumerateStackTrace(bool includeContext = false) => _helpers.EnumerateStackTrace(this, includeContext);

        /// <summary>
        /// Gets the exception currently on the thread.  Note that this field may be <see langword="null"/>.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public ClrException? CurrentException
        {
            get
            {
                ulong ptr = _exceptionHandle;
                if (ptr == 0)
                    return null;

                ulong obj = _helpers.DataReader.ReadPointer(ptr);
                ClrException? ex = Runtime.Heap.GetExceptionObject(obj, this);
                return ex;
            }
        }

        IClrException? IClrThread.CurrentException => CurrentException;
    }
}