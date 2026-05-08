// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Identifies which on-target struct layout the parser should use when
    /// decoding a <c>ThreadStressLog</c>. The two variants differ in field
    /// sizes and ordering between the .NET Framework runtime and modern
    /// .NET Core / .NET 5+ runtimes.
    /// </summary>
    internal enum StressLogVariant
    {
        /// <summary>Modern CoreCLR / .NET 5+. 8-byte threadId, 1-byte isDead/wrap flags.</summary>
        Core,

        /// <summary>.NET Framework 4.x. 4-byte threadId, 4-byte wrap flags, curPtr at offset 16.</summary>
        FrameworkV1,
    }
}
