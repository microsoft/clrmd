// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Internal limits. The public API intentionally hides these for now;
    /// the values are conservative defaults that bound CPU and memory while
    /// parsing arbitrarily large or malformed stress logs.
    /// </summary>
    internal sealed class StressLogOptions
    {
        public static StressLogOptions Default { get; } = new();

        /// <summary>Maximum number of threads enumerated. Cycles in the thread list are also bounded by this.</summary>
        public int MaxThreads { get; init; } = 100_000;

        /// <summary>Maximum number of chunks per thread. Cycles in the chunk list are bounded by this.</summary>
        public int MaxChunksPerThread { get; init; } = 65_536;

        /// <summary>Maximum number of messages emitted per thread.</summary>
        public int MaxMessagesPerThread { get; init; } = 10_000_000;

        /// <summary>Maximum length of a format string read from the target.</summary>
        public int MaxFormatStringLength { get; init; } = StressLogConstants.MaxFormatStringLength;

        /// <summary>Maximum length of a string argument (<c>%s</c>/<c>%S</c>) read from the target.</summary>
        public int MaxStringArgumentLength { get; init; } = 256;

        /// <summary>Total bytes the parser may rent from the array pool while open.</summary>
        public long MaxTotalBytesAllocated { get; init; } = 256L * 1024 * 1024;

        /// <summary>
        /// Maximum number of distinct format strings cached. Bounded so a
        /// pathological log cannot force an entry per message.
        /// </summary>
        public int MaxFormatStringCacheEntries { get; init; } = 100_000;
    }
}
