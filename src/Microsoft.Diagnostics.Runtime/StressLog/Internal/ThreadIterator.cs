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
        private readonly Action<StressLogDiagnostic>? _diag;

        private ChunkReader _chunk;
        private readonly HashSet<ulong> _visitedChunks = new();

        private readonly ulong _curWriteChunk;
        private readonly ulong _curPtr;
        private readonly ulong _chunkListTail;
        private readonly bool _writeHasWrapped;

        private bool _readHasWrapped;
        private int _messagesEmitted;
        private int _readOffset;

        public ulong ThreadId { get; }
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
                              bool isV4)
        {
            _reader = reader;
            _options = options;
            _diag = diagnostic;
            _isV4 = isV4;
            _chunk = new ChunkReader(budget);

            ThreadId = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_ThreadId));
            byte readWrapped = threadHeader[StressLogLayout.Thread_ReadHasWrapped];
            byte writeWrapped = threadHeader[StressLogLayout.Thread_WriteHasWrapped];
            _readHasWrapped = readWrapped != 0;
            _writeHasWrapped = writeWrapped != 0;
            _curPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_CurPtr));
            ulong readPtr = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_ReadPtr));
            _chunkListTail = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_ChunkListTail));
            ulong curReadChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_CurReadChunk));
            _curWriteChunk = BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(StressLogLayout.Thread_CurWriteChunk));

            // Replicate the ReadOnly-mode constructor: start reading at curWriteChunk/curPtr.
            // Many real-world dumps have curReadChunk/readPtr not set; trust curWriteChunk/curPtr.
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
            if (_readOffset < 0 || _readOffset + StressLogLayout.Msg_HeaderSize > body.Length)
            {
                if (!CrossChunkBoundary())
                    return false;
                body = _chunk.Buffer;
            }

            // Decode the message header.
            ReadOnlySpan<byte> header = body.Slice(_readOffset, StressLogLayout.Msg_HeaderSize);
            uint facility;
            int numArgs;
            ulong formatOffset;
            ulong timeStamp;
            if (_isV4)
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

            int totalSize = StressLogLayout.Msg_HeaderSize + numArgs * StressLogLayout.Msg_ArgSize;
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
            _argsOffset = _readOffset + StressLogLayout.Msg_HeaderSize;

            // Termination: read pointer reached the write pointer.
            ulong messageAddr = _chunk.BufferStartAddress + (ulong)_readOffset;
            bool atWriteHead = _chunk.Address == _curWriteChunk
                               && messageAddr >= _curPtr
                               && _readHasWrapped;

            // Advance read offset past this message.
            _readOffset += totalSize;
            _messagesEmitted++;

            if (atWriteHead)
            {
                Exhausted = true;
                // Still emit this final message (it was at curPtr, valid).
            }

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

            // Natural full-circle termination: we've returned to the chunk we
            // started at, and we already observed the read-wrap sentinel. The
            // writer's curPtr-anchored termination test will be evaluated
            // against the next message we decode.
            if (nextAddr == _curWriteChunk && _readHasWrapped)
            {
                Exhausted = true;
                return false;
            }

            if (!LoadChunk(nextAddr))
                return false;

            // Skip leading zero pointer-words up to maxMsgSize() words. The
            // writer pads unused space at the start of chunks with NULs.
            ReadOnlySpan<byte> body = _chunk.Buffer;
            int maxSkipWords = (StressLogLayout.Msg_HeaderSize + StressLogConstants.MaxArgumentCount * StressLogLayout.Msg_ArgSize) / StressLogLayout.Msg_ArgSize;
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
                _diag?.Invoke(new StressLogDiagnostic(StressLogDiagnosticKind.CorruptChunk, address));
                return false;
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
