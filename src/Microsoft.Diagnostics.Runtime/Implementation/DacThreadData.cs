// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class DacThreadData : IClrThreadData
    {
        private readonly IDataReader _reader;
        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly ThreadData _data;
        private ulong? _stackBase;
        private ulong? _stackLimit;

        public ulong Address { get; }

        public bool HasData { get; }

        public ulong AppDomain => _data.Domain;

        public uint OSThreadId => _data.OSThreadId;

        public int ManagedThreadId => _data.ManagedThreadId < int.MaxValue ? (int)_data.ManagedThreadId : int.MaxValue;

        public uint LockCount => _data.LockCount;

        public ulong Teb => _data.Teb;

        public ulong ExceptionInFlight => _data.LastThrownObjectHandle != 0 ? _reader.ReadPointer(_data.LastThrownObjectHandle) : 0;

        public bool IsFinalizer { get; }

        public bool IsGC { get; }

        public GCMode GCMode => _data.PreemptiveGCDisabled == 0 ? GCMode.Preemptive : GCMode.Cooperative;

        public ClrThreadState ThreadState => (ClrThreadState)_data.State;

        public ulong NextThread => _data.NextThread;


        public ulong StackBase
        {
            get
            {
                if (_stackBase is ulong stackBase)
                    return stackBase;

                ulong teb = _data.Teb;
                if (teb == 0)
                {
                    _stackBase = 0;
                    return 0;
                }

                uint pointerSize = (uint)_reader.PointerSize;
                stackBase = _reader.ReadPointer(teb + pointerSize);
                _stackBase = stackBase;
                return stackBase;
            }
        }

        public ulong StackLimit
        {
            get
            {
                if (_stackLimit is ulong stackLimit)
                    return stackLimit;

                ulong teb = _data.Teb;
                if (teb == 0)
                {
                    _stackLimit = 0;
                    return 0;
                }

                uint pointerSize = (uint)_reader.PointerSize;
                stackLimit = _reader.ReadPointer(teb + pointerSize * 2);
                _stackLimit = stackLimit;
                return stackLimit;
            }
        }

        public DacThreadData(ClrDataProcess dac, SOSDac sos, IDataReader reader, ulong address, in ThreadStoreData threadStore)
        {
            _reader = reader;
            _dac = dac;
            _sos = sos;
            Address = address;
            HasData = sos.GetThreadData(address, out _data);
            IsGC = address == threadStore.GCThread;
            IsFinalizer = address == threadStore.FinalizerThread;
        }

        public IEnumerable<StackRootInfo> EnumerateStackRoots(uint osThreadId)
        {
            using SOSStackRefEnum? stackRefEnum = _sos.EnumerateStackRefs(osThreadId);
            if (stackRefEnum is null)
                yield break;

            const int GCInteriorFlag = 1;
            const int GCPinnedFlag = 2;
            foreach (StackRefData stackRef in stackRefEnum.ReadStackRefs())
            {
                if (stackRef.Object == 0)
                {
                    Trace.TraceInformation($"EnumerateStackRoots found an entry with Object == 0, addr:{(ulong)stackRef.Address:x} srcType:{stackRef.SourceType:x}");
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

                yield return new StackRootInfo()
                {
                    Source = stackRef.Source,
                    StackPointer = stackRef.StackPointer,

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

        public IEnumerable<StackFrameInfo> EnumerateStackTrace(uint osThreadId, bool includeContext, int maxFrames)
        {
            using ClrStackWalk? stackwalk = _dac.CreateStackWalk(osThreadId, 0xf);
            if (stackwalk is null)
                yield break;

            int ipOffset;
            int spOffset;
            int contextSize;
            uint contextFlags = 0;
            if (_reader.Architecture == Architecture.Arm)
            {
                ipOffset = 64;
                spOffset = 56;
                contextSize = 416;
            }
            else if (_reader.Architecture == Architecture.Arm64)
            {
                ipOffset = 264;
                spOffset = 256;
                contextSize = 912;
            }
            else if (_reader.Architecture == Architecture.X86)
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
            while (hr.IsOK && maxFrames-- > 0)
            {
                hr = stackwalk.GetContext(contextFlags, contextSize, out _, context);
                if (!hr)
                {
                    Trace.TraceInformation($"GetContext failed, flags:{contextFlags:x} size: {contextSize:x} hr={hr}");
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
                    frameVtbl = _reader.ReadPointer(sp);
                    frameName = _sos.GetFrameName(frameVtbl);
                    frameMethod = _sos.GetMethodDescPtrFromFrame(sp);
                }

                byte[]? contextCopy = null;
                if (includeContext)
                {
                    contextCopy = context.AsSpan(0, contextSize).ToArray();
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
                if (!hr)
                    Trace.TraceInformation($"STACKWALK FAILED - hr:{hr}");
            }
        }
    }
}