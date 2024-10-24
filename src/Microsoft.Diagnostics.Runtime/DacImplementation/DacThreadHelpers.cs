// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacThreadHelpers : IAbstractThreadHelpers
    {
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly IDataReader _dataReader;

        public DacThreadHelpers(ClrDataProcess dac, SOSDac sos, IDataReader reader)
        {
            _dac = dac;
            _sos = sos;
            _dataReader = reader;
        }

        // IClrThreadHelpers
        public IEnumerable<StackRootInfo> EnumerateStackRoots(uint osThreadId, bool traceErrors)
        {
            using SOSStackRefEnum? stackRefEnum = _sos.EnumerateStackRefs(osThreadId);
            if (stackRefEnum is null)
                yield break;

            const int GCInteriorFlag = 1;
            const int GCPinnedFlag = 2;
            const int SOS_StackSourceIP = 0;
            const int SOS_StackSourceFrame = 1;
            foreach (StackRefData stackRef in stackRefEnum.ReadStackRefs())
            {
                if (stackRef.Object == 0)
                {
                    if (traceErrors)
                        Trace.TraceWarning($"EnumerateStackRoots found an unexpected entry with Object == 0, addr:{(ulong)stackRef.Address:x} srcType:{stackRef.SourceType:x}");
                    continue;
                }

                bool interior = (stackRef.Flags & GCInteriorFlag) == GCInteriorFlag;
                bool isPinned = (stackRef.Flags & GCPinnedFlag) == GCPinnedFlag;
                int regOffset = 0;
                string? regName = null;
                if (stackRef.HasRegisterInformation != 0)
                {
                    regOffset = stackRef.Offset;
                    regName = _sos.GetRegisterName(stackRef.Register);
                }

                ulong ip = 0;
                ulong frame = 0;
                if (stackRef.SourceType == SOS_StackSourceIP)
                    ip = stackRef.Source;
                else if (stackRef.SourceType == SOS_StackSourceFrame)
                    frame = stackRef.Source;

                yield return new StackRootInfo()
                {
                    InstructionPointer = ip,
                    StackPointer = stackRef.StackPointer,
                    InternalFrame = frame,

                    IsInterior = interior,
                    IsPinned = isPinned,

                    Address = stackRef.Address,
                    Object = stackRef.Object,

                    IsEnregistered = stackRef.HasRegisterInformation != 0,
                    RegisterName = regName,
                    RegisterOffset = regOffset,
                };
            }
        }

        public IEnumerable<StackFrameInfo> EnumerateStackTrace(uint osThreadId, bool includeContext, bool traceErrors)
        {
            using ClrStackWalk? stackwalk = _dac.CreateStackWalk(osThreadId, 0xf);
            if (stackwalk is null)
                yield break;

            int ipOffset;
            int spOffset;
            int contextSize;
            uint contextFlags = 0;
            if (_dataReader.Architecture == Architecture.Arm)
            {
                ipOffset = 64;
                spOffset = 56;
                contextSize = 416;
            }
            else if (_dataReader.Architecture == Architecture.Arm64)
            {
                ipOffset = 264;
                spOffset = 256;
                contextSize = 912;
            }
            else if (_dataReader.Architecture == (Architecture)9 /* Architecture.RiscV64 */)
            {
                ipOffset = 264;
                spOffset = 24;
                contextSize = 544;
            }
            else if (_dataReader.Architecture == (Architecture)6 /* Architecture.LoongArch64 */)
            {
                ipOffset = 264;
                spOffset = 32;
                contextSize = 1312;
            }
            else if (_dataReader.Architecture == Architecture.X86)
            {
                ipOffset = 184;
                spOffset = 196;
                contextSize = 716;
                contextFlags = 0x1003f;
            }
            else // Architecture.X64
            {
                ipOffset = 248;
                spOffset = 152;
                contextSize = 1232;
                contextFlags = 0x10003f;
            }

            HResult hr = HResult.S_OK;
            byte[] context = ArrayPool<byte>.Shared.Rent(contextSize);
            while (hr.IsOK)
            {
                hr = stackwalk.GetContext(contextFlags, contextSize, out _, context);
                if (!hr)
                {
                    if (traceErrors)
                        Trace.TraceError($"GetContext failed, flags:{contextFlags:x} size: {contextSize:x} hr={hr}");

                    break;
                }

                ulong ip = context.AsSpan().AsPointer(ipOffset);
                ulong sp = context.AsSpan().AsPointer(spOffset);

                ulong frameVtbl = stackwalk.GetFrameVtable();
                string? frameName = null;
                ulong frameMethod = 0;
                if (frameVtbl != 0)
                {
                    sp = frameVtbl;
                    frameVtbl = _dataReader.ReadPointer(sp);
                    frameName = _sos.GetFrameName(frameVtbl);
                    frameMethod = _sos.GetMethodDescPtrFromFrame(sp);
                }

                byte[]? contextCopy = null;
                if (includeContext)
                {
                    contextCopy = new byte[contextSize];
                    context.AsSpan(0, contextSize).CopyTo(contextCopy);
                }

                yield return new StackFrameInfo()
                {
                    InstructionPointer = ip,
                    StackPointer = sp,
                    Context = contextCopy,
                    InternalFrameVTable = frameVtbl,
                    InternalFrameName = frameName,
                    InnerMethodMethodHandle = frameMethod,
                };

                hr = stackwalk.Next();
                if (traceErrors && !hr)
                    Trace.TraceInformation($"STACKWALK FAILED - hr:{hr}");
            }

            ArrayPool<byte>.Shared.Return(context);
        }
    }
}