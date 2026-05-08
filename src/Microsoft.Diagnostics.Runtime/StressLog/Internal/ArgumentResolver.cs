// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Resolves <c>%s</c>/<c>%S</c> argument addresses to sanitized byte
    /// spans. Ownership of the returned span is the resolver's, and it is
    /// only valid until the next call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The number of bytes read from the target for any single string
    /// argument is bounded by <see cref="MaxStringBytes"/>. NUL termination
    /// is honored; bytes past the first NUL are dropped. Read failures
    /// (truncated <see cref="IMemoryReader.Read"/>, address 0) yield an
    /// empty span.
    /// </para>
    /// <para>
    /// All bytes returned have been passed through
    /// <see cref="StressLogSanitizer"/>, so they are guaranteed to be 7-bit
    /// printable ASCII. Receivers can write them straight to a console,
    /// file, or other byte sink without further escaping.
    /// </para>
    /// </remarks>
    internal sealed class ArgumentResolver
    {
        public const int MaxStringBytes = 256;

        private readonly IMemoryReader _reader;
        private readonly byte[] _readScratch = new byte[MaxStringBytes * 2]; // *2 for UTF-16
        private readonly byte[] _sanitizeScratch = new byte[MaxStringBytes];

        public ArgumentResolver(IMemoryReader reader)
        {
            _reader = reader;
        }

        public ReadOnlySpan<byte> ResolveString(ulong address, StressLogStringEncoding encoding)
        {
            if (address == 0)
                return default;

            if (encoding == StressLogStringEncoding.Utf8)
            {
                Span<byte> read = _readScratch.AsSpan(0, MaxStringBytes);
                int got = _reader.Read(address, read);
                if (got <= 0)
                    return default;

                ReadOnlySpan<byte> raw = read.Slice(0, got);
                int len = StressLogSanitizer.LengthToFirstNull(raw);
                int written = StressLogSanitizer.SanitizeAscii(raw.Slice(0, len), _sanitizeScratch);
                return _sanitizeScratch.AsSpan(0, written);
            }
            else
            {
                Span<byte> read = _readScratch.AsSpan(0, MaxStringBytes * 2);
                int got = _reader.Read(address, read);
                if (got <= 0)
                    return default;

                ReadOnlySpan<byte> raw = read.Slice(0, got & ~1); // drop a stray trailing byte
                int written = StressLogSanitizer.SanitizeUtf16(raw, _sanitizeScratch);
                return _sanitizeScratch.AsSpan(0, written);
            }
        }
    }
}
