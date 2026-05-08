// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Field offsets within the on-target structures we read. Only 64-bit
    /// targets are supported; 32-bit targets are rejected during open.
    /// </summary>
    /// <remarks>
    /// The runtime's <c>StressLog</c> class layout is documented at
    /// <c>src/coreclr/inc/stresslog.h</c> and mirrored in this repo's
    /// adjacent dotnet/diagnostics tree. These offsets must move in
    /// lockstep with that header.
    /// </remarks>
    internal static class StressLogLayout
    {
        // -----------------------------------------------------------------
        // In-process StressLog (64-bit target)
        // -----------------------------------------------------------------

        public const int InProc_FacilitiesToLog   = 0;     // uint
        public const int InProc_LevelToLog        = 4;     // uint
        public const int InProc_MaxSizePerThread  = 8;     // uint
        public const int InProc_MaxSizeTotal      = 12;    // uint
        public const int InProc_TotalChunk        = 16;    // int (Volatile<LONG>)
        // 4 bytes padding for 8-byte alignment of the next pointer
        public const int InProc_Logs              = 24;    // ThreadStressLog*
        public const int InProc_Padding           = 32;    // uint (named 'padding' in the header)
        public const int InProc_DeadCount         = 36;    // int (Volatile<LONG>)
        public const int InProc_Lock              = 40;    // CRITSEC_COOKIE (pointer)
        public const int InProc_TickFrequency     = 48;    // uint64
        public const int InProc_StartTimeStamp    = 56;    // uint64
        public const int InProc_StartTime         = 64;    // FILETIME (uint64)
        public const int InProc_ModuleOffset      = 72;    // size_t
        public const int InProc_Modules           = 80;    // ModuleDesc[5]
        public const int InProc_ModuleEntrySize   = 16;    // baseAddress + size on 64-bit
        public const int InProc_ModulesEnd        = InProc_Modules + StressLogConstants.MaxModules * InProc_ModuleEntrySize;

        /// <summary>Bytes we read from the target for the in-process header.</summary>
        public const int InProc_HeaderSize        = InProc_ModulesEnd; // 160

        // -----------------------------------------------------------------
        // Memory-mapped StressLogHeader (64-bit only)
        // -----------------------------------------------------------------

        public const int Mm_HeaderSize            = 0;     // size_t
        public const int Mm_Magic                 = 8;     // uint32 'STRL'
        public const int Mm_Version               = 12;    // uint32
        public const int Mm_MemoryBase            = 16;    // uint8*
        public const int Mm_MemoryCur             = 24;    // uint8*
        public const int Mm_MemoryLimit           = 32;    // uint8*
        public const int Mm_Logs                  = 40;    // ThreadStressLog*
        public const int Mm_TickFrequency         = 48;    // uint64
        public const int Mm_StartTimeStamp        = 56;    // uint64
        public const int Mm_ThreadsWithNoLog      = 64;    // uint32
        public const int Mm_Reserved1             = 68;    // uint32
        public const int Mm_Reserved2             = 72;    // uint64[15] (15 * 8 = 120 bytes)
        public const int Mm_Modules               = 192;   // ModuleDesc[5]
        public const int Mm_ModulesEnd            = Mm_Modules + StressLogConstants.MaxModules * InProc_ModuleEntrySize;
        public const int Mm_ModuleImage           = Mm_ModulesEnd;  // 272

        /// <summary>Bytes we read from the target for the memory-mapped header.</summary>
        public const int Mm_HeaderReadSize        = Mm_ModulesEnd; // 272

        // -----------------------------------------------------------------
        // ThreadStressLog (Core, 64-bit target)
        // -----------------------------------------------------------------

        public const int Thread_Next              = 0;     // ThreadStressLog*
        public const int CoreThread_ThreadId      = 8;     // uint64
        public const int CoreThread_IsDead        = 16;    // uint8
        public const int CoreThread_ReadHasWrapped  = 17;  // uint8 (uninitialized in the live writer; we never trust this byte)
        public const int CoreThread_WriteHasWrapped = 18;  // uint8
        // 5 bytes padding for 8-byte alignment of the next pointer
        public const int CoreThread_CurPtr        = 24;    // StressMsg*
        public const int CoreThread_ReadPtr       = 32;    // StressMsg*
        public const int CoreThread_ChunkListHead = 40;    // StressLogChunk*
        public const int CoreThread_ChunkListTail = 48;    // StressLogChunk*
        public const int CoreThread_CurReadChunk  = 56;    // StressLogChunk*
        public const int CoreThread_CurWriteChunk = 64;    // StressLogChunk*
        public const int CoreThread_ChunkListLength = 72;  // long (4 bytes)

        // -----------------------------------------------------------------
        // ThreadStressLog (.NET Framework V1, 64-bit target)
        // -----------------------------------------------------------------
        //
        // Framework's ThreadStressLog uses a 4-byte threadId and 4-byte
        // isDead/readHasWrapped/writeHasWrapped fields, shifting curPtr,
        // readPtr earlier in the layout. Verified against
        // .NET Framework 4.8.9325 dumps via the cdb dt extension.

        public const int FxThread_ThreadId        = 8;     // uint32
        public const int FxThread_IsDead          = 12;    // uint32
        public const int FxThread_CurPtr          = 16;    // StressMsg*
        public const int FxThread_ReadPtr         = 24;    // StressMsg*
        public const int FxThread_ReadHasWrapped  = 32;    // uint32 (uninitialized in the live writer)
        public const int FxThread_WriteHasWrapped = 36;    // uint32
        public const int FxThread_ChunkListHead   = 40;    // StressLogChunk*
        public const int FxThread_ChunkListTail   = 48;    // StressLogChunk*
        public const int FxThread_CurReadChunk    = 56;    // StressLogChunk*
        public const int FxThread_CurWriteChunk   = 64;    // StressLogChunk*
        public const int FxThread_ChunkListLength = 72;    // long (4 bytes)

        // Both layouts use the same 80-byte struct size (the differences are
        // a rearrangement of fields within those bytes), so we always read
        // 80 bytes for the thread header regardless of variant.
        public const int Thread_HeaderSize        = 80;

        // -----------------------------------------------------------------
        // StressLogChunk (64-bit target, 32 KB body)
        // -----------------------------------------------------------------

        public const int Chunk_Prev               = 0;     // StressLogChunk*
        public const int Chunk_Next               = 8;     // StressLogChunk*
        public const int Chunk_Buf                = 16;    // char[ChunkBufferSize64]
        public const int Chunk_DwSig1Offset       = Chunk_Buf + StressLogConstants.ChunkBufferSize64;     // 32784
        public const int Chunk_DwSig2Offset       = Chunk_DwSig1Offset + 4;                              // 32788
        public const int Chunk_TotalSize          = Chunk_DwSig2Offset + 4;                              // 32792

        // -----------------------------------------------------------------
        // StressMsg
        // -----------------------------------------------------------------

        public const int Msg_HeaderSize           = 16;    // sizeof(StressMsg) without args (Core V3/V4)
        public const int Msg_HeaderSizeFxV1       = 24;    // .NET Framework: each header field is qword-aligned
        public const int Msg_ArgSize              = 8;     // pointer-sized

        // -----------------------------------------------------------------
        // Bit-field decoders
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
        /// Decode a Framework V1 <c>StressMsg</c> header from a 24-byte
        /// little-endian span. Layout differs from Core's V3/V4: each field
        /// is qword-aligned, so <c>fmtOffsCArgs</c>, <c>facility</c>, and
        /// <c>timeStamp</c> each occupy 8 bytes (with the upper 4 bytes of
        /// the first two being unused padding). The bit-field is also
        /// (numArgs:3 | formatOffset:29) without the V3 high-bits split.
        /// </summary>
        public static void DecodeMessageFxV1(ReadOnlySpan<byte> header24,
                                             out uint facility,
                                             out int numberOfArgs,
                                             out ulong formatOffset,
                                             out ulong timeStamp)
        {
            uint w0Low = BinaryPrimitives.ReadUInt32LittleEndian(header24);
            // header24[4..8] is padding/uninitialized — never read.
            uint w1Low = BinaryPrimitives.ReadUInt32LittleEndian(header24.Slice(8));
            // header24[12..16] is padding.
            ulong ts = BinaryPrimitives.ReadUInt64LittleEndian(header24.Slice(16));

            numberOfArgs = (int)(w0Low & 0x7u);
            formatOffset = (w0Low >> 3) & ((1u << 29) - 1);
            facility = w1Low;
            timeStamp = ts;
        }
    }
}
