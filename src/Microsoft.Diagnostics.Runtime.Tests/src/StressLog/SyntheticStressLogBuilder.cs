// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.StressLogs.Internal;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Builds a minimal but valid in-process stress log image for end-to-end
    /// tests. The image consists of one <c>StressLog</c> header, one
    /// <c>ThreadStressLog</c>, and one <c>StressLogChunk</c> containing a
    /// single zero-argument message.
    /// </summary>
    internal sealed class SyntheticStressLogBuilder
    {
        public const ulong StressLogAddr = 0x100000;
        public const ulong ThreadAddr    = 0x200000;
        public const ulong ChunkAddr     = 0x300000;
        public const ulong ThreadId      = 0x42;
        public const uint  Facility      = 0xABCDEF01;
        public const ulong TickFrequency = 10_000_000UL; // 10 MHz
        public const ulong StartTimeStamp = 1_000_000UL;
        public const ulong MessageTimeStamp = 1_500_000UL;

        public Dictionary<ulong, byte[]> Memory { get; } = new();

        public ulong Build(out ulong threadAddr, out ulong chunkAddr)
            => Build(StressLogLayout.CoreX64, out threadAddr, out chunkAddr);

        public ulong Build(StressLogLayout layout, out ulong threadAddr, out ulong chunkAddr)
        {
            threadAddr = ThreadAddr;
            chunkAddr = ChunkAddr;

            // Construct a chunk with one V4 message at the start, followed by zeros.
            byte[] chunk = new byte[layout.ChunkTotalSize];

            // prev = next = self (single-element ring)
            WritePointer(layout, chunk.AsSpan(layout.ChunkPrevOffset), ChunkAddr);
            WritePointer(layout, chunk.AsSpan(layout.ChunkNextOffset), ChunkAddr);

            // Write a single message at the start of the buffer.
            // For Core (V4) layout (little-endian, 16 bytes):
            //   word0 = facility(32) | numberOfArgs(6) | formatOffsetLow(26)
            //   word1 = formatOffsetHigh(13) | timeStamp(51)
            int msgOffset = layout.ChunkBufOffset;
            ulong word0 = Facility; // numberOfArgs = 0, formatOffsetLow = 0
            ulong word1 = MessageTimeStamp << StressLogConstants.FormatOffsetHighBits;
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset), word0);
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset + 8), word1);

            // Signatures.
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(layout.ChunkSig1Offset), StressLogConstants.ChunkSignature);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(layout.ChunkSig2Offset), StressLogConstants.ChunkSignature);

            Memory[ChunkAddr] = chunk;

            // Construct the ThreadStressLog.
            byte[] thread = new byte[layout.ThreadHeaderSize];
            // next = 0 (no further threads)
            // threadId
            if (layout.ThreadIdSize == 8)
                BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(layout.ThreadIdOffset), ThreadId);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(thread.AsSpan(layout.ThreadIdOffset), (uint)ThreadId);
            // isDead = readHasWrapped = writeHasWrapped = 0
            ulong msgAddr = ChunkAddr + (ulong)layout.ChunkBufOffset;
            WritePointer(layout, thread.AsSpan(layout.ThreadCurPtrOffset), msgAddr);
            WritePointer(layout, thread.AsSpan(layout.ThreadReadPtrOffset), msgAddr);
            WritePointer(layout, thread.AsSpan(layout.ThreadChunkListHeadOffset), ChunkAddr);
            WritePointer(layout, thread.AsSpan(layout.ThreadChunkListTailOffset), ChunkAddr);
            WritePointer(layout, thread.AsSpan(layout.ThreadCurReadChunkOffset), ChunkAddr);
            WritePointer(layout, thread.AsSpan(layout.ThreadCurWriteChunkOffset), ChunkAddr);
            Memory[ThreadAddr] = thread;

            // Construct the StressLog header.
            byte[] header = new byte[layout.InProcHeaderSize];
            WritePointer(layout, header.AsSpan(layout.InProcLogsOffset), ThreadAddr);
            // padding sentinel (0xFFFFFFFF) marks this as a Core in-process StressLog.
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(layout.InProcPaddingOffset), 0xFFFFFFFFu);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(layout.InProcTickFrequencyOffset), TickFrequency);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(layout.InProcStartTimeStampOffset), StartTimeStamp);
            // StartTime: leave 0 (no FILETIME)
            // ModuleOffset = 0 (no module table; format addresses won't resolve)
            Memory[StressLogAddr] = header;

            return StressLogAddr;
        }

        private static void WritePointer(StressLogLayout layout, Span<byte> dst, ulong value)
        {
            if (layout.PointerSize == 8)
                BinaryPrimitives.WriteUInt64LittleEndian(dst, value);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(dst, (uint)value);
        }
    }
}
