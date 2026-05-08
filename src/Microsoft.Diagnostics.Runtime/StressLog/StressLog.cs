// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.DacInterface;
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
    /// </remarks>
    public sealed class StressLog : IDisposable
    {
        private readonly IDataReader _reader;
        private readonly StressLogOptions _options;
        private readonly AllocationBudget _budget;
        private readonly StressLogModuleTable _modules;
        private readonly FormatStringCache _formatCache;

        private readonly ulong _firstThreadAddr;
        private readonly ulong _tickFrequency;
        private readonly ulong _startTimeStamp;
        private readonly DateTime? _startTimeUtc;
        private readonly bool _isV4;
        private readonly StressLogVariant _variant;

        private bool _disposed;

        private StressLog(IDataReader reader,
                          StressLogOptions options,
                          AllocationBudget budget,
                          StressLogModuleTable modules,
                          ulong firstThreadAddr,
                          ulong tickFrequency,
                          ulong startTimeStamp,
                          DateTime? startTimeUtc,
                          bool isV4,
                          StressLogVariant variant)
        {
            _reader = reader;
            _options = options;
            _budget = budget;
            _modules = modules;
            _firstThreadAddr = firstThreadAddr;
            _tickFrequency = tickFrequency;
            _startTimeStamp = startTimeStamp;
            _startTimeUtc = startTimeUtc;
            _isV4 = isV4;
            _variant = variant;
            _formatCache = new FormatStringCache(reader, budget, options.MaxFormatStringLength, options.MaxFormatStringCacheEntries);
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
        public DateTime? StartTimeUtc => _startTimeUtc;

        /// <summary>
        /// Try to open the stress log for the given <paramref name="runtime"/>.
        /// Looks up the stress log address through the runtime's DAC and
        /// reads from the runtime's data target. Returns <see langword="false"/>
        /// if the runtime does not have stress logging enabled, the DAC does
        /// not support <c>GetStressLogAddress</c>, or the resulting log fails
        /// validation.
        /// </summary>
        internal static bool TryOpen(ClrRuntime runtime, out StressLog? stressLog)
            => TryOpen(runtime, out stressLog, out _);

        /// <summary>
        /// Try to open the stress log for the given <paramref name="runtime"/>,
        /// returning a human-readable explanation in
        /// <paramref name="failureReason"/> when the open fails.
        /// </summary>
        internal static bool TryOpen(ClrRuntime runtime, out StressLog? stressLog, out string? failureReason)
        {
            stressLog = null;
            failureReason = null;
            if (runtime is null)
            {
                failureReason = "Runtime is null.";
                return false;
            }

            ulong address = runtime.GetStressLogAddress();
            if (address == 0)
            {
                failureReason = "Stress logging is not enabled for this runtime, or the DAC does not expose the stress log address.";
                return false;
            }

            return TryOpen(runtime.DataTarget.DataReader, address, out stressLog, out failureReason);
        }

        /// <summary>
        /// Try to open the stress log at <paramref name="address"/> in the
        /// target. Returns <see langword="false"/> on any structural
        /// validation failure.
        /// </summary>
        internal static bool TryOpen(IDataReader reader, ulong address, out StressLog? stressLog)
            => TryOpen(reader, address, out stressLog, out _);

        internal static bool TryOpen(IDataReader reader, ulong address, out StressLog? stressLog, out string? failureReason)
        {
            stressLog = null;
            failureReason = null;
            if (reader is null)
            {
                failureReason = "Data reader is null.";
                return false;
            }
            if (reader.PointerSize != 8)
            {
                failureReason = $"Stress logs on a {reader.PointerSize * 8}-bit target are not supported; only 64-bit targets are.";
                return false;
            }
            if (address == 0)
            {
                failureReason = "Stress log address is 0.";
                return false;
            }
            StressLogOptions options = StressLogOptions.Default;
            AllocationBudget budget = new AllocationBudget(options.MaxTotalBytesAllocated);

            // Speculatively read the larger memory-mapped header. If the magic
            // does not match, treat as in-process and use only the first
            // InProc_HeaderSize bytes.
            Span<byte> header = stackalloc byte[StressLogLayout.Mm_HeaderReadSize];
            int got = reader.Read(address, header);
            if (got < StressLogLayout.InProc_HeaderSize)
            {
                failureReason = $"Could not read the stress log header at 0x{address:x} (got {got} bytes, expected at least {StressLogLayout.InProc_HeaderSize}).";
                return false;
            }

            uint maybeMagic = got >= StressLogLayout.Mm_Version
                ? BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(StressLogLayout.Mm_Magic))
                : 0;

            bool isMemoryMapped = got >= StressLogLayout.Mm_HeaderReadSize
                                  && maybeMagic == StressLogConstants.MemoryMappedMagic;

            if (isMemoryMapped)
            {
                stressLog = OpenMemoryMapped(reader, options, budget, header);
            }
            else
            {
                stressLog = OpenInProcess(reader, options, budget, header.Slice(0, StressLogLayout.InProc_HeaderSize));
            }

            if (stressLog is null)
            {
                failureReason = "Stress log header failed validation.";
                return false;
            }
            return true;
        }

        private static StressLog? OpenInProcess(IDataReader reader,
                                                StressLogOptions options,
                                                AllocationBudget budget,
                                                ReadOnlySpan<byte> header)
        {
            // Variant detection. Modern CoreCLR initializes the 'padding'
            // field at StressLog offset 32 to UINT_MAX (0xFFFFFFFF) as a
            // sentinel; the .NET Framework runtime stores its TLS slot index
            // there (a small positive integer). This is a stable signal we
            // verified across all five integration-test dump variants.
            uint sentinel = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(StressLogLayout.InProc_Padding));
            StressLogVariant variant = sentinel == 0xFFFFFFFFu
                ? StressLogVariant.Core
                : StressLogVariant.FrameworkV1;

            ulong logs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.InProc_Logs));
            ulong tickFreq = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.InProc_TickFrequency));
            ulong startTs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.InProc_StartTimeStamp));
            ulong startFiletime = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.InProc_StartTime));
            ulong moduleOffset = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.InProc_ModuleOffset));

            DateTime? startTimeUtc = null;
            try
            {
                if (startFiletime != 0)
                    startTimeUtc = DateTime.FromFileTimeUtc((long)startFiletime);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Malformed FILETIME; leave null.
            }

            // Framework's StressLog struct has no module table appended; the
            // bytes we speculatively read at offset 80..160 are unrelated heap
            // memory. Restrict module table parsing to Core, and let Framework
            // fall through to the single-module path keyed off moduleOffset.
            StressLogModuleTable modules;
            if (variant == StressLogVariant.Core)
            {
                ReadOnlySpan<byte> entries = header.Slice(StressLogLayout.InProc_Modules,
                                                          StressLogConstants.MaxModules * StressLogLayout.InProc_ModuleEntrySize);
                modules = StressLogModuleTable.BuildInProcess(
                    entries16: entries,
                    moduleOffset,
                    hasModuleTable: true,
                    StressLogConstants.FormatOffsetMax);
            }
            else
            {
                modules = StressLogModuleTable.BuildInProcess(
                    entries16: ReadOnlySpan<byte>.Empty,
                    moduleOffset,
                    hasModuleTable: false,
                    StressLogConstants.FormatOffsetMax);
            }

            // V4 message decoding has been used since the addition of the
            // module table in CoreCLR; Framework V1 predates it and uses V3.
            bool isV4 = variant == StressLogVariant.Core;

            return new StressLog(reader, options, budget, modules, logs, tickFreq, startTs, startTimeUtc, isV4: isV4, variant: variant);
        }

        private static StressLog? OpenMemoryMapped(IDataReader reader,
                                                   StressLogOptions options,
                                                   AllocationBudget budget,
                                                   ReadOnlySpan<byte> header)
        {
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(StressLogLayout.Mm_Version));
            if (version != StressLogConstants.MemoryMappedVersionV1
                && version != StressLogConstants.MemoryMappedVersionV2)
            {
                return null;
            }

            ulong logs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.Mm_Logs));
            ulong tickFreq = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.Mm_TickFrequency));
            ulong startTs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(StressLogLayout.Mm_StartTimeStamp));

            ReadOnlySpan<byte> entries = header.Slice(StressLogLayout.Mm_Modules,
                                                      StressLogConstants.MaxModules * StressLogLayout.InProc_ModuleEntrySize);

            ulong maxOffset = version == StressLogConstants.MemoryMappedVersionV1
                ? (1UL << StressLogConstants.FormatOffsetLowBits)
                : StressLogConstants.FormatOffsetMax;

            StressLogModuleTable modules = StressLogModuleTable.BuildMemoryMapped(entries, maxOffset);

            return new StressLog(reader, options, budget, modules, logs, tickFreq, startTs, startTimeUtc: null, isV4: true, variant: StressLogVariant.Core);
        }

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
        public IEnumerable<StressLogMessage> EnumerateMessages(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Each enumeration owns its own context so concurrent foreach loops
            // do not clobber each other's argument scratch / current iterator.
            ArgumentResolver argResolver = new ArgumentResolver(_reader, _options.MaxStringArgumentLength);
            StressLogEnumerationContext context = new StressLogEnumerationContext(this, argResolver);

            List<ThreadIterator> iterators = new();
            try
            {
                if (!LoadThreadIterators(iterators))
                    yield break;

                // Prime all iterators (each has its first message decoded).
                for (int i = iterators.Count - 1; i >= 0; i--)
                {
                    if (!iterators[i].Advance())
                    {
                        iterators[i].Dispose();
                        iterators.RemoveAt(i);
                    }
                }

                // Yield in newest-first order via repeated linear max scan.
                // Iterator count is bounded by MaxThreads; the linear scan is
                // O(threads) per message, which is fine for typical numbers.
                while (iterators.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int newest = 0;
                    for (int i = 1; i < iterators.Count; i++)
                    {
                        if (iterators[i].TimeStamp > iterators[newest].TimeStamp)
                            newest = i;
                    }

                    ThreadIterator iter = iterators[newest];
                    int gen = context.CaptureFromIterator(iter);

                    yield return new StressLogMessage(
                        context: context,
                        generation: gen,
                        threadId: iter.OSThreadId,
                        facility: iter.Facility,
                        timeStampTicks: iter.TimeStamp,
                        formatAddress: ResolveFormatAddress(iter.FormatOffset),
                        argumentCount: (byte)iter.ArgumentCount);

                    if (!iter.Advance())
                    {
                        iter.Dispose();
                        iterators.RemoveAt(newest);
                    }
                }
            }
            finally
            {
                context.Clear();
                foreach (ThreadIterator iter in iterators)
                    iter.Dispose();
            }
        }

        private bool LoadThreadIterators(List<ThreadIterator> iterators)
        {
            HashSet<ulong> visited = new();
            ulong addr = _firstThreadAddr;

            Span<byte> threadHeader = stackalloc byte[StressLogLayout.Thread_HeaderSize];
            Action<StressLogDiagnostic> raiseDiagnostic = d => Diagnostic?.Invoke(d);

            while (addr != 0)
            {
                if (iterators.Count >= _options.MaxThreads)
                {
                    Diagnostic?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.LimitExceeded, addr));
                    break;
                }

                if (!visited.Add(addr))
                {
                    Diagnostic?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptThread, addr));
                    break;
                }

                int got = _reader.Read(addr, threadHeader);
                if (got < StressLogLayout.Thread_HeaderSize)
                {
                    Diagnostic?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.ReadMemoryFailed, addr));
                    break;
                }

                ulong nextAddr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_Next));
                ThreadIterator iter = new ThreadIterator(_reader, _options, _budget,
                    diagnostic: raiseDiagnostic,
                    threadAddress: addr,
                    threadHeader: threadHeader,
                    isV4: _isV4,
                    variant: _variant);

                iterators.Add(iter);
                addr = nextAddr;
            }

            return iterators.Count > 0;
        }

        private ulong ResolveFormatAddress(ulong formatOffset)
        {
            return _modules.TryResolveFormatOffset(formatOffset, out ulong addr) ? addr : 0;
        }

        internal double ElapsedSecondsFor(ulong timeStamp)
        {
            if (_tickFrequency == 0) return 0;
            ulong delta = timeStamp >= _startTimeStamp ? timeStamp - _startTimeStamp : 0;
            return (double)delta / _tickFrequency;
        }

        internal StressLogKnownFormat LookupKnownFormat(ulong address)
        {
            if (address == 0) return StressLogKnownFormat.None;
            if (_formatCache.TryGet(address, out _, out StressLogKnownFormat known))
                return known;
            return StressLogKnownFormat.None;
        }

        /// <summary>
        /// Render a stress log message into a receiver using the supplied
        /// per-enumeration argument scratch and resolver. Called by
        /// <see cref="StressLogEnumerationContext"/> after it has confirmed
        /// the calling <see cref="StressLogMessage"/> still belongs to the
        /// current enumeration generation.
        /// </summary>
        internal void FormatMessage<T>(ulong[] argScratch,
                                       ArgumentResolver argResolver,
                                       ulong formatAddress,
                                       int argCount,
                                       ref T receiver)
            where T : struct, IStressLogFormatReceiver
        {
            if (formatAddress == 0)
            {
                receiver.Literal(s_unresolvedFormat.AsSpan());
                return;
            }

            if (!_formatCache.TryGet(formatAddress, out ReadOnlyMemory<byte> formatBytes, out _) || formatBytes.Length == 0)
            {
                receiver.Literal(s_unresolvedFormat.AsSpan());
                return;
            }

            ReadOnlySpan<ulong> args = argScratch.AsSpan(0, argCount);
            FormatStringParser.Parse(formatBytes.Span, args, argResolver, ref receiver);
        }

        private static readonly byte[] s_unresolvedFormat =
            System.Text.Encoding.ASCII.GetBytes("<unresolved-format>");

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StressLog));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
