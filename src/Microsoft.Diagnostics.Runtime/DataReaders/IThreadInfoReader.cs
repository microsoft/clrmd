// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Implementation
{
    /// <summary>
    /// Per-OS-thread metadata captured at the time of a dump (or, for live
    /// targets, sampled at the time of the call). Fields are nullable when the
    /// underlying source does not carry them — for example Linux core dumps do
    /// not record thread create/exit time.
    /// </summary>
    public readonly struct ThreadInfo
    {
        /// <summary>OS thread id of the thread.</summary>
        public uint OSThreadId { get; init; }

        /// <summary>Thread creation time, if available.</summary>
        public DateTime? CreateTimeUtc { get; init; }

        /// <summary>Thread exit time, if available (typically only set for
        /// terminated threads in a dump).</summary>
        public DateTime? ExitTimeUtc { get; init; }

        /// <summary>Time spent in user mode since thread creation, if available.</summary>
        public TimeSpan? UserTime { get; init; }

        /// <summary>Time spent in kernel mode since thread creation, if available.</summary>
        public TimeSpan? KernelTime { get; init; }

        /// <summary>The thread's start (entry-point) address, if available.</summary>
        public ulong StartAddress { get; init; }

        /// <summary>Affinity mask, if available.</summary>
        public ulong Affinity { get; init; }

        /// <summary>Suspend count at the time of the dump, if available.</summary>
        public uint SuspendCount { get; init; }

        /// <summary>Priority class, if available.</summary>
        public uint PriorityClass { get; init; }

        /// <summary>Priority, if available.</summary>
        public uint Priority { get; init; }

        /// <summary>Top of the thread's stack range. On x86/amd64 this is the high
        /// address; the stack grows downward. Zero when not available.</summary>
        public ulong StackBase { get; init; }

        /// <summary>Size of the thread's stack range, in bytes. Zero when not available.</summary>
        public ulong StackSize { get; init; }

        /// <summary>Exit status of the thread, if available.</summary>
        public uint ExitStatus { get; init; }

        /// <summary>Source-specific dump flags. For Windows minidumps this is
        /// MINIDUMP_THREAD_INFO.DumpFlags (e.g. MINIDUMP_THREAD_INFO_ERROR_THREAD).
        /// Zero on other sources.</summary>
        public uint DumpFlags { get; init; }
    }

    /// <summary>
    /// Provides per-thread OS metadata (CPU times, stack range, priority, etc.).
    /// This interface is not used by the ClrMD library itself, but is here to
    /// expose data that <see cref="IDataReader"/> implementations already parse
    /// (notably from MINIDUMP_THREAD_INFO and ELF prstatus notes).
    ///
    /// This interface must always be requested and not assumed to be there:
    ///
    ///     IDataReader reader = ...;
    ///
    ///     if (reader is IThreadInfoReader threadInfo)
    ///         ...
    /// </summary>
    public interface IThreadInfoReader
    {
        /// <summary>
        /// Looks up <see cref="ThreadInfo"/> for the given OS thread id.
        /// Returns false when the data reader has no record of the thread.
        /// </summary>
        bool TryGetThreadInfo(uint osThreadId, out ThreadInfo info);

        /// <summary>
        /// Enumerates <see cref="ThreadInfo"/> for every thread known to the
        /// data reader.
        /// </summary>
        IEnumerable<ThreadInfo> EnumerateThreadInfo();
    }
}
