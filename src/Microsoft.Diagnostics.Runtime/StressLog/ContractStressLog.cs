// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// <see cref="StressLog"/> backed by the structured DAC stress-log contract
    /// (<see cref="IAbstractStressLog"/>, the cDAC's <c>ISOSDacInterface17</c>).
    /// Threads, messages, their already-resolved arguments, and the chunk memory
    /// ranges all come from the DAC; format-string text is still resolved and
    /// sanitized here from the returned absolute format address.
    /// </summary>
    internal sealed class ContractStressLog : StressLog
    {
        private readonly IAbstractStressLog _dac;
        private readonly IMemoryReader _formatReader;
        private readonly StressLogOptions _options;
        private readonly FormatStringCache _formatCache;
        private readonly int _pointerSize;

        private readonly ulong _tickFrequency;
        private readonly ulong _startTimeStamp;
        private readonly DateTime? _startTimeUtc;
        private readonly uint? _facilitiesToLog;
        private readonly uint? _levelToLog;
        private readonly uint? _maxSizePerThread;
        private readonly uint? _maxSizeTotal;
        private readonly int? _chunkCount;

        private ContractStressLog(IAbstractStressLog dac,
                               IMemoryReader formatReader,
                               StressLogOptions options,
                               AllocationBudget budget,
                               int pointerSize,
                               ulong tickFrequency,
                               ulong startTimeStamp,
                               DateTime? startTimeUtc,
                               uint? facilitiesToLog,
                               uint? levelToLog,
                               uint? maxSizePerThread,
                               uint? maxSizeTotal,
                               int? chunkCount)
        {
            _dac = dac;
            _formatReader = formatReader;
            _options = options;
            _pointerSize = pointerSize;
            _tickFrequency = tickFrequency;
            _startTimeStamp = startTimeStamp;
            _startTimeUtc = startTimeUtc;
            _facilitiesToLog = facilitiesToLog;
            _levelToLog = levelToLog;
            _maxSizePerThread = maxSizePerThread;
            _maxSizeTotal = maxSizeTotal;
            _chunkCount = chunkCount;
            _formatCache = new FormatStringCache(formatReader, budget, options.MaxFormatStringLength, options.MaxFormatStringCacheEntries);
        }

        public override DateTime? StartTimeUtc => _startTimeUtc;
        public override int PointerSize => _pointerSize;
        public override ulong TickFrequency => _tickFrequency;
        public override uint? FacilitiesToLog => _facilitiesToLog;
        public override uint? LevelToLog => _levelToLog;
        public override uint? MaxSizePerThread => _maxSizePerThread;
        public override uint? MaxSizeTotal => _maxSizeTotal;
        public override int? ChunkCount => _chunkCount;

        /// <summary>
        /// Build a stress log entirely from the structured DAC contract: header from
        /// <see cref="IAbstractStressLog.GetStressLogData"/>, threads/messages/memory
        /// ranges from the contract's enumerators. Returns <see langword="false"/> when
        /// the contract reports no stress log or the target is unsupported.
        /// </summary>
        internal static bool TryOpen(DataTarget dataTarget, IAbstractStressLog dac, out StressLog? stressLog, out string? failureReason)
        {
            stressLog = null;
            failureReason = null;

            IDataReader reader = dataTarget.DataReader;
            if (reader.PointerSize != 8 && reader.PointerSize != 4)
            {
                failureReason = $"Stress logs on a {reader.PointerSize * 8}-bit target are not supported; only 32-bit and 64-bit targets are.";
                return false;
            }

            if (!dac.GetStressLogData(out StressLogData header))
            {
                failureReason = "The DAC did not report a stress log for this runtime.";
                return false;
            }

            // The contract is a CoreCLR/cDAC-only feature. We need no chunk layout or
            // module table -- threads, messages, and memory ranges all come from the
            // contract, and the message enumerator returns absolute format addresses.
            // A module-image format reader still recovers format-string and %s/%S bytes
            // that the dump may have stripped.
            StressLogOptions options = StressLogOptions.Default;
            AllocationBudget budget = new(options.MaxTotalBytesAllocated);
            ModuleImageReader formatReader = new(reader, dataTarget);

            stressLog = new ContractStressLog(dac, formatReader, options, budget,
                pointerSize: reader.PointerSize,
                tickFrequency: header.TickFrequency,
                startTimeStamp: header.StartTimestamp,
                startTimeUtc: FileTimeToUtc(header.StartTime),
                facilitiesToLog: header.LoggedFacilities,
                levelToLog: header.Level,
                maxSizePerThread: header.MaxSizePerThread,
                maxSizeTotal: header.MaxSizeTotal,
                chunkCount: header.TotalChunks);
            return true;
        }

        /// <summary>
        /// Enumerates the stress log's chunk memory ranges directly from the DAC
        /// contract (the cDAC precomputes the chunk list), so no raw chunk-ring walk
        /// is needed.
        /// </summary>
        public override IEnumerable<MemoryRange> EnumerateMemoryRanges()
        {
            ThrowIfDisposed();
            foreach ((ulong start, ulong size) in _dac.EnumerateMemoryRanges())
            {
                ulong end = start + size;
                if (end < start)
                {
                    RaiseDiagnostic(StressLogDiagnosticKind.CorruptChunk, start);
                    continue;
                }

                yield return new MemoryRange(start, end);
            }
        }

        public override IEnumerable<StressLogMessage> EnumerateMessages(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            ArgumentResolver argResolver = new(_formatReader, _options.MaxStringArgumentLength, _pointerSize);
            StressLogEnumerationContext context = new(this, argResolver);

            // The contract's message records carry no thread id (it is a property of
            // the thread), so each per-thread iterator is paired with its OS thread id.
            // Each thread stream is newest-first; the merge below matches the raw path.
            List<(IEnumerator<StressLogMessageInfo> Iterator, ulong ThreadId)> iterators = new();
            try
            {
                foreach (StressLogThreadInfo thread in _dac.EnumerateThreads())
                {
                    if (iterators.Count >= _options.MaxThreads)
                    {
                        RaiseDiagnostic(StressLogDiagnosticKind.LimitExceeded, thread.ThreadLogAddress);
                        break;
                    }

                    IEnumerator<StressLogMessageInfo> iter = _dac.EnumerateMessages(thread.ThreadLogAddress, cancellationToken).GetEnumerator();
                    if (iter.MoveNext())
                        iterators.Add((iter, thread.ThreadId));
                    else
                        iter.Dispose();
                }

                while (iterators.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int newest = 0;
                    for (int i = 1; i < iterators.Count; i++)
                    {
                        if (iterators[i].Iterator.Current.Timestamp > iterators[newest].Iterator.Current.Timestamp)
                            newest = i;
                    }

                    StressLogMessageInfo msg = iterators[newest].Iterator.Current;
                    ulong[] args = msg.Arguments ?? Array.Empty<ulong>();
                    int argCount = Math.Min(args.Length, StressLogConstants.MaxArgumentCount);
                    int gen = context.CaptureArgs(args.AsSpan(0, argCount));

                    yield return new StressLogMessage(
                        context: context,
                        generation: gen,
                        threadId: iterators[newest].ThreadId,
                        facility: msg.Facility,
                        timeStampTicks: msg.Timestamp,
                        formatAddress: msg.FormatAddress,
                        argumentCount: (byte)argCount);

                    if (!iterators[newest].Iterator.MoveNext())
                    {
                        iterators[newest].Iterator.Dispose();
                        iterators.RemoveAt(newest);
                    }
                }
            }
            finally
            {
                context.Clear();
                foreach ((IEnumerator<StressLogMessageInfo> Iterator, ulong ThreadId) entry in iterators)
                    entry.Iterator.Dispose();
            }
        }

        internal override double ElapsedSecondsFor(ulong timeStamp)
        {
            if (_tickFrequency == 0) return 0;
            ulong delta = timeStamp >= _startTimeStamp ? timeStamp - _startTimeStamp : 0;
            return (double)delta / _tickFrequency;
        }

        internal override StressLogKnownFormat LookupKnownFormat(ulong address)
        {
            if (address == 0) return StressLogKnownFormat.None;
            if (_formatCache.TryGet(address, out _, out StressLogKnownFormat known))
                return known;
            return StressLogKnownFormat.None;
        }

        internal override void FormatMessage<T>(ReadOnlySpan<ulong> args,
                                                ArgumentResolver argResolver,
                                                ulong formatAddress,
                                                ref T receiver)
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

            FormatStringParser.Parse(formatBytes.Span, args, argResolver, ref receiver);
        }

        private static readonly byte[] s_unresolvedFormat =
            System.Text.Encoding.ASCII.GetBytes("<unresolved-format>");

        protected override void DisposeCore()
        {
            if (_formatReader is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
