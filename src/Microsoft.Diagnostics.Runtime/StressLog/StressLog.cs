// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Reads a runtime stress log from a target process or dump. The
    /// implementation validates every input byte: every linked-list walk
    /// is bounded and cycle-detected, every memory read is checked against
    /// the requested length, and no log-derived bytes are ever passed to
    /// a runtime formatter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the public entry point for stress-log analysis. Callers open
    /// a log via <c>TryOpen</c>, then enumerate messages with
    /// <see cref="EnumerateMessages"/>. Each yielded
    /// <see cref="StressLogMessage"/> is valid only for the body of the
    /// iteration step that produced it.
    /// </para>
    /// <para>
    /// Format string bytes are sanitized and decoded inside the
    /// <see cref="StressLog"/> instance. Consumers receive only typed,
    /// sanitized tokens through <see cref="IStressLogFormatReceiver"/>; the
    /// raw bytes never escape this assembly.
    /// </para>
    /// <para>
    /// <see cref="StressLog"/> is an abstract base; concrete subclasses read
    /// either by parsing target memory directly (<see cref="LegacyStressLog"/>)
    /// or through the runtime's structured DAC contract
    /// (<c>ContractStressLog</c>). Callers never construct a subclass directly --
    /// they use the static <c>TryOpen</c> factories or
    /// <see cref="ClrRuntime.TryGetStressLog"/>.
    /// </para>
    /// </remarks>
    public abstract class StressLog : IDisposable
    {
        private bool _disposed;

        private protected StressLog()
        {
        }

        /// <summary>
        /// Raised when the parser detects malformed input. Diagnostics are
        /// best-effort and may be raised before the corresponding
        /// <see cref="EnumerateMessages"/> sequence terminates.
        /// </summary>
        public event Action<StressLogDiagnostic>? Diagnostic;

        /// <summary>
        /// Approximate wall-clock time at which the runtime initialized the
        /// stress log. Available only for in-process logs; memory-mapped
        /// logs do not record an absolute time and report <see langword="null"/>.
        /// </summary>
        public abstract DateTime? StartTimeUtc { get; }

        /// <summary>
        /// Pointer size of the target the log was captured from, in bytes.
        /// 4 for a 32-bit target, 8 for a 64-bit target. Receivers should
        /// format <c>%p</c> arguments with <c>2 * PointerSize</c> hex digits
        /// to match the target's native pointer width.
        /// </summary>
        public abstract int PointerSize { get; }

        /// <summary>
        /// QueryPerformanceFrequency tick rate the log timestamps were captured with,
        /// used to convert raw timestamps to elapsed seconds.
        /// </summary>
        public abstract ulong TickFrequency { get; }

        /// <summary>
        /// Bitmask of logging facilities the runtime was configured to record
        /// (the runtime's <c>facilitiesToLog</c>). In-process logs only;
        /// <see langword="null"/> for memory-mapped logs.
        /// </summary>
        public abstract uint? FacilitiesToLog { get; }

        /// <summary>
        /// Maximum logging verbosity level the runtime was configured to record
        /// (the runtime's <c>levelToLog</c>). In-process logs only;
        /// <see langword="null"/> for memory-mapped logs.
        /// </summary>
        public abstract uint? LevelToLog { get; }

        /// <summary>
        /// Configured maximum log size per thread, in bytes. In-process logs
        /// only; <see langword="null"/> for memory-mapped logs.
        /// </summary>
        public abstract uint? MaxSizePerThread { get; }

        /// <summary>
        /// Configured maximum total log size, in bytes. In-process logs only;
        /// <see langword="null"/> for memory-mapped logs.
        /// </summary>
        public abstract uint? MaxSizeTotal { get; }

        /// <summary>
        /// Current number of allocated stress log chunks across all threads
        /// (the runtime's <c>totalChunk</c>). In-process logs only;
        /// <see langword="null"/> for memory-mapped logs.
        /// </summary>
        public abstract int? ChunkCount { get; }

        /// <summary>
        /// Open the stress log located at <paramref name="address"/> in the given
        /// <paramref name="dataTarget"/>. Unlike the <see cref="IDataReader"/>
        /// overload, this wires up an on-disk module-image fallback so that
        /// format strings and <c>%s</c>/<c>%S</c> argument strings living in a
        /// module's read-only data can be recovered when the dump itself strips
        /// those file-backed pages (common for Linux coredumps and minidumps);
        /// without it every message would render as <c>&lt;unresolved-format&gt;</c>.
        /// The returned <see cref="StressLog"/> owns the fallback reader and
        /// releases it on <see cref="Dispose"/>.
        /// </summary>
        public static bool TryOpen(DataTarget dataTarget, ulong address, out StressLog? stressLog, out string? failureReason)
        {
            stressLog = null;
            failureReason = null;
            if (dataTarget is null)
            {
                failureReason = "DataTarget is null.";
                return false;
            }

            ModuleImageReader formatReader = new(dataTarget.DataReader, dataTarget);
            if (!LegacyStressLog.TryOpen(dataTarget.DataReader, address, formatReader, out stressLog, out failureReason))
            {
                formatReader.Dispose();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Open the stress log located at <paramref name="address"/> in the target
        /// described by <paramref name="reader"/>. Use this to read a stress log
        /// that is not the runtime's own — for example a different utilcode-linked
        /// module's log such as <c>mscordbi!StressLog::theLog</c>. Returns
        /// <see langword="false"/> on any structural validation failure, with
        /// <paramref name="failureReason"/> describing the problem. The returned
        /// <see cref="StressLog"/> is owned by the caller and must be disposed.
        /// </summary>
        public static bool TryOpen(IDataReader reader, ulong address, out StressLog? stressLog, out string? failureReason)
            => LegacyStressLog.TryOpen(reader, address, formatReader: null, out stressLog, out failureReason);

        /// <summary>
        /// Enumerate the target memory ranges occupied by the stress log's
        /// per-thread chunk buffers. Callers can use this to exclude stress
        /// log memory from a broader scan — for example, to distinguish a real
        /// reference to an object from a reference that merely appears inside a
        /// logged message.
        /// </summary>
        /// <remarks>
        /// Each range covers a whole <c>StressLogChunk</c> structure, matching the
        /// runtime's chunk allocation granularity. A thread or chunk that cannot be
        /// read, or that looks corrupt, is skipped and reported through
        /// <see cref="Diagnostic"/>; enumeration continues with the remaining
        /// threads where possible. Each thread and chunk is visited at most once, so
        /// a corrupt log whose links form a cycle stays bounded.
        /// </remarks>
        public abstract IEnumerable<MemoryRange> EnumerateMemoryRanges();

        /// <summary>
        /// Enumerate messages from all threads in newest-first chronological
        /// order. Iteration stops cooperatively when
        /// <paramref name="cancellationToken"/> is canceled or when every
        /// thread has been drained.
        /// </summary>
        /// <remarks>
        /// Each call allocates an independent enumeration context. Two
        /// concurrent enumerations on the same <see cref="StressLog"/> do
        /// not share argument scratch buffers, so message access through
        /// <see cref="StressLogMessage.GetArgument"/> and
        /// <see cref="StressLogMessage.Format{T}"/> remains correct under
        /// concurrent <c>foreach</c> loops.
        /// </remarks>
        public abstract IEnumerable<StressLogMessage> EnumerateMessages(CancellationToken cancellationToken = default);

        protected void RaiseDiagnostic(StressLogDiagnosticKind kind, ulong address)
            => Diagnostic?.Invoke(new StressLogDiagnostic(kind, address));

        protected void RaiseDiagnostic(StressLogDiagnostic diagnostic)
            => Diagnostic?.Invoke(diagnostic);

        /// <summary>
        /// Convert a raw QPC timestamp to seconds elapsed since the log started.
        /// Implemented by subclasses from their own timing fields; called by
        /// <see cref="StressLogEnumerationContext"/> on behalf of a
        /// <see cref="StressLogMessage"/>.
        /// </summary>
        internal abstract double ElapsedSecondsFor(ulong timeStamp);

        /// <summary>
        /// Classify a format address as one of a small set of well-known runtime
        /// format strings. Called by <see cref="StressLogEnumerationContext"/> on
        /// behalf of a <see cref="StressLogMessage"/>.
        /// </summary>
        internal abstract StressLogKnownFormat LookupKnownFormat(ulong address);

        /// <summary>
        /// Render a stress log message into a receiver using the supplied
        /// per-enumeration argument span and resolver. Called by
        /// <see cref="StressLogEnumerationContext"/> after it has confirmed
        /// the calling <see cref="StressLogMessage"/> still belongs to the
        /// current enumeration generation.
        /// </summary>
        internal abstract void FormatMessage<T>(ReadOnlySpan<ulong> args,
                                                ArgumentResolver argResolver,
                                                ulong formatAddress,
                                                ref T receiver)
            where T : struct, IStressLogFormatReceiver;

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StressLog));
        }

        /// <summary>
        /// Convert a Windows <c>FILETIME</c> (as captured in the stress log header) to a
        /// UTC <see cref="DateTime"/>, or <see langword="null"/> when the value is zero
        /// or out of range. Shared by the concrete subclasses, which read the same
        /// FILETIME field from either the raw header or the DAC contract.
        /// </summary>
        protected static DateTime? FileTimeToUtc(ulong fileTime)
        {
            if (fileTime == 0)
                return null;

            try
            {
                return DateTime.FromFileTimeUtc((long)fileTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeCore();
        }

        /// <summary>
        /// Release subclass-owned resources (for example a module-image format
        /// reader). Called exactly once, the first time <see cref="Dispose"/> runs.
        /// </summary>
        protected virtual void DisposeCore()
        {
        }
    }
}
