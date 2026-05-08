// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Categorizes a non-fatal corruption event raised by the stress log reader
    /// while enumerating a malformed or partially-readable log.
    /// </summary>
    public enum StressLogDiagnosticKind
    {
        /// <summary>A thread record looked malformed (signature, list cycle, sizes).</summary>
        CorruptThread,

        /// <summary>A chunk record looked malformed (signature, list cycle).</summary>
        CorruptChunk,

        /// <summary>A message record looked malformed (arg count, format offset, layout).</summary>
        CorruptMessage,

        /// <summary>An <see cref="IMemoryReader"/> read returned fewer bytes than requested.</summary>
        ReadMemoryFailed,

        /// <summary>A configured limit (thread count, chunk count, allocation budget) was hit.</summary>
        LimitExceeded,
    }
}
