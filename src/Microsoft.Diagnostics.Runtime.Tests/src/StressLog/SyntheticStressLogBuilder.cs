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
        {
            threadAddr = ThreadAddr;
            chunkAddr = ChunkAddr;

            // Construct a chunk with one V4 message at the start, followed by zeros.
            byte[] chunk = new byte[StressLogLayout.Chunk_TotalSize];

            // prev = next = self (single-element ring)
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(StressLogLayout.Chunk_Prev), ChunkAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(StressLogLayout.Chunk_Next), ChunkAddr);

            // Write a single message with 0 args at the start of the buffer.
            // V4 header layout (little-endian, 16 bytes):
            //   word0 = facility(32) | numberOfArgs(6) | formatOffsetLow(26)
            //   word1 = formatOffsetHigh(13) | timeStamp(51)
            int msgOffset = StressLogLayout.Chunk_Buf;
            ulong word0 = Facility; // numberOfArgs = 0, formatOffsetLow = 0
            ulong word1 = MessageTimeStamp << StressLogConstants.FormatOffsetHighBits;
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset), word0);
            BinaryPrimitives.WriteUInt64LittleEndian(chunk.AsSpan(msgOffset + 8), word1);

            // Signatures.
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(StressLogLayout.Chunk_DwSig1Offset), StressLogConstants.ChunkSignature);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(StressLogLayout.Chunk_DwSig2Offset), StressLogConstants.ChunkSignature);

            Memory[ChunkAddr] = chunk;

            // Construct the ThreadStressLog (80 bytes, Core layout).
            byte[] thread = new byte[StressLogLayout.Thread_HeaderSize];
            // next = 0 (no further threads)
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_ThreadId), ThreadId);
            // isDead = readHasWrapped = writeHasWrapped = 0
            ulong msgAddr = ChunkAddr + (ulong)StressLogLayout.Chunk_Buf;
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_CurPtr), msgAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_ReadPtr), msgAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_ChunkListHead), ChunkAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_ChunkListTail), ChunkAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_CurReadChunk), ChunkAddr);
            BinaryPrimitives.WriteUInt64LittleEndian(thread.AsSpan(StressLogLayout.CoreThread_CurWriteChunk), ChunkAddr);
            Memory[ThreadAddr] = thread;

            // Construct the StressLog header (160 bytes, Core layout).
            byte[] header = new byte[StressLogLayout.InProc_HeaderSize];
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(StressLogLayout.InProc_Logs), ThreadAddr);
            // padding sentinel (0xFFFFFFFF) marks this as a Core in-process StressLog.
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(StressLogLayout.InProc_Padding), 0xFFFFFFFFu);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(StressLogLayout.InProc_TickFrequency), TickFrequency);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(StressLogLayout.InProc_StartTimeStamp), StartTimeStamp);
            // StartTime: leave 0 (no FILETIME)
            // ModuleOffset = 0 (no module table; format addresses won't resolve)
            Memory[StressLogAddr] = header;

            return StressLogAddr;
        }
    }
}
