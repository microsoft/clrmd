// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                ulong ex = _exception;
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

                ulong ptr = _teb + (ulong)IntPtr.Size;
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

                ulong ptr = _teb + (ulong)IntPtr.Size * 2;
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
                    List<ClrStackFrame> frames = new List<ClrStackFrame>(32);

                    ulong lastSP = ulong.MaxValue;
                    int spCount = 0;

                    int max = 4096;
                    foreach (ClrStackFrame frame in DesktopRuntime.EnumerateStackFrames(this))
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

            ICorDebugThread3 thread = (ICorDebugThread3)CorDebugThread;
            thread.CreateStackWalk(out ICorDebugStackWalk stackwalk);

            do
            {
                stackwalk.GetFrame(out ICorDebugFrame frame);

                ICorDebugILFrame ilFrame = frame as ICorDebugILFrame;
                if (ilFrame == null)
                    continue;

                byte[] context = ContextHelper.Context;

                fixed (byte* ptr = context)
                    stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out uint size, new IntPtr(ptr));

                ulong ip, sp;
                if (IntPtr.Size == 4)
                {
                    ip = BitConverter.ToUInt32(context, ContextHelper.InstructionPointerOffset);
                    sp = BitConverter.ToUInt32(context, ContextHelper.StackPointerOffset);
                }
                else
                {
                    ip = BitConverter.ToUInt64(context, ContextHelper.InstructionPointerOffset);
                    sp = BitConverter.ToUInt64(context, ContextHelper.StackPointerOffset);
                }

                DesktopStackFrame result = _stackTrace.Where(frm => sp == frm.StackPointer && ip == frm.InstructionPointer).Select(p => (DesktopStackFrame)p).SingleOrDefault();
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