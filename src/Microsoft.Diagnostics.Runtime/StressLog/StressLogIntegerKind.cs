// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// The numeric format requested by an integer-typed stress log argument.
    /// </summary>
    public enum StressLogIntegerKind
    {
        /// <summary>Signed decimal (printf <c>%d</c>, <c>%i</c>).</summary>
        Decimal,

        /// <summary>Unsigned decimal (printf <c>%u</c>).</summary>
        Unsigned,

        /// <summary>Lowercase hexadecimal (printf <c>%x</c>).</summary>
        Hex,

        /// <summary>Uppercase hexadecimal (printf <c>%X</c>).</summary>
        HexUpper,

        /// <summary>Single character (printf <c>%c</c>).</summary>
        Char,
    }
}
