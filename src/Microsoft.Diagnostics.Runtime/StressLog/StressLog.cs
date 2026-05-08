// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
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
    /// a log via <see cref="TryOpen"/>, then enumerate messages with
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
        private readonly ArgumentResolver _argResolver;

        private readonly ulong _firstThreadAddr;
        private readonly ulong _tickFrequency;
        private readonly ulong _startTimeStamp;
        private readonly DateTime? _startTimeUtc;
        private readonly bool _isV4;

        private readonly ulong[] _argScratch = new ulong[StressLogConstants.MaxArgumentCount];

        private ThreadIterator? _currentIterator;
        private int _generationCounter;

        private bool _disposed;

        private StressLog(IDataReader reader,
                          StressLogOptions options,
                          AllocationBudget budget,
                          StressLogModuleTable modules,
                          ulong firstThreadAddr,
                          ulong tickFrequency,
                          ulong startTimeStamp,
                          DateTime? startTimeUtc,
                          bool isV4)
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
            _formatCache = new FormatStringCache(reader, budget, options.MaxFormatStringLength, options.MaxFormatStringCacheEntries);
            _argResolver = new ArgumentResolver(reader);
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
        /// Try to open the stress log at <paramref name="address"/> in the
        /// target. Returns <see langword="false"/> on any structural
        /// validation failure.
        /// </summary>
        public static bool TryOpen(IDataReader reader, ulong address, out StressLog? stressLog)
        {
            stressLog = null;
            if (reader is null) return false;
            if (reader.PointerSize != 8) return false;
            if (address == 0) return false;

            StressLogOptions options = StressLogOptions.Default;
            AllocationBudget budget = new AllocationBudget(options.MaxTotalBytesAllocated);

            // Speculatively read the larger memory-mapped header. If the magic
            // does not match, treat as in-process and use only the first
            // InProc_HeaderSize bytes.
            Span<byte> header = stackalloc byte[StressLogLayout.Mm_HeaderReadSize];
            int got = reader.Read(address, header);
            if (got < StressLogLayout.InProc_HeaderSize)
                return false;

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

            return stressLog is not null;
        }

        private static StressLog? OpenInProcess(IDataReader reader,
                                                StressLogOptions options,
                                                AllocationBudget budget,
                                                ReadOnlySpan<byte> header)
        {
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

            ReadOnlySpan<byte> entries = header.Slice(StressLogLayout.InProc_Modules,
                                                      StressLogConstants.MaxModules * StressLogLayout.InProc_ModuleEntrySize);
            // Use the module table only when at least one module entry's base
            // address matches moduleOffset; otherwise fall back to single-module
            // mode so a malformed log cannot remap addresses arbitrarily.
            StressLogModuleTable modules = StressLogModuleTable.BuildInProcess(
                entries,
                moduleOffset,
                hasModuleTable: true,
                StressLogConstants.FormatOffsetMax);

            return new StressLog(reader, options, budget, modules, logs, tickFreq, startTs, startTimeUtc, isV4: true);
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

            return new StressLog(reader, options, budget, modules, logs, tickFreq, startTs, startTimeUtc: null, isV4: true);
        }

        /// <summary>
        /// Enumerate messages from all threads in newest-first chronological
        /// order. Iteration stops cooperatively when
        /// <paramref name="cancellationToken"/> is canceled or when every
        /// thread has been drained.
        /// </summary>
        public IEnumerable<StressLogMessage> EnumerateMessages(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

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
                    _currentIterator = iter;
                    int gen = ++_generationCounter;
                    CopyArgsToScratch(iter);

                    yield return new StressLogMessage(
                        log: this,
                        generation: gen,
                        threadId: iter.ThreadId,
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
                _currentIterator = null;
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
                    isV4: _isV4);

                iterators.Add(iter);
                addr = nextAddr;
            }

            return iterators.Count > 0;
        }

        private void CopyArgsToScratch(ThreadIterator iter)
        {
            int n = iter.ArgumentCount;
            for (int i = 0; i < n; i++)
                _argScratch[i] = iter.GetArgument(i);
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

        internal ulong GetArgument(int generation, int index, int argCount)
        {
            if (generation != _generationCounter || _currentIterator is null) return 0;
            if ((uint)index >= (uint)argCount) return 0;
            return _argScratch[index];
        }

        internal void FormatMessage<T>(int generation, ulong formatAddress, int argCount, ref T receiver)
            where T : struct, IStressLogFormatReceiver
        {
            if (generation != _generationCounter || _currentIterator is null) return;
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

            ReadOnlySpan<ulong> args = _argScratch.AsSpan(0, argCount);
            FormatStringParser.Parse(formatBytes.Span, args, _argResolver, ref receiver);
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
            _currentIterator = null;
        }
    }
}
