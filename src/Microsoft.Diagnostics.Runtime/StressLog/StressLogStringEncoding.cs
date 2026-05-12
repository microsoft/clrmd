// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Encoding of a string-typed stress log argument as it was stored in the target.
    /// </summary>
    public enum StressLogStringEncoding
    {
        /// <summary>
        /// Narrow string in the target. Corresponds to printf <c>%s</c> / <c>%hs</c>.
        /// The bytes have been interpreted as UTF-8 and any unrepresentable
        /// or control characters replaced.
        /// </summary>
        Utf8,

        /// <summary>
        /// Wide string in the target. Corresponds to printf <c>%S</c> / <c>%ls</c>.
        /// The bytes have been interpreted as UTF-16LE and any unrepresentable
        /// or control characters replaced; the receiver still gets ASCII bytes.
        /// </summary>
        Utf16,
    }
}
