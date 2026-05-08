// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Per-thread iterator that walks the chunked, circular message buffer of
    /// a single <c>ThreadStressLog</c>. Yields raw decoded messages in
    /// reverse-chronological order (newest first).
    /// </summary>
    /// <remarks>
    /// <para>
    /// All linked-list and address arithmetic in this file validates each
    /// byte before using it: every chunk transition is bounded by
    /// <see cref="StressLogOptions.MaxChunksPerThread"/> and visited-address
    /// tracking, every per-message advance is validated against chunk bounds,
    /// and every <c>Read</c> is checked for short returns. Failures stop the
    /// iterator and surface a <see cref="StressLogDiagnostic"/> to the
    /// owning <see cref="StressLog"/>; they never throw.
    /// </para>
    /// <para>
    /// The decoded message exposes only validated metadata. Argument bytes
    /// remain in the chunk buffer; consumers extract them via <see cref="GetArgument"/>.
    /// </para>
    /// </remarks>
    internal sealed class ThreadIterator : IDisposable
    {
        private readonly IDataReader _reader;
        private readonly StressLogOptions _options;
        private readonly bool _isV4;
        private readonly StressLogVariant _variant;
        private readonly int _msgHeaderSize;
        private readonly Action<StressLogDiagnostic>? _diag;

        private ChunkReader _chunk;
        private readonly HashSet<ulong> _visitedChunks = new();

        private readonly ulong _curWriteChunk;
        private readonly ulong _curPtr;
        private readonly ulong _chunkListTail;
        private readonly bool _writeHasWrapped;

        private bool _readHasWrapped;
        private bool _revisitedWriteChunk;
        private int _messagesEmitted;
        private int _readOffset;

        public ulong OSThreadId { get; }
        public bool Exhausted { get; private set; }

        // Latest decoded message fields; consumer reads via accessors after each Advance().
        public uint Facility { get; private set; }
        public ulong TimeStamp { get; private set; }
        public ulong FormatOffset { get; private set; }
        public int ArgumentCount { get; private set; }
        private int _argsOffset;

        public ThreadIterator(IDataReader reader,
                              StressLogOptions options,
                              AllocationBudget budget,
                              Action<StressLogDiagnostic>? diagnostic,
                              ulong threadAddress,
                              ReadOnlySpan<byte> threadHeader,
                              bool isV4,
                              StressLogVariant variant)
        {
            _reader = reader;
            _options = options;
            _diag = diagnostic;
            _isV4 = isV4;
            _variant = variant;
            _msgHeaderSize = variant == StressLogVariant.FrameworkV1
                ? StressLogLayout.Msg_HeaderSizeFxV1
                : StressLogLayout.Msg_HeaderSize;
            _chunk = new ChunkReader(budget);

            ulong curReadChunk;
            ulong readPtr;
            byte writeWrapped;

            if (variant == StressLogVariant.FrameworkV1)
            {
                OSThreadId = BinaryPrimitives.ReadUInt32LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_ThreadId));
                _curPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_CurPtr));
                readPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_ReadPtr));
                uint writeWrapped32 = BinaryPrimitives.ReadUInt32LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_WriteHasWrapped));
                writeWrapped = (byte)(writeWrapped32 != 0 ? 1 : 0);
                _chunkListTail = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_ChunkListTail));
                curReadChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_CurReadChunk));
                _curWriteChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.FxThread_CurWriteChunk));
            }
            else
            {
                OSThreadId = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_ThreadId));
                writeWrapped = threadHeader[StressLogLayout.CoreThread_WriteHasWrapped];
                _curPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_CurPtr));
                readPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_ReadPtr));
                _chunkListTail = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_ChunkListTail));
                curReadChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_CurReadChunk));
                _curWriteChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.CoreThread_CurWriteChunk));
            }

            // The runtime writer never initializes readHasWrapped (it is only
            // set by the readonly Activate() and by AdvReadPastBoundary in the
            // SOS DAC code path). Treat the dump's bytes as junk and start from
            // the readonly initial state described in stresslog.h.
            _readHasWrapped = false;
            _writeHasWrapped = writeWrapped != 0;

            // Replicate the readonly Activate() seed: read from curWriteChunk/
            // curPtr. If curWriteChunk is zero, fall back to curReadChunk/readPtr.
            ulong startChunk = _curWriteChunk != 0 ? _curWriteChunk : curReadChunk;
            ulong startPtr = _curPtr != 0 ? _curPtr : readPtr;

            if (startChunk == 0 || !LoadChunk(startChunk))
            {
                Exhausted = true;
                return;
            }

            if (!_chunk.TryAddressToOffset(startPtr, out _readOffset))
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptThread, threadAddress));
                Exhausted = true;
                return;
            }

            _ = threadAddress; // address kept for diagnostics only; not referenced afterwards
        }

        /// <summary>
        /// Advance to the next message. Returns <see langword="false"/> if the
        /// log has been fully drained (or terminated due to corruption).
        /// </summary>
        public bool Advance()
        {
            if (Exhausted) return false;
            if (_messagesEmitted >= _options.MaxMessagesPerThread)
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.LimitExceeded, _chunk.Address));
                Exhausted = true;
                return false;
            }

            ReadOnlySpan<byte> body = _chunk.Buffer;
            if (_readOffset < 0 || _readOffset + _msgHeaderSize > body.Length)
            {
                if (!CrossChunkBoundary())
                    return false;
                body = _chunk.Buffer;
            }

            // Decode the message header.
            ReadOnlySpan<byte> header = body.Slice(_readOffset, _msgHeaderSize);
            uint facility;
            int numArgs;
            ulong formatOffset;
            ulong timeStamp;
            if (_variant == StressLogVariant.FrameworkV1)
                StressLogLayout.DecodeMessageFxV1(header, out facility, out numArgs, out formatOffset, out timeStamp);
            else if (_isV4)
                StressLogLayout.DecodeMessageV4(header, out facility, out numArgs, out formatOffset, out timeStamp);
            else
                StressLogLayout.DecodeMessageV3(header, out facility, out numArgs, out formatOffset, out timeStamp);

            // Termination: timeStamp == 0 means we've reached the zero-padded region
            // at the start of the current chunk that the writer skipped.
            if (timeStamp == 0)
            {
                Exhausted = true;
                return false;
            }

            // Validate numberOfArgs and chunk bounds.
            if ((uint)numArgs > StressLogConstants.MaxArgumentCount)
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptMessage, _chunk.BufferStartAddress + (ulong)_readOffset));
                Exhausted = true;
                return false;
            }

            int totalSize = _msgHeaderSize + numArgs * StressLogLayout.Msg_ArgSize;
            if (_readOffset + totalSize > body.Length)
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptMessage, _chunk.BufferStartAddress + (ulong)_readOffset));
                Exhausted = true;
                return false;
            }

            Facility = facility;
            TimeStamp = timeStamp;
            FormatOffset = formatOffset;
            ArgumentCount = numArgs;
            _argsOffset = _readOffset + _msgHeaderSize;

            // Termination: read pointer reached the write pointer. Mirrors
            // the runtime's CompletedDump check (stresslog.h): once
            // readHasWrapped, stopping happens when we re-enter curWriteChunk
            // and reach curPtr. Do NOT emit this final message: in the
            // wrapped case we already emitted the one at curPtr on the very
            // first decode, and re-emitting it would duplicate. In the
            // non-wrapped case _readHasWrapped is false so this branch is
            // not taken at all.
            ulong messageAddr = _chunk.BufferStartAddress + (ulong)_readOffset;
            if (_chunk.Address == _curWriteChunk
                && messageAddr >= _curPtr
                && _readHasWrapped)
            {
                Exhausted = true;
                return false;
            }

            // Advance read offset past this message.
            _readOffset += totalSize;
            _messagesEmitted++;

            return true;
        }

        public ulong GetArgument(int index)
        {
            if ((uint)index >= (uint)ArgumentCount) return 0;
            ReadOnlySpan<byte> body = _chunk.Buffer;
            int offset = _argsOffset + index * StressLogLayout.Msg_ArgSize;
            if (offset + StressLogLayout.Msg_ArgSize > body.Length) return 0;
            return BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(offset));
        }

        private bool CrossChunkBoundary()
        {
            // Mirror AdvReadPastBoundary: if we just exhausted chunkListTail and
            // write hasn't wrapped, we've read everything the writer left valid.
            if (_chunk.Address == _chunkListTail)
            {
                _readHasWrapped = true;
                if (!_writeHasWrapped)
                {
                    Exhausted = true;
                    return false;
                }
            }

            ulong nextAddr = _chunk.Next;
            if (nextAddr == 0)
            {
                Exhausted = true;
                return false;
            }

            // Allow re-entry to _curWriteChunk exactly once after wrapping.
            // The runtime's chunk list is circular; after walking around the
            // ring we re-enter curWriteChunk at offset 0 to read the messages
            // written from buffer-start up to curPtr. The per-message
            // termination above stops us before crossing curPtr a second time.
            if (!LoadChunk(nextAddr))
                return false;

            // Skip leading zero pointer-words up to maxMsgSize() words. The
            // writer pads unused space at the start of chunks with NULs.
            ReadOnlySpan<byte> body = _chunk.Buffer;
            int maxSkipWords = (_msgHeaderSize + StressLogConstants.MaxArgumentCount * StressLogLayout.Msg_ArgSize) / StressLogLayout.Msg_ArgSize;
            int offset = 0;
            for (int w = 0; w < maxSkipWords && offset + StressLogLayout.Msg_ArgSize <= body.Length; w++)
            {
                ulong word = BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(offset));
                if (word != 0) break;
                offset += StressLogLayout.Msg_ArgSize;
            }

            _readOffset = offset;
            return true;
        }

        private bool LoadChunk(ulong address)
        {
            if (_visitedChunks.Count >= _options.MaxChunksPerThread)
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.LimitExceeded, address));
                return false;
            }

            if (!_visitedChunks.Add(address))
            {
                // The legitimate case for re-visiting a chunk is re-entering
                // _curWriteChunk after the read pointer has wrapped all the
                // way around the ring. Allow this exactly once; any other
                // duplicate is treated as a cycle in the chunk list.
                if (address == _curWriteChunk
                    && _readHasWrapped
                    && _writeHasWrapped
                    && !_revisitedWriteChunk)
                {
                    _revisitedWriteChunk = true;
                }
                else
                {
                    _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptChunk, address));
                    return false;
                }
            }

            if (!_chunk.TryLoad(_reader, address))
            {
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptChunk, address));
                return false;
            }

            return true;
        }

        public void Dispose() => _chunk.Release();
    }
}
