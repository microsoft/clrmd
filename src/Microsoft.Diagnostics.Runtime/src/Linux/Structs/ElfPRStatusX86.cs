// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfPRStatusX86 : IElfPRStatus
    {
        public ElfSignalInfo SignalInfo;
        public short CurrentSignal;
        private readonly ushort Padding;
        public uint SignalsPending;
        public uint SignalsHeld;

        public uint Pid;
        public uint PPid;
        public uint PGrp;
        public uint Sid;

        public TimeVal32 UserTime;
        public TimeVal32 SystemTime;
        public TimeVal32 CUserTime;
        public TimeVal32 CSystemTime;

        public RegSetX86 RegisterSet;

        public int FPValid;

        public uint ProcessId => PGrp;

        public uint ThreadId => Pid;

        public unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context) => RegisterSet.CopyContext(contextFlags, contextSize, context);
    }
}
