// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Per-target description of the on-disk struct layouts that make up a
    /// runtime stress log. A single instance captures pointer size, OS, and
    /// runtime variant (Core vs .NET Framework V1) so that consumers can
    /// read fields by symbolic name regardless of the target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bit-field decoders (<see cref="DecodeMessageV3"/>,
    /// <see cref="DecodeMessageV4"/>, <see cref="DecodeMessageFxV1"/>) are
    /// independent of pointer size and remain static methods on this type.
    /// </para>
    /// <para>
    /// All pointer-sized field reads MUST go through
    /// <see cref="ReadTargetPointer"/>. Reads done with
    /// <c>BinaryPrimitives.ReadInt32LittleEndian</c> would sign-extend a
    /// high-bit (≥ 0x80000000) 32-bit pointer, producing a malformed
    /// 64-bit address; reads done with <c>ReadUInt64LittleEndian</c> on a
    /// 32-bit target would consume four bytes of an unrelated adjacent
    /// field. Centralizing in a single helper avoids both classes of bug.
    /// </para>
    /// </remarks>
    internal sealed class StressLogLayout
    {
        /// <summary>Pointer size on the target, in bytes (4 or 8).</summary>
        public int PointerSize { get; }

        /// <summary>
        /// Largest valid target address. <c>uint.MaxValue</c> on 32-bit;
        /// <c>ulong.MaxValue</c> on 64-bit. Use this as the upper bound on
        /// all address arithmetic to reject overflow that would otherwise
        /// wrap a malformed dump's pointers back into the legal range.
        /// </summary>
        public ulong MaxAddress { get; }

        /// <summary>True if the target is a 64-bit target.</summary>
        public bool Is64Bit => PointerSize == 8;

        /// <summary>True if the target is the legacy .NET Framework runtime.</summary>
        public bool IsFramework { get; }

        // -----------------------------------------------------------------
        // StressLog struct (in-process). Fields up through 'totalChunk'
        // share offsets with both pointer sizes; everything past 'logs'
        // shifts because the pointer size changes.
        // -----------------------------------------------------------------

        public int InProcFacilitiesToLogOffset => 0;   // uint
        public int InProcLevelToLogOffset      => 4;   // uint
        public int InProcMaxSizePerThreadOffset => 8;  // uint
        public int InProcMaxSizeTotalOffset    => 12;  // uint
        public int InProcTotalChunkOffset      => 16;  // int (Volatile<LONG>)

        // 'logs' is the first pointer-sized field. On x86 there's no
        // pre-padding (totalChunk is 4 bytes, naturally aligned for a
        // 4-byte pointer); on x64 there are 4 bytes of padding inserted
        // for 8-byte alignment.
        public int InProcLogsOffset { get; }
        public int InProcPaddingOffset { get; }
        public int InProcDeadCountOffset { get; }
        public int InProcLockOffset { get; }
        public int InProcTickFrequencyOffset { get; }
        public int InProcStartTimeStampOffset { get; }
        public int InProcStartTimeOffset { get; }
        public int InProcModuleOffsetOffset { get; }
        public int InProcModulesOffset { get; }
        public int InProcHeaderSize { get; }

        // -----------------------------------------------------------------
        // Memory-mapped StressLogHeader. Memory-mapped logs only exist on
        // 64-bit hosts; on 32-bit the offsets here are unused but we keep
        // them populated for symmetry. TryOpen rejects MM logs when
        // <see cref="PointerSize"/> != 8 by checking the magic.
        // -----------------------------------------------------------------

        public int MmHeaderSizeOffset       => 0;
        public int MmMagicOffset            => 8;
        public int MmVersionOffset          => 12;
        public int MmMemoryBaseOffset       { get; }
        public int MmMemoryCurOffset        { get; }
        public int MmMemoryLimitOffset      { get; }
        public int MmLogsOffset             { get; }
        public int MmTickFrequencyOffset    { get; }
        public int MmStartTimeStampOffset   { get; }
        public int MmThreadsWithNoLogOffset { get; }
        public int MmModulesOffset          { get; }
        public int MmHeaderReadSize         { get; }

        // -----------------------------------------------------------------
        // ThreadStressLog struct.
        //
        // Core layout:
        //   [P]   next                    pointer
        //   [8]   threadId                always uint64
        //   [1]   isDead
        //   [1]   readHasWrapped (junk in writer)
        //   [1]   writeHasWrapped
        //   pad to pointer alignment
        //   [P]   curPtr
        //   [P]   readPtr
        //   [P]   chunkListHead
        //   [P]   chunkListTail
        //   [P]   curReadChunk
        //   [P]   curWriteChunk
        //   [4]   chunkListLength (long)
        //
        // Framework layout (pre-Core; threadId is uint32, the write-wrapped
        // flag is a full uint32, etc.):
        //   [P]   next
        //   [4]   threadId
        //   [4]   isDead
        //   [P]   curPtr
        //   [P]   readPtr
        //   [4]   readHasWrapped
        //   [4]   writeHasWrapped
        //   [P]   chunkListHead
        //   [P]   chunkListTail
        //   [P]   curReadChunk
        //   [P]   curWriteChunk
        //   [4]   chunkListLength
        // -----------------------------------------------------------------

        public int ThreadNextOffset           => 0;
        public int ThreadIdOffset             { get; }
        public int ThreadIdSize               { get; }   // 8 on Core, 4 on Framework
        public int ThreadIsDeadOffset         { get; }
        public int ThreadReadHasWrappedOffset { get; }   // never trusted; see remarks
        public int ThreadWriteHasWrappedOffset { get; }
        public int ThreadCurPtrOffset         { get; }
        public int ThreadReadPtrOffset        { get; }
        public int ThreadChunkListHeadOffset  { get; }
        public int ThreadChunkListTailOffset  { get; }
        public int ThreadCurReadChunkOffset   { get; }
        public int ThreadCurWriteChunkOffset  { get; }
        public int ThreadChunkListLengthOffset { get; }
        public int ThreadHeaderSize           { get; }

        // -----------------------------------------------------------------
        // StressLogChunk
        // -----------------------------------------------------------------

        public int ChunkPrevOffset       => 0;
        public int ChunkNextOffset       { get; }
        public int ChunkBufOffset        { get; }
        public int ChunkBufferSize       { get; }
        public int ChunkSig1Offset       => ChunkBufOffset + ChunkBufferSize;
        public int ChunkSig2Offset       => ChunkSig1Offset + 4;
        public int ChunkTotalSize        => ChunkSig2Offset + 4;

        // -----------------------------------------------------------------
        // StressMsg
        // -----------------------------------------------------------------

        /// <summary>Bytes consumed by the <c>StressMsg</c> header (without args).</summary>
        public int MessageHeaderSize { get; }

        /// <summary>
        /// Stride in bytes between consecutive arguments. Always equals
        /// <see cref="PointerSize"/>: arguments are <c>void*</c> on the
        /// target.
        /// </summary>
        public int ArgStride => PointerSize;

        /// <summary>Number of hex digits to emit for a <c>%p</c> on this target (8 or 16).</summary>
        public int PointerHexDigits => PointerSize * 2;

        /// <summary>Number of bytes occupied by a single module-table entry (baseAddress + size, both pointer-sized).</summary>
        public int ModuleEntrySize => PointerSize * 2;

        // -----------------------------------------------------------------
        // Static factories
        // -----------------------------------------------------------------

        /// <summary>
        /// Layout for a 64-bit Core target. Matches modern CoreCLR.
        /// </summary>
        public static StressLogLayout CoreX64 { get; } = new StressLogLayout(pointerSize: 8, isFramework: false);

        /// <summary>
        /// Layout for a 64-bit .NET Framework V1 target. Same chunk and
        /// module-table sizes as Core x64; ThreadStressLog and StressMsg
        /// layouts differ.
        /// </summary>
        public static StressLogLayout FrameworkX64 { get; } = new StressLogLayout(pointerSize: 8, isFramework: true);

        /// <summary>
        /// Layout for a 32-bit Core target on Windows. .NET Core has not
        /// shipped a 32-bit Linux runtime in a supported configuration,
        /// so only Windows-x86 layout is defined.
        /// </summary>
        public static StressLogLayout CoreWinX86 { get; } = new StressLogLayout(pointerSize: 4, isFramework: false);

        /// <summary>
        /// Layout for a 32-bit .NET Framework V1 target on Windows.
        /// </summary>
        public static StressLogLayout FrameworkWinX86 { get; } = new StressLogLayout(pointerSize: 4, isFramework: true);

        /// <summary>
        /// Pick a layout for the given pointer size. The returned layout
        /// initially assumes Core; callers must call <see cref="WithFramework"/>
        /// after variant detection if the target is Framework.
        /// </summary>
        public static StressLogLayout ForCore(int pointerSize) => pointerSize switch
        {
            8 => CoreX64,
            4 => CoreWinX86,
            _ => throw new ArgumentOutOfRangeException(nameof(pointerSize), pointerSize, "Pointer size must be 4 or 8."),
        };

        /// <summary>
        /// Returns the Framework V1 sibling of the supplied layout,
        /// preserving pointer size.
        /// </summary>
        public StressLogLayout WithFramework() => PointerSize switch
        {
            8 => FrameworkX64,
            4 => FrameworkWinX86,
            _ => throw new InvalidOperationException(),
        };

        private StressLogLayout(int pointerSize, bool isFramework)
        {
            PointerSize = pointerSize;
            IsFramework = isFramework;
            MaxAddress = pointerSize == 8 ? ulong.MaxValue : uint.MaxValue;

            // Field offsets in the StressLog struct.
            //
            // The first pointer-sized field is 'logs'. On x86 it follows
            // totalChunk (offset 16) directly with no padding. On x64 there
            // are 4 bytes of padding for 8-byte alignment.
            int p = pointerSize;
            int afterTotalChunk = 20;                      // x86: align(20, 4) = 20
            if (p == 8) afterTotalChunk = AlignUp(20, 8); // x64: 24
            InProcLogsOffset = afterTotalChunk;
            InProcPaddingOffset = InProcLogsOffset + p;            // uint padding (or fwk's TLS slot)
            InProcDeadCountOffset = InProcPaddingOffset + 4;       // int
            InProcLockOffset = InProcDeadCountOffset + 4;          // pointer (CRITSEC_COOKIE)
            // tickFrequency is uint64; on x86 MSVC __int64 is 8-byte aligned,
            // so there are 4 bytes of padding between lock and tickFrequency.
            int afterLock = InProcLockOffset + p;
            InProcTickFrequencyOffset = AlignUp(afterLock, 8);
            InProcStartTimeStampOffset = InProcTickFrequencyOffset + 8;
            InProcStartTimeOffset = InProcStartTimeStampOffset + 8;
            InProcModuleOffsetOffset = InProcStartTimeOffset + 8;  // size_t
            InProcModulesOffset = InProcModuleOffsetOffset + p;
            InProcHeaderSize = InProcModulesOffset
                             + StressLogConstants.MaxModules * ModuleEntrySize;

            // Memory-mapped header offsets. These are only valid on 64-bit
            // hosts (the runtime guards memory-mapped stress logs behind
            // HOST_64BIT). On 32-bit we still compute defensible numbers
            // but the magic check in TryOpen prevents this path from running.
            MmMemoryBaseOffset = 16;
            MmMemoryCurOffset = MmMemoryBaseOffset + p;            // 24 on x64
            MmMemoryLimitOffset = MmMemoryCurOffset + p;           // 32
            MmLogsOffset = MmMemoryLimitOffset + p;                // 40
            MmTickFrequencyOffset = MmLogsOffset + p;              // 48
            MmStartTimeStampOffset = MmTickFrequencyOffset + 8;    // 56
            MmThreadsWithNoLogOffset = MmStartTimeStampOffset + 8; // 64
            // Reserved1 (uint32) at +68, Reserved2[15] (uint64*15) at +72,
            // Modules at +192. We jump straight to Modules.
            MmModulesOffset = MmThreadsWithNoLogOffset + 4 + 4 + 15 * 8;  // 192 on x64
            MmHeaderReadSize = MmModulesOffset
                             + StressLogConstants.MaxModules * ModuleEntrySize;

            // ThreadStressLog field offsets.
            ChunkNextOffset = p;
            if (isFramework)
            {
                // Framework's ThreadStressLog lays out:
                //   next:P, threadId:4, isDead:4, curPtr:P, readPtr:P,
                //   readHasWrapped:4, writeHasWrapped:4, chunkListHead:P,
                //   chunkListTail:P, curReadChunk:P, curWriteChunk:P,
                //   chunkListLength:4.
                ThreadIdOffset = p;
                ThreadIdSize = 4;
                ThreadIsDeadOffset = ThreadIdOffset + 4;
                ThreadCurPtrOffset = ThreadIsDeadOffset + 4;
                ThreadReadPtrOffset = ThreadCurPtrOffset + p;
                ThreadReadHasWrappedOffset = ThreadReadPtrOffset + p;
                ThreadWriteHasWrappedOffset = ThreadReadHasWrappedOffset + 4;
                ThreadChunkListHeadOffset = ThreadWriteHasWrappedOffset + 4;
                ThreadChunkListTailOffset = ThreadChunkListHeadOffset + p;
                ThreadCurReadChunkOffset = ThreadChunkListTailOffset + p;
                ThreadCurWriteChunkOffset = ThreadCurReadChunkOffset + p;
                ThreadChunkListLengthOffset = ThreadCurWriteChunkOffset + p;
                // Round up to pointer alignment to match the C++ sizeof,
                // matching the existing 80-byte read on x64.
                ThreadHeaderSize = AlignUp(ThreadChunkListLengthOffset + 4, p);
            }
            else
            {
                // Core layout:
                //   next:P, threadId:8 (8-byte aligned), isDead:1,
                //   readHasWrapped:1, writeHasWrapped:1, pad to P,
                //   curPtr:P, readPtr:P, chunkListHead:P, chunkListTail:P,
                //   curReadChunk:P, curWriteChunk:P, chunkListLength:4.
                int afterNext = p;
                ThreadIdOffset = AlignUp(afterNext, 8);          // 8 on both bitness
                ThreadIdSize = 8;
                ThreadIsDeadOffset = ThreadIdOffset + 8;          // 16
                ThreadReadHasWrappedOffset = ThreadIsDeadOffset + 1;
                ThreadWriteHasWrappedOffset = ThreadReadHasWrappedOffset + 1;
                int afterFlags = ThreadWriteHasWrappedOffset + 1;
                ThreadCurPtrOffset = AlignUp(afterFlags, p);
                ThreadReadPtrOffset = ThreadCurPtrOffset + p;
                ThreadChunkListHeadOffset = ThreadReadPtrOffset + p;
                ThreadChunkListTailOffset = ThreadChunkListHeadOffset + p;
                ThreadCurReadChunkOffset = ThreadChunkListTailOffset + p;
                ThreadCurWriteChunkOffset = ThreadCurReadChunkOffset + p;
                ThreadChunkListLengthOffset = ThreadCurWriteChunkOffset + p;
                ThreadHeaderSize = AlignUp(ThreadChunkListLengthOffset + 4, p);
            }

            // Chunk: prev (P), next (P), buf[ChunkBufferSize], sig1, sig2
            ChunkBufOffset = 2 * p;
            ChunkBufferSize = p == 8
                ? StressLogConstants.ChunkBufferSize64
                : StressLogConstants.ChunkBufferSize32;

            // StressMsg header.
            //   Core (V3/V4):       fmtOffsCArgs:DWORD_PTR + facility:DWORD_PTR(?) + timestamp:uint64
            //                       Both x64 and x86 collapse to 16 bytes.
            //                       Wait — that's NOT what the existing code says for x64 Fwk.
            //   Framework V1 x64:   fmtOffsCArgs:size_t + facility:size_t + timeStamp:uint64
            //                       = 8 + 8 + 8 = 24 bytes (qword-aligned).
            //   Framework V1 x86:   fmtOffsCArgs:size_t + facility:size_t + timeStamp:uint64
            //                       = 4 + 4 + 8 = 16 bytes (no padding).
            //   Core V3/V4 (both):  packed bitfield + uint32 facility + uint64 timeStamp = 16 bytes.
            if (isFramework && p == 8)
                MessageHeaderSize = 24;
            else
                MessageHeaderSize = 16;
        }

        private static int AlignUp(int value, int alignment)
            => (value + alignment - 1) & ~(alignment - 1);

        // -----------------------------------------------------------------
        // Pointer reads
        // -----------------------------------------------------------------

        /// <summary>
        /// Read a pointer-sized field from <paramref name="span"/> at
        /// <paramref name="offset"/>, zero-extending to <see cref="ulong"/>
        /// on 32-bit targets.
        /// </summary>
        /// <remarks>
        /// 32-bit pointer fields stored in target memory are exactly four
        /// raw bytes. Treating them as <c>uint</c> (zero-extended to
        /// <c>ulong</c>) is correct: any sign-extension would conflate a
        /// high-bit pointer such as <c>0x90000000</c> with the bogus
        /// <c>0xFFFFFFFF90000000</c>. (DAC-returned addresses are a
        /// separate concern: <c>CLRDATA_ADDRESS</c> is sign-extended by
        /// the DAC ABI; we un-extend at the SOSDac boundary.)
        /// </remarks>
        public ulong ReadTargetPointer(ReadOnlySpan<byte> span, int offset)
        {
            if (PointerSize == 8)
                return BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset));
            return BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset));
        }

        /// <summary>
        /// Read the threadId field. Width depends on the runtime variant:
        /// 8 bytes on Core (always uint64), 4 bytes on Framework.
        /// </summary>
        public ulong ReadThreadId(ReadOnlySpan<byte> threadHeader)
        {
            if (ThreadIdSize == 8)
                return BinaryPrimitives.ReadUInt64LittleEndian(threadHeader.Slice(ThreadIdOffset));
            return BinaryPrimitives.ReadUInt32LittleEndian(threadHeader.Slice(ThreadIdOffset));
        }

        /// <summary>
        /// True if a chunk's address can be added to its total size without
        /// exceeding the target's <see cref="MaxAddress"/>. Use this before
        /// computing any per-chunk address arithmetic.
        /// </summary>
        public bool ChunkFits(ulong address)
        {
            ulong totalSize = (ulong)ChunkTotalSize;
            return address <= MaxAddress - totalSize;
        }

        // -----------------------------------------------------------------
        // Bit-field decoders. These are independent of pointer size.
        // -----------------------------------------------------------------

        /// <summary>
        /// Decode a V4 <c>StressMsg</c> header from a 16-byte little-endian span.
        /// </summary>
        public static void DecodeMessageV4(ReadOnlySpan<byte> header16,
                                           out uint facility,
                                           out int numberOfArgs,
                                           out ulong formatOffset,
                                           out ulong timeStamp)
        {
            ulong w0 = BinaryPrimitives.ReadUInt64LittleEndian(header16);
            ulong w1 = BinaryPrimitives.ReadUInt64LittleEndian(header16.Slice(8));

            // word 0: facility (32) | numberOfArgs (6) | formatOffsetLow (26)
            facility = (uint)(w0 & 0xFFFFFFFFu);
            numberOfArgs = (int)((w0 >> 32) & 0x3F);
            ulong formatOffsetLow = (w0 >> 38) & ((1UL << StressLogConstants.FormatOffsetLowBits) - 1);

            // word 1: formatOffsetHigh (13) | timeStamp (51)
            ulong formatOffsetHigh = w1 & ((1UL << StressLogConstants.FormatOffsetHighBits) - 1);
            timeStamp = w1 >> StressLogConstants.FormatOffsetHighBits;

            formatOffset = (formatOffsetHigh << StressLogConstants.FormatOffsetLowBits) | formatOffsetLow;
        }

        /// <summary>
        /// Decode a V3 <c>StressMsg</c> header from a 16-byte little-endian span.
        /// V3 uses a separate 32-bit facility field and a single 26-bit
        /// <c>formatOffset</c> with a 3+3 split <c>numberOfArgs</c>.
        /// </summary>
        public static void DecodeMessageV3(ReadOnlySpan<byte> header16,
                                           out uint facility,
                                           out int numberOfArgs,
                                           out ulong formatOffset,
                                           out ulong timeStamp)
        {
            uint w0 = BinaryPrimitives.ReadUInt32LittleEndian(header16);
            uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(header16.Slice(4));
            ulong w2 = BinaryPrimitives.ReadUInt64LittleEndian(header16.Slice(8));

            // word 0: numberOfArgsLow (3) | formatOffset (26) | numberOfArgsHigh (3)
            uint argsLow = w0 & 0x7u;
            uint formatOffset26 = (w0 >> 3) & ((1u << 26) - 1);
            uint argsHigh = (w0 >> 29) & 0x7u;

            numberOfArgs = (int)(argsLow | (argsHigh << 3));
            formatOffset = formatOffset26;
            facility = w1;
            timeStamp = w2;
        }

        /// <summary>
        /// Decode a Framework V1 <c>StressMsg</c> header. On x64 the header
        /// is 24 bytes (each scalar qword-aligned); on x86 it is 16 bytes.
        /// In either case the bit-field is (numArgs:3 | formatOffset:29).
        /// </summary>
        public static void DecodeMessageFxV1(ReadOnlySpan<byte> headerBytes,
                                             int pointerSize,
                                             out uint facility,
                                             out int numberOfArgs,
                                             out ulong formatOffset,
                                             out ulong timeStamp)
        {
            if (pointerSize == 8)
            {
                uint w0Low = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes);
                // headerBytes[4..8] is padding/uninitialized — never read.
                uint w1Low = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(8));
                // headerBytes[12..16] is padding.
                ulong ts = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(16));

                numberOfArgs = (int)(w0Low & 0x7u);
                formatOffset = (w0Low >> 3) & ((1u << 29) - 1);
                facility = w1Low;
                timeStamp = ts;
            }
            else
            {
                uint w0 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes);
                uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(4));
                ulong ts = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(8));

                numberOfArgs = (int)(w0 & 0x7u);
                formatOffset = (w0 >> 3) & ((1u << 29) - 1);
                facility = w1;
                timeStamp = ts;
            }
        }
    }
}
