// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;
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
        private readonly IClrThreadData _threadData;
        private ClrAppDomain? _currentDomain;
        private ClrException? _lastThrownException;
        private volatile Cache<ClrStackFrame>? _frameCache;
        private volatile Cache<ClrStackRoot>? _rootCache;

        internal ClrThread(IDataReader dataReader, ClrRuntime runtime, IClrThreadData data)
        {
            _dataReader = dataReader;
            _threadData = data;
            Runtime = runtime;
        }

        /// <summary>
        /// Gets the runtime associated with this thread.
        /// </summary>
        public ClrRuntime Runtime { get; }

        IClrRuntime IClrThread.Runtime => Runtime;

        /// <summary>
        /// Gets the suspension state of the thread according to the runtime.
        /// </summary>
        public GCMode GCMode => _threadData.GCMode;

        /// <summary>
        /// Gets the address of the underlying data structure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public ulong Address => _threadData.Address;

        public ClrThreadState State => _threadData.ThreadState;

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public bool IsAlive => OSThreadId != 0 && (State & (ClrThreadState.TS_Unstarted | ClrThreadState.TS_Dead)) == 0;

        /// <summary>
        /// Returns true if a finalizer thread otherwise false.
        /// </summary>
        public bool IsFinalizer => _threadData.IsFinalizer;

        /// <summary>
        /// Returns true if a GC thread otherwise false.
        /// </summary>
        public bool IsGc => _threadData.IsGC;

        /// <summary>
        /// Gets the OS thread id for the thread.
        /// </summary>
        public uint OSThreadId => _threadData.OSThreadId;

        /// <summary>
        /// Gets the managed thread ID (this is equivalent to <see cref="System.Threading.Thread.ManagedThreadId"/>
        /// in the target process).
        /// </summary>
        public int ManagedThreadId => _threadData.ManagedThreadId;

        /// <summary>
        /// Gets the AppDomain the thread is running in.
        /// </summary>
        public ClrAppDomain? CurrentAppDomain
        {
            get
            {
                if (_currentDomain is not null || _threadData.AppDomain == 0)
                    return _currentDomain;

                return _currentDomain = Runtime.GetAppDomainByAddress(_threadData.AppDomain);
            }
        }

        IClrAppDomain? IClrThread.CurrentAppDomain => CurrentAppDomain;

        /// <summary>
        /// Gets the number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public uint LockCount => _threadData.LockCount;

        /// <summary>
        /// Gets the base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public ulong StackBase => _threadData.StackBase;

        /// <summary>
        /// Gets the limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public ulong StackLimit => _threadData.StackLimit;

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public IEnumerable<ClrStackRoot> EnumerateStackRoots()
        {
            Cache<ClrStackRoot>? cache = _rootCache;
            if (cache is not null)
                return Array.AsReadOnly(cache.Elements);

            ClrHeap heap = Runtime.Heap;
            ClrStackFrame[] frames = GetFramesForRoots();
            IEnumerable<ClrStackRoot> roots = _threadData.EnumerateStackRoots(this).Select(r => CreateClrStackRoot(heap, frames, r)).Where(r => r is not null)!;
            if (!Runtime.DataTarget.CacheOptions.CacheStackRoots)
                return roots;

            // it's ok if we race and replace another cache as this will stabilize eventually
            ClrStackRoot[] rootArray = roots.ToArray();
            _rootCache = new(rootArray, false, 0);
            return Array.AsReadOnly(rootArray);
        }

        private ClrStackFrame[] GetFramesForRoots()
        {
            Cache<ClrStackFrame>? cache = _frameCache;
            if (cache is not null)
                return cache.Elements;

            ClrStackFrame[] stack = _threadData.EnumerateStackTrace(this, includeContext: false, maxFrames: MaxFrameDefault).Select(r => CreateClrStackFrame(r)).ToArray();

            if (Runtime.DataTarget.CacheOptions.CacheStackTraces)
                _frameCache = new(stack, includedContext: false, maxFrames: MaxFrameDefault);

            return stack;
        }

        private ClrStackRoot? CreateClrStackRoot(ClrHeap heap, ClrStackFrame[] stack, StackRootInfo stackRef)
        {
            ClrStackFrame? frame = stack.FirstOrDefault(f => f.StackPointer == stackRef.Source || f.StackPointer == stackRef.StackPointer && f.InstructionPointer == stackRef.Source);
            frame ??= new ClrStackFrame(this, null, stackRef.Source, stackRef.StackPointer, ClrStackFrameKind.Unknown, null, null);

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
            Cache<ClrStackFrame>? cache = _frameCache;
            if (cache is not null && (!includeContext || cache.IncludedContext) && maxFrames <= cache.MaxFrames)
                return Array.AsReadOnly(cache.Elements);

            IEnumerable<ClrStackFrame> frames = _threadData.EnumerateStackTrace(this, includeContext, maxFrames).Select(r => CreateClrStackFrame(r));
            if (!Runtime.DataTarget.CacheOptions.CacheStackTraces)
                return frames;

            // it's ok if we race and replace another cache as this will stabilize eventually
            ClrStackFrame[] frameArray = frames.ToArray();
            _frameCache = new(frameArray, includeContext, maxFrames);
            return Array.AsReadOnly(frameArray);
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
        public ClrException? CurrentException => _lastThrownException ??= _threadData.ExceptionInFlight != 0 ? Runtime.Heap.GetExceptionObject(_threadData.ExceptionInFlight, this) : null;

        IClrException? IClrThread.CurrentException => CurrentException;

        private sealed class Cache<T>
            where T : class
        {
            public T[] Elements { get; }
            public bool IncludedContext { get; }
            public int MaxFrames { get; }

            public Cache(T[] elements, bool includedContext, int maxFrames)
            {
                Elements = elements;
                IncludedContext = includedContext;
                MaxFrames = maxFrames;
            }
        }
    }
}