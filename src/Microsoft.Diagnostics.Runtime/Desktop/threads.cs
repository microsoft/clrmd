// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopStackFrame : ClrStackFrame
    {
        public override Address StackPointer
        {
            get { return _sp; }
        }

        public override Address InstructionPointer
        {
            get { return _ip; }
        }

        public override ClrStackFrameType Kind
        {
            get { return _type; }
        }

        public override string DisplayString
        {
            get { return _frameName; }
        }

        public override ClrMethod Method
        {
            get
            {
                if (_method == null && _ip != 0 && _type == ClrStackFrameType.ManagedMethod)
                    _method = _runtime.GetMethodByAddress(_ip);

                return _method;
            }
        }

        public override string ToString()
        {
            if (_type == ClrStackFrameType.ManagedMethod)
                return _frameName;

            int methodLen = 0;
            int methodTypeLen = 0;

            if (_method != null)
            {
                methodLen = _method.Name.Length;
                if (_method.Type != null)
                    methodTypeLen = _method.Type.Name.Length;
            }

            StringBuilder sb = new StringBuilder(_frameName.Length + methodLen + methodTypeLen + 10);

            sb.Append('[');
            sb.Append(_frameName);
            sb.Append(']');

            if (_method != null)
            {
                sb.Append(" (");

                if (_method.Type != null)
                {
                    sb.Append(_method.Type.Name);
                    sb.Append('.');
                }

                sb.Append(_method.Name);
                sb.Append(')');
            }

            return sb.ToString();
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong ip, ulong sp, ulong md)
        {
            _runtime = runtime;
            _ip = ip;
            _sp = sp;
            _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
            _type = ClrStackFrameType.ManagedMethod;

            InitMethod(md);
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong sp, ulong md)
        {
            _runtime = runtime;
            _sp = sp;
            _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
            _type = ClrStackFrameType.Runtime;

            InitMethod(md);
        }

        public DesktopStackFrame(DesktopRuntimeBase runtime, ulong sp, string method, ClrMethod innerMethod)
        {
            _runtime = runtime;
            _sp = sp;
            _frameName = method ?? "Unknown";
            _type = ClrStackFrameType.Runtime;
            _method = innerMethod;
        }

        private void InitMethod(ulong md)
        {
            if (_method != null)
                return;

            if (_ip != 0 && _type == ClrStackFrameType.ManagedMethod)
            {
                _method = _runtime.GetMethodByAddress(_ip);
            }
            else if (md != 0)
            {
                IMethodDescData mdData = _runtime.GetMethodDescData(md);
                _method = DesktopMethod.Create(_runtime, mdData);
            }
        }

        private ulong _ip, _sp;
        private string _frameName;
        private ClrStackFrameType _type;
        private ClrMethod _method;
        private DesktopRuntimeBase _runtime;
    }

    internal class DesktopThread : ClrThread
    {
        public override ClrRuntime Runtime
        {
            get
            {
                return _runtime;
            }
        }

        public override Address Address
        {
            get { return _address; }
        }

        public override bool IsFinalizer
        {
            get { return _finalizer; }
        }

        public override ClrException CurrentException
        {
            get
            {
                ulong ex = _exception;
                if (ex == 0)
                    return null;

                if (!_runtime.ReadPointer(ex, out ex) || ex == 0)
                    return null;

                return _runtime.GetHeap().GetExceptionObject(ex);
            }
        }

        public override bool IsGC
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_GC) == (int)TlsThreadType.ThreadType_GC; }
        }

        public override bool IsDebuggerHelper
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_DbgHelper) == (int)TlsThreadType.ThreadType_DbgHelper; }
        }

        public override bool IsThreadpoolTimer
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Timer) == (int)TlsThreadType.ThreadType_Timer; }
        }

        public override bool IsThreadpoolCompletionPort
        {
            get
            {
                return (ThreadType & (int)TlsThreadType.ThreadType_Threadpool_IOCompletion) == (int)TlsThreadType.ThreadType_Threadpool_IOCompletion
                    || (_threadState & (int)ThreadState.TS_CompletionPortThread) == (int)ThreadState.TS_CompletionPortThread;
            }
        }

        public override bool IsThreadpoolWorker
        {
            get
            {
                return (ThreadType & (int)TlsThreadType.ThreadType_Threadpool_Worker) == (int)TlsThreadType.ThreadType_Threadpool_Worker
                    || (_threadState & (int)ThreadState.TS_TPWorkerThread) == (int)ThreadState.TS_TPWorkerThread;
            }
        }

        public override bool IsThreadpoolWait
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Wait) == (int)TlsThreadType.ThreadType_Wait; }
        }

        public override bool IsThreadpoolGate
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_Gate) == (int)TlsThreadType.ThreadType_Gate; }
        }

        public override bool IsSuspendingEE
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_DynamicSuspendEE) == (int)TlsThreadType.ThreadType_DynamicSuspendEE; }
        }

        public override bool IsShutdownHelper
        {
            get { return (ThreadType & (int)TlsThreadType.ThreadType_ShutdownHelper) == (int)TlsThreadType.ThreadType_ShutdownHelper; }
        }



        public override bool IsAborted
        {
            get { return (_threadState & (int)ThreadState.TS_Aborted) == (int)ThreadState.TS_Aborted; }
        }

        public override bool IsGCSuspendPending
        {
            get { return (_threadState & (int)ThreadState.TS_GCSuspendPending) == (int)ThreadState.TS_GCSuspendPending; }
        }

        public override bool IsUserSuspended
        {
            get { return (_threadState & (int)ThreadState.TS_UserSuspendPending) == (int)ThreadState.TS_UserSuspendPending; }
        }

        public override bool IsDebugSuspended
        {
            get { return (_threadState & (int)ThreadState.TS_DebugSuspendPending) == (int)ThreadState.TS_DebugSuspendPending; }
        }

        public override bool IsBackground
        {
            get { return (_threadState & (int)ThreadState.TS_Background) == (int)ThreadState.TS_Background; }
        }

        public override bool IsUnstarted
        {
            get { return (_threadState & (int)ThreadState.TS_Unstarted) == (int)ThreadState.TS_Unstarted; }
        }

        public override bool IsCoInitialized
        {
            get { return (_threadState & (int)ThreadState.TS_CoInitialized) == (int)ThreadState.TS_CoInitialized; }
        }

        public override GcMode GcMode
        {
            get { return _preemptive ? GcMode.Preemptive : GcMode.Cooperative; }
        }

        public override bool IsSTA
        {
            get { return (_threadState & (int)ThreadState.TS_InSTA) == (int)ThreadState.TS_InSTA; }
        }

        public override bool IsMTA
        {
            get { return (_threadState & (int)ThreadState.TS_InMTA) == (int)ThreadState.TS_InMTA; }
        }

        public override bool IsAbortRequested
        {
            get
            {
                return (_threadState & (int)ThreadState.TS_AbortRequested) == (int)ThreadState.TS_AbortRequested
                    || (_threadState & (int)ThreadState.TS_AbortInitiated) == (int)ThreadState.TS_AbortInitiated;
            }
        }


        public override bool IsAlive { get { return _osThreadId != 0 && (_threadState & ((int)ThreadState.TS_Unstarted | (int)ThreadState.TS_Dead)) == 0; } }
        public override uint OSThreadId { get { return _osThreadId; } }
        public override int ManagedThreadId { get { return (int)_managedThreadId; } }
        public override ulong AppDomain { get { return _appDomain; } }
        public override uint LockCount { get { return _lockCount; } }
        public override ulong Teb { get { return _teb; } }
        public override ulong StackBase
        {
            get
            {
                if (_teb == 0)
                    return 0;

                ulong ptr = _teb + (ulong)IntPtr.Size;
                if (!_runtime.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        public override ulong StackLimit
        {
            get
            {
                if (_teb == 0)
                    return 0;

                ulong ptr = _teb + (ulong)IntPtr.Size * 2;
                if (!_runtime.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        public override IEnumerable<ClrRoot> EnumerateStackObjects()
        {
            return _runtime.EnumerateStackReferences(this, true);
        }


        public override IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead)
        {
            return _runtime.EnumerateStackReferences(this, includePossiblyDead);
        }

        public override IList<ClrStackFrame> StackTrace
        {
            get
            {
                if (_stackTrace == null)
                {
                    List<ClrStackFrame> frames = new List<ClrStackFrame>(32);

                    ulong lastSP = ulong.MaxValue;
                    int spCount = 0;

                    int max = 4096;
                    foreach (ClrStackFrame frame in _runtime.EnumerateStackFrames(OSThreadId))
                    {
                        // We only allow a maximum of 4096 frames to be enumerated out of this stack trace to
                        // ensure we don't hit degenerate cases of stack unwind where we never make progress
                        // but the stack pointer keeps changing somehow.
                        if (max-- == 0)
                            break;

                        if (frame.StackPointer == lastSP)
                        {
                            // If we hit five stack frames with the same stack pointer then we aren't making progress
                            // in the unwind.  At that point we need to stop to ensure we don't loop infinitely.
                            if (spCount++ >= 5)
                                break;
                        }
                        else
                        {
                            lastSP = frame.StackPointer;
                            spCount = 0;
                        }

                        frames.Add(frame);
                    }

                    _stackTrace = frames.ToArray();
                }

                return _stackTrace;
            }
        }

        public override IEnumerable<ClrStackFrame> EnumerateStackTrace()
        {
            return _runtime.EnumerateStackFrames(OSThreadId);
        }

        public override IList<BlockingObject> BlockingObjects
        {
            get
            {
                ((DesktopGCHeap)_runtime.GetHeap()).InitLockInspection();

                if (_blockingObjs == null)
                    return new BlockingObject[0];
                return _blockingObjs;
            }
        }

        internal void SetBlockingObjects(BlockingObject[] blobjs)
        {
            _blockingObjs = blobjs;
        }

        #region Helper Enums
        internal enum TlsThreadType
        {
            ThreadType_GC = 0x00000001,
            ThreadType_Timer = 0x00000002,
            ThreadType_Gate = 0x00000004,
            ThreadType_DbgHelper = 0x00000008,
            //ThreadType_Shutdown = 0x00000010,
            ThreadType_DynamicSuspendEE = 0x00000020,
            //ThreadType_Finalizer = 0x00000040,
            //ThreadType_ADUnloadHelper = 0x00000200,
            ThreadType_ShutdownHelper = 0x00000400,
            ThreadType_Threadpool_IOCompletion = 0x00000800,
            ThreadType_Threadpool_Worker = 0x00001000,
            ThreadType_Wait = 0x00002000,
        }

        private enum ThreadState
        {
            //TS_Unknown                = 0x00000000,    // threads are initialized this way

            TS_AbortRequested = 0x00000001,    // Abort the thread
            TS_GCSuspendPending = 0x00000002,    // waiting to get to safe spot for GC
            TS_UserSuspendPending = 0x00000004,    // user suspension at next opportunity
            TS_DebugSuspendPending = 0x00000008,    // Is the debugger suspending threads?
                                                    //TS_GCOnTransitions        = 0x00000010,    // Force a GC on stub transitions (GCStress only)

            //TS_LegalToJoin            = 0x00000020,    // Is it now legal to attempt a Join()
            //TS_YieldRequested         = 0x00000040,    // The task should yield
            //TS_Hijacked               = 0x00000080,    // Return address has been hijacked
            //TS_BlockGCForSO           = 0x00000100,    // If a thread does not have enough stack, WaitUntilGCComplete may fail.
            // Either GC suspension will wait until the thread has cleared this bit,
            // Or the current thread is going to spin if GC has suspended all threads.
            TS_Background = 0x00000200,    // Thread is a background thread
            TS_Unstarted = 0x00000400,    // Thread has never been started
            TS_Dead = 0x00000800,    // Thread is dead

            //TS_WeOwn                  = 0x00001000,    // Exposed object initiated this thread
            TS_CoInitialized = 0x00002000,    // CoInitialize has been called for this thread

            TS_InSTA = 0x00004000,    // Thread hosts an STA
            TS_InMTA = 0x00008000,    // Thread is part of the MTA

            // Some bits that only have meaning for reporting the state to clients.
            //TS_ReportDead             = 0x00010000,    // in WaitForOtherThreads()

            //TS_TaskReset              = 0x00040000,    // The task is reset

            //TS_SyncSuspended          = 0x00080000,    // Suspended via WaitSuspendEvent
            //TS_DebugWillSync          = 0x00100000,    // Debugger will wait for this thread to sync

            //TS_StackCrawlNeeded       = 0x00200000,    // A stackcrawl is needed on this thread, such as for thread abort
            // See comment for s_pWaitForStackCrawlEvent for reason.

            //TS_SuspendUnstarted       = 0x00400000,    // latch a user suspension on an unstarted thread

            TS_Aborted = 0x00800000,    // is the thread aborted?
            TS_TPWorkerThread = 0x01000000,    // is this a threadpool worker thread?

            //TS_Interruptible          = 0x02000000,    // sitting in a Sleep(), Wait(), Join()
            //TS_Interrupted            = 0x04000000,    // was awakened by an interrupt APC. !!! This can be moved to TSNC

            TS_CompletionPortThread = 0x08000000,    // Completion port thread

            TS_AbortInitiated = 0x10000000,    // set when abort is begun

            //TS_Finalized              = 0x20000000,    // The associated managed Thread object has been finalized.
            // We can clean up the unmanaged part now.

            //TS_FailStarted            = 0x40000000,    // The thread fails during startup.
            //TS_Detached               = 0x80000000,    // Thread was detached by DllMain
        }
        #endregion

        #region Internal Methods
        private void InitTls()
        {
            if (_tlsInit)
                return;

            _tlsInit = true;

            _threadType = GetTlsSlotForThread(_runtime, Teb);
        }

        internal static int GetTlsSlotForThread(RuntimeBase runtime, ulong teb)
        {
            const int maxTlsSlot = 64;
            const int tlsSlotOffset = 0x1480; // Same on x86 and amd64
            const int tlsExpansionSlotsOffset = 0x1780;
            uint ptrSize = (uint)runtime.PointerSize;

            ulong lowerTlsSlots = teb + tlsSlotOffset;
            uint clrTlsSlot = runtime.GetTlsSlot();
            if (clrTlsSlot == uint.MaxValue)
                return 0;

            ulong tlsSlot = 0;
            if (clrTlsSlot < maxTlsSlot)
            {
                tlsSlot = lowerTlsSlots + ptrSize * clrTlsSlot;
            }
            else
            {
                if (!runtime.ReadPointer(teb + tlsExpansionSlotsOffset, out tlsSlot) || tlsSlot == 0)
                    return 0;

                tlsSlot += ptrSize * (clrTlsSlot - maxTlsSlot);
            }

            ulong clrTls = 0;
            if (!runtime.ReadPointer(tlsSlot, out clrTls))
                return 0;

            // Get thread data;

            uint tlsThreadTypeIndex = runtime.GetThreadTypeIndex();
            if (tlsThreadTypeIndex == uint.MaxValue)
                return 0;

            ulong threadType = 0;
            if (!runtime.ReadPointer(clrTls + ptrSize * tlsThreadTypeIndex, out threadType))
                return 0;

            return (int)threadType;
        }

        internal DesktopThread(RuntimeBase clr, IThreadData thread, ulong address, bool finalizer)
        {
            _runtime = clr;
            _address = address;
            _finalizer = finalizer;

            Debug.Assert(thread != null);
            if (thread != null)
            {
                _osThreadId = thread.OSThreadID;
                _managedThreadId = thread.ManagedThreadID;
                _appDomain = thread.AppDomain;
                _lockCount = thread.LockCount;
                _teb = thread.Teb;
                _threadState = thread.State;
                _exception = thread.ExceptionPtr;
                _preemptive = thread.Preemptive;
            }
        }


        private uint _osThreadId;
        private RuntimeBase _runtime;
        private IList<ClrStackFrame> _stackTrace;
        private bool _finalizer;

        private bool _tlsInit;
        private int _threadType;
        private int _threadState;
        private uint _managedThreadId;
        private uint _lockCount;
        private ulong _address;
        private ulong _appDomain;
        private ulong _teb;
        private ulong _exception;
        private BlockingObject[] _blockingObjs;
        private bool _preemptive;
        private int ThreadType { get { InitTls(); return _threadType; } }
        #endregion
    }

    internal class LocalVarRoot : ClrRoot
    {
        private bool _pinned;
        private bool _falsePos;
        private bool _interior;
        private ClrThread _thread;
        private ClrType _type;
        private ClrAppDomain _domain;

        public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrAppDomain domain, ClrThread thread, bool pinned, bool falsePos, bool interior)
        {
            Address = addr;
            Object = obj;
            _pinned = pinned;
            _falsePos = falsePos;
            _interior = interior;
            _domain = domain;
            _thread = thread;
            _type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override ClrThread Thread
        {
            get
            {
                return _thread;
            }
        }

        public override bool IsPossibleFalsePositive
        {
            get
            {
                return _falsePos;
            }
        }

        public override string Name
        {
            get
            {
                return "local var";
            }
        }

        public override bool IsPinned
        {
            get
            {
                return _pinned;
            }
        }

        public override GCRootKind Kind
        {
            get
            {
                return GCRootKind.LocalVar;
            }
        }

        public override bool IsInterior
        {
            get
            {
                return _interior;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
