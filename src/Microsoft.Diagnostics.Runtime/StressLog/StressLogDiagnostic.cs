// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// A non-fatal diagnostic raised by the stress log reader while enumerating
    /// a malformed or partially-readable log. Consumers may choose to log these
    /// or display them to the user; enumeration continues past the offending
    /// record where possible.
    /// </summary>
    public readonly struct StressLogDiagnostic
    {
        internal StressLogDiagnostic(StressLogDiagnosticKind kind, ulong address)
        {
            Kind = kind;
            Address = address;
        }

        /// <summary>Category of the issue.</summary>
        public StressLogDiagnosticKind Kind { get; }

        /// <summary>
        /// Address in the target at which the issue was detected, or 0 if not
        /// associated with a specific address.
        /// </summary>
        public ulong Address { get; }

        /// <inheritdoc/>
        public override string ToString() => $"{Kind} @ 0x{Address:x}";
    }
}
