// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopThread : ThreadBase
    {
        internal DesktopRuntimeBase DesktopRuntime { get; }
        internal ICorDebugThread CorDebugThread => DesktopRuntime.GetCorDebugThread(OSThreadId);
        public override ClrRuntime Runtime => DesktopRuntime;

        public override ClrException CurrentException
        {
            get
            {
                var ex = _exception;
                if (ex == 0)
                    return null;

                if (!DesktopRuntime.ReadPointer(ex, out ex) || ex == 0)
                    return null;

                return DesktopRuntime.Heap.GetExceptionObject(ex);
            }
        }

        public override ulong StackBase
        {
            get
            {
                if (_teb == 0)
                    return 0;

                var ptr = _teb + (ulong)IntPtr.Size;
                if (!DesktopRuntime.ReadPointer(ptr, out ptr))
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

                var ptr = _teb + (ulong)IntPtr.Size * 2;
                if (!DesktopRuntime.ReadPointer(ptr, out ptr))
                    return 0;

                return ptr;
            }
        }

        public override IEnumerable<ClrRoot> EnumerateStackObjects()
        {
            return DesktopRuntime.EnumerateStackReferences(this, true);
        }

        public override IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead)
        {
            return DesktopRuntime.EnumerateStackReferences(this, includePossiblyDead);
        }

        public override IList<ClrStackFrame> StackTrace
        {
            get
            {
                if (_stackTrace == null)
                {
                    var frames = new List<ClrStackFrame>(32);

                    var lastSP = ulong.MaxValue;
                    var spCount = 0;

                    var max = 4096;
                    foreach (var frame in DesktopRuntime.EnumerateStackFrames(this))
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

        internal unsafe void InitLocalData()
        {
            if (_corDebugInit)
                return;

            _corDebugInit = true;

            var thread = (ICorDebugThread3)CorDebugThread;
            thread.CreateStackWalk(out var stackwalk);

            do
            {
                stackwalk.GetFrame(out var frame);

                var ilFrame = frame as ICorDebugILFrame;
                if (ilFrame == null)
                    continue;

                var context = ContextHelper.Context;

                fixed (byte* ptr = context)
                    stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out var size, new IntPtr(ptr));

                ulong ip = BitConverter.ToUInt32(context, ContextHelper.InstructionPointerOffset);
                ulong sp = BitConverter.ToUInt32(context, ContextHelper.StackPointerOffset);

                var result = _stackTrace.Where(frm => sp == frm.StackPointer && ip == frm.InstructionPointer).Select(p => (DesktopStackFrame)p).SingleOrDefault();
                if (result != null)
                    result.CordbFrame = ilFrame;
            } while (stackwalk.Next() == 0);
        }

        public override IEnumerable<ClrStackFrame> EnumerateStackTrace()
        {
            return DesktopRuntime.EnumerateStackFrames(this);
        }

        public override IList<BlockingObject> BlockingObjects
        {
            get
            {
                ((DesktopGCHeap)DesktopRuntime.Heap).InitLockInspection();

                if (_blockingObjs == null)
                    return new BlockingObject[0];

                return _blockingObjs;
            }
        }

        internal DesktopThread(DesktopRuntimeBase clr, IThreadData thread, ulong address, bool finalizer)
            : base(thread, address, finalizer)
        {
            DesktopRuntime = clr;
        }

        private bool _corDebugInit;
    }
}