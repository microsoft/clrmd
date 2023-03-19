// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DacOOMData
    {
        public readonly OOMReason Reason;
        public readonly ulong AllocSize;
        public readonly ulong AvailablePageFileMB;
        public readonly ulong GCIndex;
        public readonly OOMGetMemoryFailure GetMemoryFailure;
        public readonly ulong Size;
        public readonly int IsLOH;
    }

    public enum OOMGetMemoryFailure
    {
        None = 0,
        ReserveSegment = 1,
        CommitSegmentBegin = 2,
        CommitEphemeralSegment = 3,
        GrowTable = 4,
        CommitTable = 5
    }

    public enum OOMReason
    {
        None = 0,
        Budget = 1,
        CantCommit = 2,
        CantReserve = 3,
        LOH = 4,
        LowMem = 5,
        UnproductiveFullGC = 6
    }
}