// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Wire-protocol constants for the runtime stress log. These match the
    /// values in <c>src/coreclr/inc/stresslog.h</c> in dotnet/runtime and
    /// the in-tree mirror in dotnet/diagnostics. They are part of the
    /// runtime's documented binary interface; values here must not be
    /// changed without coordinating with the runtime.
    /// </summary>
    internal static class StressLogConstants
    {
        /// <summary>The "STRL" magic at the head of a memory-mapped stress log.</summary>
        public const uint MemoryMappedMagic = 0x4C525453; // 'STRL' little-endian

        /// <summary>Legacy memory-mapped layout (small format offset).</summary>
        public const uint MemoryMappedVersionV1 = 0x00010001;

        /// <summary>Memory-mapped layout introduced in .NET 8 (large format offset).</summary>
        public const uint MemoryMappedVersionV2 = 0x00010002;

        /// <summary>The signature word that brackets every <c>StressLogChunk</c>.</summary>
        public const uint ChunkSignature = 0xCFCFCFCFu;

        /// <summary>Size of a stress log chunk on a 64-bit target.</summary>
        public const int ChunkBufferSize64 = 32 * 1024;

        /// <summary>Size of a stress log chunk on a 32-bit target.</summary>
        public const int ChunkBufferSize32 = 16 * 1024;

        /// <summary>Maximum number of modules tracked by the runtime's module table.</summary>
        public const int MaxModules = 5;

        /// <summary>Maximum number of arguments any message may carry. The 6-bit field caps it at 63.</summary>
        public const int MaxArgumentCount = 63;

        /// <summary>Number of bits used to encode the low half of <c>formatOffset</c> in V4.</summary>
        public const int FormatOffsetLowBits = 26;

        /// <summary>Number of bits used to encode the high half of <c>formatOffset</c> in V4.</summary>
        public const int FormatOffsetHighBits = 13;

        /// <summary>Total number of bits used to encode <c>formatOffset</c> in V4.</summary>
        public const int FormatOffsetTotalBits = FormatOffsetLowBits + FormatOffsetHighBits;

        /// <summary>Maximum value of a V4 <c>formatOffset</c>: <c>1 &lt;&lt; 39</c>.</summary>
        public const ulong FormatOffsetMax = 1UL << FormatOffsetTotalBits;

        /// <summary>Maximum length of a format string read from the target.</summary>
        public const int MaxFormatStringLength = 256;

        /// <summary>
        /// Upper bound on bytes ever read for a <c>ThreadStressLog</c>
        /// header. Sized for the largest variant we support so callers can
        /// stack-allocate a buffer once and slice. Must be kept in sync
        /// with <see cref="StressLogLayout.ThreadHeaderSize"/>.
        /// </summary>
        public const int MaxThreadHeaderBytes = 80;
    }
}
