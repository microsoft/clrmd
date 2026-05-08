// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Byte-level sanitization for any text byte read from the target.
    /// Replaces anything that is not 7-bit printable ASCII (<c>0x20</c>
    /// through <c>0x7E</c>) with <c>'.'</c>. Used both for format strings
    /// and for <c>%s</c>/<c>%S</c> argument bytes before they are handed to
    /// a receiver.
    /// </summary>
    /// <remarks>
    /// The replacement character is deliberately chosen to be benign and to
    /// not be misinterpreted as part of a printf-family format specifier
    /// (e.g., <c>%</c>) or as an ANSI escape introducer (e.g., <c>\u001B</c>)
    /// in any downstream consumer. Tab and newline bytes are also replaced
    /// so that target-controlled bytes cannot inject visual line breaks
    /// into the rendered output of a single message.
    /// </remarks>
    internal static class StressLogSanitizer
    {
        public const byte Replacement = (byte)'.';

        /// <summary>
        /// Returns the length of <paramref name="src"/> up to the first NUL byte
        /// (or the full length if there is none). Used because the runtime
        /// stores format strings null-terminated in their original module.
        /// </summary>
        public static int LengthToFirstNull(ReadOnlySpan<byte> src)
        {
            int idx = src.IndexOf((byte)0);
            return idx < 0 ? src.Length : idx;
        }

        /// <summary>
        /// Sanitize <paramref name="src"/> into <paramref name="dst"/>:
        /// non-printable bytes become <c>'.'</c>. Returns the number of bytes
        /// written. <paramref name="dst"/> must be at least as long as
        /// <paramref name="src"/>.
        /// </summary>
        public static int SanitizeAscii(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            if (dst.Length < src.Length)
                throw new ArgumentException("destination buffer is too small", nameof(dst));

            for (int i = 0; i < src.Length; i++)
                dst[i] = SanitizeByte(src[i]);

            return src.Length;
        }

        /// <summary>
        /// Sanitize a UTF-16LE byte sequence into ASCII <paramref name="dst"/>.
        /// Returns the number of bytes written.
        /// </summary>
        public static int SanitizeUtf16(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            int srcCount = src.Length / 2;
            if (dst.Length < srcCount)
                throw new ArgumentException("destination buffer is too small", nameof(dst));

            for (int i = 0; i < srcCount; i++)
            {
                ushort u = (ushort)(src[i * 2] | (src[i * 2 + 1] << 8));
                if (u == 0)
                    return i;

                dst[i] = u <= 0x7F ? SanitizeByte((byte)u) : Replacement;
            }

            return srcCount;
        }

        private static byte SanitizeByte(byte b)
        {
            // Allow only printable 7-bit ASCII (0x20..0x7E). Everything else,
            // including 0x00 / control / DEL / non-ASCII, is collapsed to the
            // replacement byte. Tabs and newlines are deliberately replaced
            // so that target-derived bytes cannot inject what looks like a
            // line break in the rendered output.
            if (b is >= 0x20 and <= 0x7E)
                return b;

            return Replacement;
        }
    }
}
