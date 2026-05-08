// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Receives a sequence of typed tokens describing a single stress log
    /// message, produced by <see cref="StressLogMessage.Format{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reader parses, validates, and sanitizes the log-supplied format
    /// bytes and emits a sequence of typed tokens to the receiver. Receivers
    /// never see raw format bytes; the indirection means consumers cannot
    /// accidentally hand log-controlled bytes to a printf-family or
    /// <see cref="string.Format(string, object)"/> API.
    /// </para>
    /// <para>
    /// Implementations should be value types declared with <see langword="struct"/>
    /// to avoid boxing when invoked through
    /// <c>Format&lt;T&gt;(ref T) where T : struct, IStressLogFormatReceiver</c>.
    /// </para>
    /// </remarks>
    public interface IStressLogFormatReceiver
    {
        /// <summary>
        /// Receives a literal run of bytes from the format string.
        /// The bytes are guaranteed to be 7-bit printable ASCII; control
        /// characters and ANSI escape sequences are replaced with
        /// <c>'.'</c> before being passed.
        /// </summary>
        void Literal(ReadOnlySpan<byte> ascii);

        /// <summary>
        /// Receives an integer-typed argument.
        /// </summary>
        /// <param name="kind">Numeric format requested by the format specifier.</param>
        /// <param name="width">Minimum width, or <c>-1</c> if not specified. Clamped to <c>[0, 256]</c>.</param>
        /// <param name="precision">Precision, or <c>-1</c> if not specified. Clamped to <c>[0, 256]</c>.</param>
        /// <param name="value">The integer value. Sign-extended for signed kinds, zero-extended otherwise.</param>
        void Integer(StressLogIntegerKind kind, int width, int precision, long value);

        /// <summary>
        /// Receives a pointer-typed argument.
        /// </summary>
        /// <param name="kind">Whether the pointer is plain or runtime-typed (MethodDesc/MethodTable/...).</param>
        /// <param name="address">The pointer value as it appeared in the message arguments.</param>
        void Pointer(StressLogPointerKind kind, ulong address);

        /// <summary>
        /// Receives a string-typed argument whose bytes have already been read
        /// from the target, decoded, and sanitized into 7-bit printable ASCII.
        /// </summary>
        /// <param name="encoding">Encoding of the string in the target before sanitization.</param>
        /// <param name="sanitized">The sanitized string bytes. Length is bounded by the reader's options.</param>
        void String(StressLogStringEncoding encoding, ReadOnlySpan<byte> sanitized);

        /// <summary>
        /// Indicates that the format string requested an argument the message
        /// did not carry. Receivers typically render this as <c>&lt;missing&gt;</c>
        /// or similar.
        /// </summary>
        void MissingArgument();
    }
}
