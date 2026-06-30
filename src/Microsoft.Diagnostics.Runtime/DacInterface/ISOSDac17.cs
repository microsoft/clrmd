// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SOSStressLogData
    {
        public uint LoggedFacilities;
        public uint Level;
        public uint MaxSizePerThread;
        public uint MaxSizeTotal;
        public int TotalChunks;
        public ulong TickFrequency;
        public ulong StartTimestamp;
        public ulong StartTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SOSThreadStressLogData
    {
        public ClrDataAddress ThreadLogAddress;
        public ulong ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SOSStressMsgData
    {
        public uint Facility;
        public ClrDataAddress FormatString;
        public ulong Timestamp;
        public uint ArgumentCount;
    }

    internal interface ISOSDac17 : IDisposable
    {
        bool TryGetStressLogData(out SOSStressLogData data);
        SosStressLogThreadEnum? GetThreadEnumerator();
        SosStressLogMsgEnum? GetMessageEnumerator(ClrDataAddress threadStressLogAddress);
        SosMemoryEnum? GetMemoryRangeEnumerator();
    }
}
