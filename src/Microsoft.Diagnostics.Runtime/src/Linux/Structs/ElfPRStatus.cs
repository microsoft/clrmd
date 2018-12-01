// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ElfPRStatus
    {
        public ElfSignalInfo SignalInfo;
        public short CurrentSignal;
        public long SignalsPending;
        public long SignalsHeld;

        public uint Pid;
        public uint PPid;
        public uint PGrp;
        public uint Sid;

        public TimeVal UserTime;
        public TimeVal SystemTime;
        public TimeVal CUserTime;
        public TimeVal CSystemTime;

        public RegSetX64 RegisterSet;

        public int FPValid;
    }
}