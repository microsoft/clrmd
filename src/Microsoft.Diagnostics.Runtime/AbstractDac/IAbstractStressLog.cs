// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// This interface is used to read the stress log data from the target process.
    ///
    /// This interface is optional.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractStressLog
    {
        bool GetStressLogData(out StressLogData data);
        IEnumerable<StressLogThreadInfo> EnumerateThreads();
        IEnumerable<StressLogMessageInfo> EnumerateMessages(ulong threadLogAddress, CancellationToken cancellationToken);
        IEnumerable<(ulong Start, ulong Size)> EnumerateMemoryRanges();
    }

    /// <summary>
    /// Stress log header values (the runtime's <c>StressLog</c> configuration and
    /// timing fields).
    /// </summary>
    public struct StressLogData
    {
        /// <summary>Bitmask of logging facilities the runtime was configured to record.</summary>
        public uint LoggedFacilities { get; set; }

        /// <summary>Maximum logging verbosity level the runtime was configured to record.</summary>
        public uint Level { get; set; }

        /// <summary>Configured maximum log size per thread, in bytes.</summary>
        public uint MaxSizePerThread { get; set; }

        /// <summary>Configured maximum total log size, in bytes.</summary>
        public uint MaxSizeTotal { get; set; }

        /// <summary>Current number of allocated stress log chunks across all threads.</summary>
        public int TotalChunks { get; set; }

        /// <summary>QueryPerformanceFrequency tick rate the timestamps were captured with.</summary>
        public ulong TickFrequency { get; set; }

        /// <summary>The QPC timestamp captured when the stress log was initialized.</summary>
        public ulong StartTimestamp { get; set; }

        /// <summary>The wall-clock start time as a Windows <c>FILETIME</c> (0 when unavailable).</summary>
        public ulong StartTime { get; set; }
    }

    /// <summary>A single thread's stress log.</summary>
    public struct StressLogThreadInfo
    {
        /// <summary>Target-process address of the <c>ThreadStressLog</c>.</summary>
        public ulong ThreadLogAddress { get; set; }

        /// <summary>The OS thread id that owns this log.</summary>
        public ulong ThreadId { get; set; }
    }

    /// <summary>A single decoded stress log message.</summary>
    public struct StressLogMessageInfo
    {
        /// <summary>Facility bits as encoded in the message header.</summary>
        public uint Facility { get; set; }

        /// <summary>The raw QPC tick value the runtime stamped on the message.</summary>
        public ulong Timestamp { get; set; }

        /// <summary>Target-process address of the message's format string text.</summary>
        public ulong FormatAddress { get; set; }

        /// <summary>The message's argument values (already resolved by the DAC).</summary>
        public ulong[] Arguments { get; set; }
    }
}
