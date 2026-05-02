// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Implementation
{
    /// <summary>
    /// Process-wide metadata captured at the time of a dump (or, for live
    /// targets, sampled at the time of the call). Fields that the underlying
    /// source does not carry are reported as null / 0 / default.
    /// </summary>
    public readonly struct ProcessInfo
    {
        /// <summary>The OS process id of the target.</summary>
        public uint ProcessId { get; init; }

        /// <summary>Full path to the main executable image, if available.</summary>
        public string? ImagePath { get; init; }

        /// <summary>Process command line, if available.</summary>
        public string? CommandLine { get; init; }

        /// <summary>Process creation time, if available.</summary>
        public DateTime? CreateTimeUtc { get; init; }

        /// <summary>Aggregate user-mode CPU time, if available.</summary>
        public TimeSpan? UserTime { get; init; }

        /// <summary>Aggregate kernel-mode CPU time, if available.</summary>
        public TimeSpan? KernelTime { get; init; }

        /// <summary>Architecture of the target process.</summary>
        public Architecture Architecture { get; init; }

        /// <summary>Operating system platform of the target.</summary>
        public OSPlatform TargetPlatform { get; init; }

        /// <summary>OS version (Major.Minor.Build) of the target, if available.</summary>
        public Version? OSVersion { get; init; }

        /// <summary>Free-form OS build/service-pack/distro string, if available.</summary>
        public string? OSBuildString { get; init; }

        /// <summary>Number of logical processors visible to the target, if available.</summary>
        public ushort ProcessorCount { get; init; }

        /// <summary>Time the dump was captured, if available. For live targets,
        /// this is typically null.</summary>
        public DateTime? DumpTimestampUtc { get; init; }
    }

    /// <summary>
    /// Provides one-shot process- and system-level metadata about the target.
    /// This interface is not used by the ClrMD library itself, but is here to
    /// expose data that <see cref="IDataReader"/> implementations already parse
    /// (notably from MINIDUMP_MISC_INFO / MINIDUMP_SYSTEM_INFO and
    /// /proc/$pid/{stat,cmdline,exe} or ELF prpsinfo).
    ///
    /// This interface must always be requested and not assumed to be there:
    ///
    ///     IDataReader reader = ...;
    ///
    ///     if (reader is IProcessInfoProvider processInfo)
    ///         ...
    /// </summary>
    public interface IProcessInfoProvider
    {
        /// <summary>Returns process-wide metadata.</summary>
        ProcessInfo GetProcessInfo();
    }
}
