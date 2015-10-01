// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Utilities;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The type of frame the ClrStackFrame represents.
    /// </summary>
    public enum ClrStackFrameType
    {
        /// <summary>
        /// Indicates this stack frame is a standard managed method.
        /// </summary>
        ManagedMethod,

        /// <summary>
        /// Indicates this stack frame is a special stack marker that the Clr runtime leaves on the stack.
        /// Note that the ClrStackFrame may still have a ClrMethod associated with the marker.
        /// </summary>
        Runtime
    }

    /// <summary>
    /// A frame in a managed stack trace.  Note you can call ToString on an instance of this object to get the
    /// function name (or clr!Frame name) similar to SOS's !clrstack output.
    /// </summary>
    public abstract class ClrStackFrame
    {
        /// <summary>
        /// The instruction pointer of this frame.
        /// </summary>
        public abstract Address InstructionPointer { get; }

        /// <summary>
        /// The stack pointer of this frame.
        /// </summary>
        public abstract Address StackPointer { get; }

        /// <summary>
        /// The type of frame (managed or internal).
        /// </summary>
        public abstract ClrStackFrameType Kind { get; }

        /// <summary>
        /// The string to display in a stack trace.  Similar to !clrstack output.
        /// </summary>
        public abstract string DisplayString { get; }

        /// <summary>
        /// Returns the ClrMethod which corresponds to the current stack frame.  This may be null if the
        /// current frame is actually a CLR "Internal Frame" representing a marker on the stack, and that
        /// stack marker does not have a managed method associated with it.
        /// </summary>
        public abstract ClrMethod Method { get; }

        /// <summary>
        /// Returns the source file and line number of the location represented by this stack frame.
        /// This will return null if the location cannot be determined (or the module containing it does
        /// not have PDBs loaded).
        /// </summary>
        /// <returns>The file and line number for this stack frame, null if not found.</returns>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual SourceLocation GetFileAndLineNumber() { return null; }

        /// <summary>
        /// Returns the module name to use for building the stack trace.
        /// </summary>
        public virtual string ModuleName
        {
            get
            {
                if (Method == null || Method.Type == null || Method.Type.Module == null)
                    return UnknownModuleName;

                string result = Method.Type.Module.Name;
                try
                {
                    return Path.GetFileNameWithoutExtension(result);
                }
                catch
                {
                    return result;
                }
            }
        }

        /// <summary>
        /// The default name used when a module name cannot be calculated.
        /// </summary>
        public static string UnknownModuleName = "UNKNOWN";
    }

    /// <summary>
    /// A SourceLocation represents a point in the source code.  That is the file and the line number.  
    /// </summary>
    [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
    public class SourceLocation
    {
        /// <summary>
        /// The source file for the code
        /// </summary>
        public string FilePath { get { return null; } }

        /// <summary>
        /// The line number for the code.
        /// </summary>
        public int LineNumber { get { return 0; } }

        /// <summary>
        /// The end line number of the location (if multiline this will be different from LineNumber).
        /// </summary>
        public int LineNumberEnd { get { return 0; } }

        /// <summary>
        /// The start column of the source line.
        /// </summary>
        public int ColStart { get { return 0; } }

        /// <summary>
        /// The end column of the source line.
        /// </summary>
        public int ColEnd { get { return 0; } }

        /// <summary>
        /// Generates a human readable form of the source location.
        /// </summary>
        /// <returns>File:Line-Line</returns>
        public override string ToString()
        {
            int line = LineNumber;
            int lineEnd = LineNumberEnd;

            if (line == lineEnd)
                return string.Format("{0}:{1}", FilePath, line);
            else
                return string.Format("{0}:{1}-{2}", FilePath, line, lineEnd);
        }
    }

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
        public abstract Address Address { get; }

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
        public abstract Address AppDomain { get; }

        /// <summary>
        /// The number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public abstract uint LockCount { get; }

        /// <summary>
        /// The TEB (thread execution block) address in the process.
        /// </summary>
        public abstract Address Teb { get; }

        /// <summary>
        /// The base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract Address StackBase { get; }

        /// <summary>
        /// The limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract Address StackLimit { get; }

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.  This is equivalent to
        /// EnumerateStackObjects(true).
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<ClrRoot> EnumerateStackObjects();

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.
        /// </summary>
        /// <param name="includePossiblyDead">Include all objects found on the stack.  Passing
        /// false attempts to replicate the behavior of the GC, reporting only live objects.</param>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead);

        /// <summary>
        /// Returns the managed stack trace of the thread.  Note that this property may return incomplete
        /// data in the case of a bad stack unwind or if there is a very large number of methods on the stack.
        /// (This is usually caused by a stack overflow on the target thread, stack corruption which leads to
        /// a bad stack unwind, or other inconsistent state in the target debuggee.)
        /// 
        /// Note: This property uses a heuristic to attempt to detect bad unwinds to stop enumerating
        /// frames by inspecting the stack pointer and instruction pointer of each frame to ensure the stack
        /// walk is "making progress".  Additionally we cap the number of frames returned by this method
        /// as another safegaurd.  This means we may not have all frames even if the stack walk was making
        /// progress.
        /// 
        /// If you want to ensure that you receive an un-clipped stack trace, you should use EnumerateStackTrace
        /// instead of this property, and be sure to handle the case of repeating stack frames.
        /// </summary>
        public abstract IList<ClrStackFrame> StackTrace { get; }

        /// <summary>
        /// Enumerates a stack trace for a given thread.  Note this method may loop infinitely in the case of
        /// stack corruption or other stack unwind issues which can happen in practice.  When enumerating frames
        /// out of this method you should be careful to either set a maximum loop count, or to ensure the stack
        /// unwind is making progress by ensuring that ClrStackFrame.StackPointer is making progress (though it
        /// is expected that sometimes two frames may return the same StackPointer in some corner cases).
        /// </summary>
        /// <returns>An enumeration of stack frames.</returns>
        public abstract IEnumerable<ClrStackFrame> EnumerateStackTrace();

        /// <summary>
        /// Returns the exception currently on the thread.  Note that this field may be null.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public abstract ClrException CurrentException { get; }


        /// <summary>
        /// Returns if this thread is a GC thread.  If the runtime is using a server GC, then there will be
        /// dedicated GC threads, which this will indicate.  For a runtime using the workstation GC, this flag
        /// will only be true for a thread which is currently running a GC (and the background GC thread).
        /// </summary>
        public abstract bool IsGC { get; }

        /// <summary>
        /// Returns if this thread is the debugger helper thread.
        /// </summary>
        public abstract bool IsDebuggerHelper { get; }

        /// <summary>
        /// Returns true if this thread is a threadpool timer thread.
        /// </summary>
        public abstract bool IsThreadpoolTimer { get; }

        /// <summary>
        /// Returns true if this thread is a threadpool IO completion port.
        /// </summary>
        public abstract bool IsThreadpoolCompletionPort { get; }

        /// <summary>
        /// Returns true if this is a threadpool worker thread.
        /// </summary>
        public abstract bool IsThreadpoolWorker { get; }

        /// <summary>
        /// Returns true if this is a threadpool wait thread.
        /// </summary>
        public abstract bool IsThreadpoolWait { get; }

        /// <summary>
        /// Returns true if this is the threadpool gate thread.
        /// </summary>
        public abstract bool IsThreadpoolGate { get; }

        /// <summary>
        /// Returns if this thread currently suspending the runtime.
        /// </summary>
        public abstract bool IsSuspendingEE { get; }

        /// <summary>
        /// Returns true if this thread is currently the thread shutting down the runtime.
        /// </summary>
        public abstract bool IsShutdownHelper { get; }

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

        /// <summary>
        /// Returns the object this thread is blocked waiting on, or null if the thread is not blocked.
        /// </summary>
        public abstract IList<BlockingObject> BlockingObjects { get; }
    }

}