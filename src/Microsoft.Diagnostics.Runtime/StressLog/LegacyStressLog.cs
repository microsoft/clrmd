// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// <see cref="StressLog"/> that reads threads and messages by parsing the
    /// runtime's stress log directly out of target memory. This is the back-compat
    /// path for runtimes whose DAC does not expose the structured stress-log contract,
    /// and the implementation behind the address-based <c>StressLog.TryOpen</c>
    /// overloads.
    /// </summary>
    internal sealed class LegacyStressLog : StressLog
    {
        private readonly IDataReader _reader;
        private readonly IMemoryReader _formatReader;
        private readonly StressLogLayout _layout;
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
        private readonly uint? _facilitiesToLog;
        private readonly uint? _levelToLog;
        private readonly uint? _maxSizePerThread;
        private readonly uint? _maxSizeTotal;
        private readonly int? _chunkCount;

        internal LegacyStressLog(IDataReader reader,
                          IMemoryReader? formatReader,
                          StressLogLayout layout,
                          StressLogOptions options,
                          AllocationBudget budget,
                          StressLogModuleTable modules,
                          ulong firstThreadAddr,
                          ulong tickFrequency,
                          ulong startTimeStamp,
                          DateTime? startTimeUtc,
                          bool isV4,
                          StressLogVariant variant,
                          uint? facilitiesToLog,
                          uint? levelToLog,
                          uint? maxSizePerThread,
                          uint? maxSizeTotal,
                          int? chunkCount)
        {
            _reader = reader;
            _formatReader = formatReader ?? reader;
            _layout = layout;
            _options = options;
            _budget = budget;
            _modules = modules;
            _firstThreadAddr = firstThreadAddr;
            _tickFrequency = tickFrequency;
            _startTimeStamp = startTimeStamp;
            _startTimeUtc = startTimeUtc;
            _isV4 = isV4;
            _variant = variant;
            _facilitiesToLog = facilitiesToLog;
            _levelToLog = levelToLog;
            _maxSizePerThread = maxSizePerThread;
            _maxSizeTotal = maxSizeTotal;
            _chunkCount = chunkCount;
            _formatCache = new FormatStringCache(_formatReader, budget, options.MaxFormatStringLength, options.MaxFormatStringCacheEntries);
        }

        public override DateTime? StartTimeUtc => _startTimeUtc;
        public override int PointerSize => _layout.PointerSize;
        public override ulong TickFrequency => _tickFrequency;
        public override uint? FacilitiesToLog => _facilitiesToLog;
        public override uint? LevelToLog => _levelToLog;
        public override uint? MaxSizePerThread => _maxSizePerThread;
        public override uint? MaxSizeTotal => _maxSizeTotal;
        public override int? ChunkCount => _chunkCount;

        /// <summary>
        /// Open the stress log, optionally supplying a <paramref name="formatReader"/>
        /// used to recover format-string and string-argument bytes (for example
        /// a reader that falls back to module images on disk). When
        /// <paramref name="formatReader"/> is <see langword="null"/> the dump
        /// <paramref name="reader"/> is used for those reads as well.
        /// </summary>
        internal static bool TryOpen(IDataReader reader, ulong address, IMemoryReader? formatReader, out StressLog? stressLog, out string? failureReason)
        {
            stressLog = null;
            failureReason = null;
            if (reader is null)
            {
                failureReason = "Data reader is null.";
                return false;
            }
            if (reader.PointerSize != 8 && reader.PointerSize != 4)
            {
                failureReason = $"Stress logs on a {reader.PointerSize * 8}-bit target are not supported; only 32-bit and 64-bit targets are.";
                return false;
            }
            if (reader.PointerSize == 4 && reader.TargetPlatform != OSPlatform.Windows)
            {
                // 32-bit Linux/macOS .NET runtimes have not shipped in a
                // supported configuration in years; we have no reference
                // dumps to validate the layout against. Reject explicitly.
                failureReason = "Stress logs on a 32-bit non-Windows target are not supported.";
                return false;
            }
            if (address == 0)
            {
                failureReason = "Stress log address is 0.";
                return false;
            }

            // Choose a preliminary Core layout based on pointer size; if
            // variant detection identifies a Framework target later, we
            // swap to the matching Framework layout.
            StressLogLayout layout = StressLogLayout.ForCore(reader.PointerSize);
            StressLogOptions options = StressLogOptions.Default;
            AllocationBudget budget = new AllocationBudget(options.MaxTotalBytesAllocated);

            // Speculatively read the larger memory-mapped header. If the magic
            // does not match, treat as in-process and use only the first
            // InProcHeaderSize bytes. Memory-mapped logs are 64-bit only;
            // on 32-bit the magic check will always fail.
            int speculativeReadSize = layout.Is64Bit
                ? Math.Max(layout.MmHeaderReadSize, layout.InProcHeaderSize)
                : layout.InProcHeaderSize;
            Span<byte> header = stackalloc byte[512]; // upper bound for any layout
            if (speculativeReadSize > header.Length)
            {
                // Defensive: any future layout change that bloats the header
                // beyond 512 bytes would land here. Today the worst case is
                // 272 (Mm x64).
                failureReason = "Speculative header read size exceeds the preallocated buffer.";
                return false;
            }
            header = header.Slice(0, speculativeReadSize);

            int got = reader.Read(address, header);
            if (got < layout.InProcHeaderSize)
            {
                failureReason = $"Could not read the stress log header at 0x{address:x} (got {got} bytes, expected at least {layout.InProcHeaderSize}).";
                return false;
            }

            uint maybeMagic = layout.Is64Bit && got >= layout.MmVersionOffset
                ? BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.MmMagicOffset))
                : 0;

            bool isMemoryMapped = layout.Is64Bit
                                  && got >= layout.MmHeaderReadSize
                                  && maybeMagic == StressLogConstants.MemoryMappedMagic;

            if (isMemoryMapped)
            {
                stressLog = OpenMemoryMapped(reader, formatReader, layout, options, budget, header);
            }
            else
            {
                stressLog = OpenInProcess(reader, formatReader, layout, options, budget, header.Slice(0, layout.InProcHeaderSize));
            }

            if (stressLog is null)
            {
                failureReason = "Stress log header failed validation.";
                return false;
            }
            return true;
        }

        private static StressLog? OpenInProcess(IDataReader reader,
                                                IMemoryReader? formatReader,
                                                StressLogLayout layout,
                                                StressLogOptions options,
                                                AllocationBudget budget,
                                                ReadOnlySpan<byte> header)
        {
            // Variant detection. Modern CoreCLR initializes the 'padding'
            // field at StressLog InProcPaddingOffset to UINT_MAX (0xFFFFFFFF) as a
            // sentinel; the .NET Framework runtime stores its TLS slot index
            // there (a small positive integer). This is a stable signal we
            // verified across all five integration-test dump variants.
            uint sentinel = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.InProcPaddingOffset));
            StressLogVariant variant = sentinel == 0xFFFFFFFFu
                ? StressLogVariant.Core
                : StressLogVariant.FrameworkV1;

            // Swap to the Framework layout if needed; ThreadStressLog and
            // StressMsg layouts differ from Core.
            if (variant == StressLogVariant.FrameworkV1)
                layout = layout.WithFramework();

            ulong logs = layout.ReadTargetPointer(header, layout.InProcLogsOffset);
            ulong tickFreq = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(layout.InProcTickFrequencyOffset));
            ulong startTs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(layout.InProcStartTimeStampOffset));
            ulong startFiletime = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(layout.InProcStartTimeOffset));
            ulong moduleOffset = layout.ReadTargetPointer(header, layout.InProcModuleOffsetOffset);
            uint facilitiesToLog = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.InProcFacilitiesToLogOffset));
            uint levelToLog = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.InProcLevelToLogOffset));
            uint maxSizePerThread = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.InProcMaxSizePerThreadOffset));
            uint maxSizeTotal = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.InProcMaxSizeTotalOffset));
            int chunkCount = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(layout.InProcTotalChunkOffset));

            DateTime? startTimeUtc = FileTimeToUtc(startFiletime);

            // Framework's StressLog struct has no module table appended; the
            // bytes we speculatively read past the header end are unrelated
            // heap memory. Restrict module table parsing to Core, and let
            // Framework fall through to the single-module path keyed off
            // moduleOffset.
            StressLogModuleTable modules;
            if (variant == StressLogVariant.Core)
            {
                int entriesByteCount = StressLogConstants.MaxModules * layout.ModuleEntrySize;
                ReadOnlySpan<byte> entries = header.Slice(layout.InProcModulesOffset, entriesByteCount);
                modules = StressLogModuleTable.BuildInProcess(
                    layout: layout,
                    entries: entries,
                    legacyModuleOffset: moduleOffset,
                    hasModuleTable: true,
                    maxOffset: StressLogConstants.FormatOffsetMax);
            }
            else
            {
                modules = StressLogModuleTable.BuildInProcess(
                    layout: layout,
                    entries: ReadOnlySpan<byte>.Empty,
                    legacyModuleOffset: moduleOffset,
                    hasModuleTable: false,
                    maxOffset: StressLogConstants.FormatOffsetMax);
            }

            // V4 message decoding has been used since the addition of the
            // module table in CoreCLR; Framework V1 predates it and uses V3.
            bool isV4 = variant == StressLogVariant.Core;

            return new LegacyStressLog(reader, formatReader, layout, options, budget, modules, logs, tickFreq, startTs, startTimeUtc, isV4: isV4, variant: variant,
                facilitiesToLog: facilitiesToLog, levelToLog: levelToLog, maxSizePerThread: maxSizePerThread, maxSizeTotal: maxSizeTotal, chunkCount: chunkCount);
        }

        private static StressLog? OpenMemoryMapped(IDataReader reader,
                                                   IMemoryReader? formatReader,
                                                   StressLogLayout layout,
                                                   StressLogOptions options,
                                                   AllocationBudget budget,
                                                   ReadOnlySpan<byte> header)
        {
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(layout.MmVersionOffset));
            if (version != StressLogConstants.MemoryMappedVersionV1
                && version != StressLogConstants.MemoryMappedVersionV2)
            {
                return null;
            }

            ulong logs = layout.ReadTargetPointer(header, layout.MmLogsOffset);
            ulong tickFreq = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(layout.MmTickFrequencyOffset));
            ulong startTs = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(layout.MmStartTimeStampOffset));

            int entriesByteCount = StressLogConstants.MaxModules * layout.ModuleEntrySize;
            ReadOnlySpan<byte> entries = header.Slice(layout.MmModulesOffset, entriesByteCount);

            ulong maxOffset = version == StressLogConstants.MemoryMappedVersionV1
                ? (1UL << StressLogConstants.FormatOffsetLowBits)
                : StressLogConstants.FormatOffsetMax;

            StressLogModuleTable modules = StressLogModuleTable.BuildMemoryMapped(layout, entries, maxOffset);

            return new LegacyStressLog(reader, formatReader, layout, options, budget, modules, logs, tickFreq, startTs, startTimeUtc: null, isV4: true, variant: StressLogVariant.Core,
                facilitiesToLog: null, levelToLog: null, maxSizePerThread: null, maxSizeTotal: null, chunkCount: null);
        }

        public override IEnumerable<MemoryRange> EnumerateMemoryRanges()
        {
            ThrowIfDisposed();

            // Conservative per-entry charge for the visited-chunk set, so a crafted
            // log with many disjoint chunk chains is bounded by the allocation budget
            // (the per-thread MaxChunksPerThread bound resets per thread and does NOT
            // bound the global set).
            const int visitedChunkBytes = 32;

            // Track visited threads and chunks so a corrupt log whose links form a
            // cycle (rather than terminating at the list head) stays bounded: each
            // node is yielded at most once and revisiting one ends the walk.
            HashSet<ulong> visitedThreads = new();
            HashSet<ulong> visitedChunks = new();
            int reservedBytes = 0;
            try
            {
                ulong threadAddr = _firstThreadAddr;
                while (threadAddr != 0)
                {
                    if (visitedThreads.Count >= _options.MaxThreads)
                    {
                        RaiseDiagnostic(StressLogDiagnosticKind.LimitExceeded, threadAddr);
                        yield break;
                    }
                    if (!visitedThreads.Add(threadAddr))
                    {
                        // Thread-list cycle: we cannot make progress, so stop.
                        RaiseDiagnostic(StressLogDiagnosticKind.CorruptThread, threadAddr);
                        yield break;
                    }

                    // Walk this thread's chunk ring. A failure here is isolated to
                    // the thread: report it and fall through to the next thread.
                    // chunkListHead == 0 is a legitimately empty/dead thread.
                    if (TryReadTargetPointer(threadAddr + (ulong)_layout.ThreadChunkListHeadOffset, out ulong chunkListHead))
                    {
                        ulong chunkAddr = chunkListHead;
                        int chunkCount = 0;
                        while (chunkAddr != 0 && chunkCount < _options.MaxChunksPerThread)
                        {
                            // Skip a chunk whose range would wrap past the end of the
                            // address space (mirrors ChunkReader's ChunkFits guard).
                            if (!_layout.ChunkFits(chunkAddr))
                            {
                                RaiseDiagnostic(StressLogDiagnosticKind.CorruptChunk, chunkAddr);
                                break;
                            }
                            if (!visitedChunks.Add(chunkAddr))
                            {
                                // Non-head cycle or cross-thread chunk reuse.
                                RaiseDiagnostic(StressLogDiagnosticKind.CorruptChunk, chunkAddr);
                                break;
                            }
                            if (!_budget.TryReserve(visitedChunkBytes))
                            {
                                // Global memory ceiling: stop the whole enumeration.
                                RaiseDiagnostic(StressLogDiagnosticKind.LimitExceeded, chunkAddr);
                                yield break;
                            }
                            reservedBytes += visitedChunkBytes;

                            if (!TryReadTargetPointer(chunkAddr + (ulong)_layout.ChunkNextOffset, out ulong nextChunk))
                            {
                                RaiseDiagnostic(StressLogDiagnosticKind.ReadMemoryFailed, chunkAddr);
                                break;
                            }

                            yield return new MemoryRange(chunkAddr, chunkAddr + (ulong)_layout.ChunkTotalSize);

                            if (nextChunk == chunkListHead)
                                break;

                            chunkAddr = nextChunk;
                            chunkCount++;
                        }
                    }
                    else
                    {
                        RaiseDiagnostic(StressLogDiagnosticKind.ReadMemoryFailed, threadAddr);
                    }

                    // Advance to the next thread; without the link we cannot continue.
                    if (!TryReadTargetPointer(threadAddr + (ulong)_layout.ThreadNextOffset, out ulong nextThread))
                    {
                        RaiseDiagnostic(StressLogDiagnosticKind.ReadMemoryFailed, threadAddr);
                        yield break;
                    }
                    threadAddr = nextThread;
                }
            }
            finally
            {
                _budget.Release(reservedBytes);
            }
        }

        private bool TryReadTargetPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[8];
            buffer = buffer.Slice(0, _layout.PointerSize);
            if (_reader.Read(address, buffer) < _layout.PointerSize)
            {
                value = 0;
                return false;
            }

            value = _layout.ReadTargetPointer(buffer, 0);
            return true;
        }

        public override IEnumerable<StressLogMessage> EnumerateMessages(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Each enumeration owns its own context so concurrent foreach loops
            // do not clobber each other's argument scratch / current iterator.
            ArgumentResolver argResolver = new ArgumentResolver(_formatReader, _options.MaxStringArgumentLength, _layout.PointerSize);
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
                // Iterator count is bounded by StressLogOptions.MaxThreads
                // (100,000), but real dumps typically have tens to hundreds
                // of threads, where the scan is dwarfed by the per-message
                // I/O performed by the outer loop. A min-heap would cut the
                // per-message cost to O(log threads) at the cost of an extra
                // allocation per enumeration plus reheap-on-advance complexity;
                // we keep the linear scan because the simplicity wins at the
                // thread counts we actually see.
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

            Span<byte> threadHeader = stackalloc byte[StressLogConstants.MaxThreadHeaderBytes];
            threadHeader = threadHeader.Slice(0, _layout.ThreadHeaderSize);
            Action<StressLogDiagnostic> raiseDiagnostic = RaiseDiagnostic;

            while (addr != 0)
            {
                if (iterators.Count >= _options.MaxThreads)
                {
                    RaiseDiagnostic(StressLogDiagnosticKind.LimitExceeded, addr);
                    break;
                }

                if (!visited.Add(addr))
                {
                    RaiseDiagnostic(StressLogDiagnosticKind.CorruptThread, addr);
                    break;
                }

                int got = _reader.Read(addr, threadHeader);
                if (got < _layout.ThreadHeaderSize)
                {
                    RaiseDiagnostic(StressLogDiagnosticKind.ReadMemoryFailed, addr);
                    break;
                }

                ulong nextAddr = _layout.ReadTargetPointer(threadHeader, _layout.ThreadNextOffset);
                ThreadIterator iter = new ThreadIterator(_reader, _layout, _options, _budget,
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
            // Dispose a format reader we own (the module-image fallback wraps
            // the dump reader but holds its own ElfFile handles). The dump
            // reader itself is owned by the DataTarget, so we never dispose it.
            if (!ReferenceEquals(_formatReader, _reader) && _formatReader is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
