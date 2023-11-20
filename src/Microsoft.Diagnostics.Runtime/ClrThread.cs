// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a managed thread in the target process.  Note this does not wrap purely native threads
    /// in the target process (that is, threads which have never run managed code before).
    /// </summary>
    public sealed class ClrThread : IClrThread, IEquatable<ClrThread>
    {
        private const int MaxFrameDefault = 8096;
        private readonly IDataReader _dataReader;
        private readonly IAbstractThreadHelpers? _threadHelpers;
        private ulong _exceptionInFlight;
        private ClrException? _lastThrownException;
        private volatile Cache<ClrStackFrame>? _frameCache;
        private volatile Cache<ClrStackRoot>? _rootCache;

        internal ClrThread(IDataReader dataReader, ClrRuntime runtime, IAbstractThreadHelpers? helpers, in ClrThreadInfo threadInfo)
        {
            _dataReader = dataReader;
            _threadHelpers = helpers;
            Runtime = runtime;
            Address = threadInfo.Address;
            CurrentAppDomain = Runtime.GetAppDomainByAddress(threadInfo.AppDomain);
            OSThreadId = threadInfo.OSThreadId;
            ManagedThreadId = threadInfo.ManagedThreadId;
            LockCount = threadInfo.LockCount;
            StackBase = threadInfo.StackBase;
            StackLimit = threadInfo.StackLimit;
            _exceptionInFlight = threadInfo.ExceptionInFlight;
            IsFinalizer = threadInfo.IsFinalizer;
            IsGc = threadInfo.IsGC;
            GCMode = threadInfo.GCMode;
            State = threadInfo.State;
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
        /// Gets the address of the underlying data structure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The state of this thread.  These flags correspond to the runtime's internal flags on a thread.
        /// </summary>
        public ClrThreadState State { get; }

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public bool IsAlive => OSThreadId != 0 && (State & (ClrThreadState.TS_Unstarted | ClrThreadState.TS_Dead)) == 0;

        /// <summary>
        /// Returns true if a finalizer thread otherwise false.
        /// </summary>
        public bool IsFinalizer { get; }

        /// <summary>
        /// Returns true if a GC thread otherwise false.
        /// </summary>
        public bool IsGc { get; }

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
        /// Enumerates the GC references (objects) on the stack.
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public IEnumerable<ClrStackRoot> EnumerateStackRoots()
        {
            IAbstractThreadHelpers? threadHelpers = _threadHelpers;
            if (threadHelpers is null)
                return Enumerable.Empty<ClrStackRoot>();

            Cache<ClrStackRoot>? cache = _rootCache;
            if (cache is not null)
                return Array.AsReadOnly(cache.Elements);

            ClrHeap heap = Runtime.Heap;
            ClrStackFrame[] frames = GetFramesForRoots();
            IEnumerable<ClrStackRoot> roots = threadHelpers.EnumerateStackRoots(OSThreadId, traceErrors: IsAlive).Select(r => CreateClrStackRoot(heap, frames, r)).Where(r => r is not null)!;
            if (!Runtime.DataTarget.CacheOptions.CacheStackRoots)
                return roots;

            return CacheAndReturnRoots(roots);
        }

        private IEnumerable<ClrStackRoot> CacheAndReturnRoots(IEnumerable<ClrStackRoot> roots)
        {
            List<ClrStackRoot> cache = new();
            foreach (ClrStackRoot root in roots)
            {
                cache.Add(root);
                yield return root;
            }

            // it's ok if we race and replace another cache as this will stabilize eventually
            _rootCache ??= new(cache.ToArray(), false);
        }

        private ClrStackFrame[] GetFramesForRoots()
        {
            IAbstractThreadHelpers? threadHelpers = _threadHelpers;
            if (threadHelpers is null)
                return Array.Empty<ClrStackFrame>();

            Cache<ClrStackFrame>? cache = _frameCache;
            if (cache is not null)
                return cache.Elements;

            // We need to make sure we don't loop forever when enumerating the stack trace.
            // We will only cache the stack if we completed enumeratione (i.e. got less
            // than MaxFrameDefault frames)
            ClrStackFrame[] stack = threadHelpers.EnumerateStackTrace(OSThreadId, includeContext: false, traceErrors: IsAlive).Select(r => CreateClrStackFrame(r)).Take(MaxFrameDefault).ToArray();
            if (Runtime.DataTarget.CacheOptions.CacheStackTraces && stack.Length < MaxFrameDefault)
                _frameCache = new(stack, includedContext: false);

            return stack;
        }

        private ClrStackRoot? CreateClrStackRoot(ClrHeap heap, ClrStackFrame[] stack, StackRootInfo stackRef)
        {
            ClrStackFrame? frame = stack.FirstOrDefault(f =>
                                    f.Kind == ClrStackFrameKind.Runtime ?
                                    f.StackPointer == stackRef.StackPointer || f.StackPointer == stackRef.InternalFrame :
                                    f.InstructionPointer == stackRef.InstructionPointer && f.StackPointer == stackRef.StackPointer);

            frame ??= new ClrStackFrame(this, null, stackRef.InstructionPointer, stackRef.StackPointer, ClrStackFrameKind.Unknown, null, null);

            ClrObject clrObject;
            if (stackRef.IsInterior)
            {
                ulong obj = stackRef.Object;
                ClrSegment? segment = heap.GetSegmentByAddress(obj);

                // If not, this may be a pointer to an object.
                if (segment is null && _dataReader.ReadPointer(obj, out ulong interiorObj))
                {
                    segment = heap.GetSegmentByAddress(interiorObj);
                    if (segment is not null)
                        obj = interiorObj;
                }

                if (segment is null)
                    return null;

                clrObject = heap.FindPreviousObjectOnSegment(obj + 1);
            }
            else
            {
                clrObject = heap.GetObject(stackRef.Object);
            }

            return new ClrStackRoot(stackRef.Address, clrObject, stackRef.IsInterior, stackRef.IsPinned, heap, frame, stackRef.RegisterName, stackRef.RegisterOffset);
        }

        IEnumerable<IClrRoot> IClrThread.EnumerateStackRoots() => EnumerateStackRoots().Cast<IClrRoot>();

        /// <summary>
        /// Enumerates a stack trace for a given thread.  Note this method may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice.  When enumerating frames
        /// out of this method you should be careful to either set a maximum loop count, or to ensure the stack
        /// unwind is making progress by ensuring that ClrStackFrame.StackPointer is making progress (though it
        /// is expected that sometimes two frames may return the same StackPointer in some corner cases).
        /// </summary>
        /// <param name="includeContext">Whether to include a CONTEXT record for the frame.  This is always in
        /// the format of the Windows CONTEXT record (as that's what CLR uses internally, even on non-Windows
        /// platforms.</param>
        /// <returns>An enumeration of stack frames.</returns>
        public IEnumerable<ClrStackFrame> EnumerateStackTrace(bool includeContext = false)
        {
            return EnumerateStackTrace(includeContext, maxFrames: MaxFrameDefault);
        }

        /// <summary>
        /// Enumerates a stack trace for a given thread.  Note this method may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice.  When enumerating frames
        /// out of this method you should be careful to either set a maximum loop count, or to ensure the stack
        /// unwind is making progress by ensuring that ClrStackFrame.StackPointer is making progress (though it
        /// is expected that sometimes two frames may return the same StackPointer in some corner cases).
        /// </summary>
        /// <param name="includeContext">Whether to include a CONTEXT record for the frame.  This is always in
        /// the format of the Windows CONTEXT record (as that's what CLR uses internally, even on non-Windows
        /// platforms.</param>
        /// <param name="maxFrames">The maximum number of stack frames to return.  It's important to cap the
        /// stack trace because sometimes bugs in the debugging layer or corruption in the target process
        /// can cause us to produce an infinite amount of stack frames.</param>
        /// <returns>An enumeration of stack frames.</returns>
        public IEnumerable<ClrStackFrame> EnumerateStackTrace(bool includeContext, int maxFrames)
        {
            IAbstractThreadHelpers? threadHelpers = _threadHelpers;
            if (threadHelpers is null)
                return Array.Empty<ClrStackFrame>();

            Cache<ClrStackFrame>? cache = _frameCache;
            if (cache is not null && (!includeContext || cache.IncludedContext))
                return Array.AsReadOnly(cache.Elements);

            IEnumerable<ClrStackFrame> frames = threadHelpers.EnumerateStackTrace(OSThreadId, includeContext, traceErrors: IsAlive).Select(r => CreateClrStackFrame(r));
            if (!Runtime.DataTarget.CacheOptions.CacheStackTraces)
                return frames;

            return CacheAndReturnFrames(includeContext, frames);
        }

        private IEnumerable<ClrStackFrame> CacheAndReturnFrames(bool includeContext, IEnumerable<ClrStackFrame> frames)
        {
            // Only cache frames if enumeration completed and the user didn't break out of the loop
            List<ClrStackFrame> cachedFrames = new();
            foreach (ClrStackFrame frame in frames)
            {
                cachedFrames.Add(frame);
                yield return frame;
            }

            // it's ok if we race and replace another cache as this will stabilize eventually
            _frameCache ??= new(cachedFrames.ToArray(), includeContext);
        }

        private ClrStackFrame CreateClrStackFrame(in StackFrameInfo frame)
        {
            ClrStackFrameKind kind;
            ClrMethod? method;
            if (frame.IsInternalFrame)
            {
                kind = ClrStackFrameKind.Runtime;
                method = frame.InnerMethodMethodHandle != 0 ? Runtime.GetMethodByHandle(frame.InnerMethodMethodHandle) : null;
            }
            else
            {
                kind = ClrStackFrameKind.ManagedMethod;
                method = Runtime.GetMethodByInstructionPointer(frame.InstructionPointer);
            }

            return new ClrStackFrame(this, frame.Context, frame.InstructionPointer, frame.StackPointer, kind, method, frame.InternalFrameName);
        }

        IEnumerable<IClrStackFrame> IClrThread.EnumerateStackTrace(bool includeContext) => EnumerateStackTrace(includeContext).Cast<IClrStackFrame>();

        public bool Equals(IClrThread? other)
        {
            return other is not null && other.Address == Address && other.OSThreadId == OSThreadId && other.ManagedThreadId == ManagedThreadId;
        }

        public bool Equals(ClrThread? other)
        {
            return other is not null && other.Address == Address && other.OSThreadId == OSThreadId && other.ManagedThreadId == ManagedThreadId;
        }

        public override bool Equals(object? obj)
        {
            if (obj is IClrThread thread)
                return thread.Equals(this);

            return false;
        }

        public override int GetHashCode() => Address.GetHashCode() ^ OSThreadId.GetHashCode() & ManagedThreadId.GetHashCode();

        /// <summary>
        /// Gets the exception currently on the thread.  Note that this field may be <see langword="null"/>.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public ClrException? CurrentException => _lastThrownException ??= _exceptionInFlight != 0 ? Runtime.Heap.GetExceptionObject(_exceptionInFlight, this) : null;

        IClrException? IClrThread.CurrentException => CurrentException;

        private sealed class Cache<T>
            where T : class
        {
            public T[] Elements { get; }
            public bool IncludedContext { get; }

            public Cache(T[] elements, bool includedContext)
            {
                Elements = elements;
                IncludedContext = includedContext;
            }
        }
    }
}