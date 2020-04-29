// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdThread : ClrThread
    {
        private readonly IThreadHelpers _helpers;
        private readonly int _threadState;
        private readonly ulong _exceptionHandle;

        public override ClrRuntime Runtime { get; }
        public override ulong Address { get; }
        public override bool IsFinalizer { get; }
        public override GCMode GCMode { get; }
        public override uint OSThreadId { get; }
        public override int ManagedThreadId { get; }
        public override ClrAppDomain CurrentAppDomain { get; }
        public override uint LockCount { get; }
        public override ulong StackBase { get; }
        public override ulong StackLimit { get; }

        public ClrmdThread(IThreadData data, ClrRuntime runtime, ClrAppDomain currentDomain)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _helpers = data.Helpers;
            Runtime = runtime;
            Address = data.Address;
            IsFinalizer = data.IsFinalizer;
            OSThreadId = data.OSThreadID;
            ManagedThreadId = data.ManagedThreadID;
            CurrentAppDomain = currentDomain;
            LockCount = data.LockCount;
            _threadState = data.State;
            _exceptionHandle = data.ExceptionHandle;
            StackBase = data.StackBase;
            StackLimit = data.StackLimit;
            GCMode = data.Preemptive ? GCMode.Preemptive : GCMode.Cooperative;
        }

        public override ClrException? CurrentException
        {
            get
            {
                ulong ptr = _exceptionHandle;
                if (ptr == 0)
                    return null;

                ulong obj = _helpers.DataReader.ReadPointer(ptr);
                ulong mt = 0;
                if (obj != 0)
                    mt = _helpers.DataReader.ReadPointer(obj);

                if (mt != 0)
                {
                    ClrType? type = _helpers.Factory.GetOrCreateType(mt, obj);
                    if (type != null)
                        return new ClrException(_helpers.ExceptionHelpers, this, new ClrObject(obj, type));
                }

                return null;
            }
        }

        public override IEnumerable<IClrStackRoot> EnumerateStackRoots() => _helpers.EnumerateStackRoots(this);
        public override IEnumerable<ClrStackFrame> EnumerateStackTrace(bool includeContext) => _helpers.EnumerateStackTrace(this, includeContext);

        public override bool IsAborted => (_threadState & (int)ThreadState.TS_Aborted) == (int)ThreadState.TS_Aborted;
        public override bool IsGCSuspendPending => (_threadState & (int)ThreadState.TS_GCSuspendPending) == (int)ThreadState.TS_GCSuspendPending;
        public override bool IsUserSuspended => (_threadState & (int)ThreadState.TS_UserSuspendPending) == (int)ThreadState.TS_UserSuspendPending;
        public override bool IsDebugSuspended => (_threadState & (int)ThreadState.TS_DebugSuspendPending) == (int)ThreadState.TS_DebugSuspendPending;
        public override bool IsBackground => (_threadState & (int)ThreadState.TS_Background) == (int)ThreadState.TS_Background;
        public override bool IsUnstarted => (_threadState & (int)ThreadState.TS_Unstarted) == (int)ThreadState.TS_Unstarted;
        public override bool IsCoInitialized => (_threadState & (int)ThreadState.TS_CoInitialized) == (int)ThreadState.TS_CoInitialized;
        public override bool IsSTA => (_threadState & (int)ThreadState.TS_InSTA) == (int)ThreadState.TS_InSTA;
        public override bool IsMTA => (_threadState & (int)ThreadState.TS_InMTA) == (int)ThreadState.TS_InMTA;

        public override bool IsAbortRequested =>
            (_threadState & (int)ThreadState.TS_AbortRequested) == (int)ThreadState.TS_AbortRequested
            || (_threadState & (int)ThreadState.TS_AbortInitiated) == (int)ThreadState.TS_AbortInitiated;

        public override bool IsAlive => OSThreadId != 0 && (_threadState & ((int)ThreadState.TS_Unstarted | (int)ThreadState.TS_Dead)) == 0;

        internal enum TlsThreadType
        {
            ThreadType_GC = 0x00000001,
            ThreadType_Timer = 0x00000002,
            ThreadType_Gate = 0x00000004,
            ThreadType_DbgHelper = 0x00000008,
            // ThreadType_Shutdown = 0x00000010,
            ThreadType_DynamicSuspendEE = 0x00000020,
            // ThreadType_Finalizer = 0x00000040,
            // ThreadType_ADUnloadHelper = 0x00000200,
            ThreadType_ShutdownHelper = 0x00000400,
            ThreadType_Threadpool_IOCompletion = 0x00000800,
            ThreadType_Threadpool_Worker = 0x00001000,
            ThreadType_Wait = 0x00002000
        }

        private enum ThreadState
        {
            // TS_Unknown                = 0x00000000,    // threads are initialized this way

            TS_AbortRequested = 0x00000001, // Abort the thread
            TS_GCSuspendPending = 0x00000002, // waiting to get to safe spot for GC
            TS_UserSuspendPending = 0x00000004, // user suspension at next opportunity
            TS_DebugSuspendPending = 0x00000008, // Is the debugger suspending threads?
            // TS_GCOnTransitions        = 0x00000010,    // Force a GC on stub transitions (GCStress only)

            // TS_LegalToJoin            = 0x00000020,    // Is it now legal to attempt a Join()
            // TS_YieldRequested         = 0x00000040,    // The task should yield
            // TS_Hijacked               = 0x00000080,    // Return address has been hijacked
            // TS_BlockGCForSO           = 0x00000100,    // If a thread does not have enough stack, WaitUntilGCComplete may fail.
            // Either GC suspension will wait until the thread has cleared this bit,
            // Or the current thread is going to spin if GC has suspended all threads.
            TS_Background = 0x00000200, // Thread is a background thread
            TS_Unstarted = 0x00000400, // Thread has never been started
            TS_Dead = 0x00000800, // Thread is dead

            // TS_WeOwn                  = 0x00001000,    // Exposed object initiated this thread
            TS_CoInitialized = 0x00002000, // CoInitialize has been called for this thread

            TS_InSTA = 0x00004000, // Thread hosts an STA
            TS_InMTA = 0x00008000, // Thread is part of the MTA

            // Some bits that only have meaning for reporting the state to clients.
            // TS_ReportDead             = 0x00010000,    // in WaitForOtherThreads()

            // TS_TaskReset              = 0x00040000,    // The task is reset

            // TS_SyncSuspended          = 0x00080000,    // Suspended via WaitSuspendEvent
            // TS_DebugWillSync          = 0x00100000,    // Debugger will wait for this thread to sync

            // TS_StackCrawlNeeded       = 0x00200000,    // A stackcrawl is needed on this thread, such as for thread abort
            // See comment for s_pWaitForStackCrawlEvent for reason.

            // TS_SuspendUnstarted       = 0x00400000,    // latch a user suspension on an unstarted thread

            TS_Aborted = 0x00800000, // is the thread aborted?
            TS_TPWorkerThread = 0x01000000, // is this a threadpool worker thread?

            // TS_Interruptible          = 0x02000000,    // sitting in a Sleep(), Wait(), Join()
            // TS_Interrupted            = 0x04000000,    // was awakened by an interrupt APC. !!! This can be moved to TSNC

            TS_CompletionPortThread = 0x08000000, // Completion port thread

            TS_AbortInitiated = 0x10000000 // set when abort is begun

            // TS_Finalized              = 0x20000000,    // The associated managed Thread object has been finalized.
            // We can clean up the unmanaged part now.

            // TS_FailStarted            = 0x40000000,    // The thread fails during startup.
            // TS_Detached               = 0x80000000,    // Thread was detached by DllMain
        }
    }
}