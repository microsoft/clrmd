// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// A single message decoded from a runtime stress log. The contents of
    /// the message are validated; argument values and format string bytes
    /// are accessible only through methods on this struct, which route
    /// through the owning <see cref="StressLog"/> so that all reads are
    /// bounded and sanitized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="StressLogMessage"/> is only valid for the body of the
    /// <c>foreach</c> iteration that yielded it. Once iteration advances
    /// (or the owning <see cref="StressLog"/> is disposed), accessors that
    /// resolve back to dump-derived data (<see cref="GetArgument"/>,
    /// <see cref="Format{T}"/>) return defaults or perform no work.
    /// </para>
    /// <para>
    /// Format string bytes are deliberately not exposed. Use
    /// <see cref="Format{T}"/> with an
    /// <see cref="IStressLogFormatReceiver"/> implementation to render the
    /// message; the parser will hand the receiver only typed, sanitized
    /// tokens.
    /// </para>
    /// </remarks>
    public readonly struct StressLogMessage
    {
        private readonly StressLog? _log;
        private readonly int _generation;
        private readonly ulong _formatAddress;

        internal StressLogMessage(StressLog log,
                                  int generation,
                                  ulong threadId,
                                  uint facility,
                                  ulong timeStampTicks,
                                  ulong formatAddress,
                                  byte argumentCount)
        {
            _log = log;
            _generation = generation;
            _formatAddress = formatAddress;
            ThreadId = threadId;
            Facility = (StressLogFacility)facility;
            TimeStampTicks = timeStampTicks;
            ArgumentCount = argumentCount;
        }

        /// <summary>The id of the thread that wrote this message.</summary>
        public ulong ThreadId { get; }

        /// <summary>Facility bits as encoded in the message header.</summary>
        public StressLogFacility Facility { get; }

        /// <summary>
        /// The raw QPC tick value the runtime stamped on the message. Use
        /// <see cref="ElapsedSeconds"/> for a meaningful comparison across
        /// messages; the absolute value is only meaningful relative to other
        /// timestamps from the same log.
        /// </summary>
        public ulong TimeStampTicks { get; }

        /// <summary>Seconds elapsed between the start of the log and this message.</summary>
        public double ElapsedSeconds => _log?.ElapsedSecondsFor(TimeStampTicks) ?? 0.0;

        /// <summary>Number of arguments carried by this message. Bounded to <c>[0, 63]</c>.</summary>
        public int ArgumentCount { get; }

        /// <summary>
        /// One of a small set of well-known runtime format strings, or
        /// <see cref="StressLogKnownFormat.None"/> if this message does not
        /// match any of them. Useful for GC-history style filtering without
        /// running the format parser.
        /// </summary>
        public StressLogKnownFormat KnownFormat
            => _log?.LookupKnownFormat(_formatAddress) ?? StressLogKnownFormat.None;

        /// <summary>
        /// Read the argument at <paramref name="index"/> as a raw 64-bit
        /// value. Returns <c>0</c> if the index is out of range or the
        /// message has been invalidated by subsequent iteration.
        /// </summary>
        public ulong GetArgument(int index)
            => _log?.GetArgument(_generation, index, ArgumentCount) ?? 0;

        /// <summary>
        /// Render the message into a receiver. The format string bytes are
        /// fetched, sanitized, and parsed into a sequence of typed tokens
        /// dispatched as method calls on <paramref name="receiver"/>. The
        /// receiver is constrained to a value type to allow the call chain
        /// to remain allocation-free.
        /// </summary>
        public void Format<T>(ref T receiver) where T : struct, IStressLogFormatReceiver
        {
            if (_log is null) return;
            _log.FormatMessage(_generation, _formatAddress, ArgumentCount, ref receiver);
        }
    }
}
