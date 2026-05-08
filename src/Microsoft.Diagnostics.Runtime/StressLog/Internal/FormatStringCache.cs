// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Caches the bytes of a format string keyed on the resolved target
    /// address from which they were read. Format strings appear repeatedly
    /// across messages; reading and sanitizing them once amortizes the cost
    /// and keeps the per-message critical path allocation-free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache is bounded in entry count and total bytes so that a
    /// pathological log cannot turn this into an unbounded allocation
    /// primitive. When the cap is hit the oldest entries are dropped (FIFO).
    /// </para>
    /// <para>
    /// Each cached entry stores both the sanitized format bytes (with a
    /// trailing NUL stripped) and the recognized
    /// <see cref="StressLogKnownFormat"/>, so identification is O(1) after
    /// the first read.
    /// </para>
    /// </remarks>
    internal sealed class FormatStringCache
    {
        private readonly Dictionary<ulong, Entry> _entries = new();
        private readonly Queue<ulong> _insertionOrder = new();
        private readonly IMemoryReader _reader;
        private readonly AllocationBudget _budget;
        private readonly int _maxLength;
        private readonly int _maxEntries;

        private byte[] _readScratch;

        public FormatStringCache(IMemoryReader reader, AllocationBudget budget, int maxFormatLength, int maxEntries)
        {
            _reader = reader;
            _budget = budget;
            _maxLength = maxFormatLength;
            _maxEntries = maxEntries;
            _readScratch = new byte[maxFormatLength];
        }

        public bool TryGet(ulong address, out ReadOnlyMemory<byte> sanitized, out StressLogKnownFormat known)
        {
            if (address == 0)
            {
                sanitized = default;
                known = StressLogKnownFormat.None;
                return false;
            }

            if (_entries.TryGetValue(address, out Entry e))
            {
                sanitized = e.Sanitized;
                known = e.Known;
                return e.Sanitized.Length > 0;
            }

            return TryReadAndCache(address, out sanitized, out known);
        }

        private bool TryReadAndCache(ulong address, out ReadOnlyMemory<byte> sanitized, out StressLogKnownFormat known)
        {
            int read = _reader.Read(address, _readScratch);
            if (read <= 0)
            {
                Insert(address, Array.Empty<byte>(), StressLogKnownFormat.None);
                sanitized = default;
                known = StressLogKnownFormat.None;
                return false;
            }

            ReadOnlySpan<byte> raw = _readScratch.AsSpan(0, read);
            int len = StressLogSanitizer.LengthToFirstNull(raw);

            // Identify against the original (unsanitized) bytes, since the
            // known format constants are themselves printf format strings
            // containing '%' specifiers that the sanitizer would not change.
            StressLogKnownFormat k = StressLogKnownFormats.Identify(raw.Slice(0, len));

            byte[] copy;
            if (!_budget.TryReserve(len))
            {
                Insert(address, Array.Empty<byte>(), k);
                sanitized = default;
                known = k;
                return false;
            }

            copy = new byte[len];
            StressLogSanitizer.SanitizeAscii(raw.Slice(0, len), copy);
            Insert(address, copy, k);

            sanitized = copy;
            known = k;
            return true;
        }

        private void Insert(ulong address, byte[] sanitized, StressLogKnownFormat known)
        {
            while (_entries.Count >= _maxEntries && _insertionOrder.Count > 0)
            {
                ulong oldest = _insertionOrder.Dequeue();
                if (_entries.TryGetValue(oldest, out Entry victim))
                {
                    _budget.Release(victim.Sanitized.Length);
                    _entries.Remove(oldest);
                }
            }

            _entries[address] = new Entry(sanitized, known);
            _insertionOrder.Enqueue(address);
        }

        private readonly struct Entry
        {
            public Entry(byte[] sanitized, StressLogKnownFormat known)
            {
                Sanitized = sanitized;
                Known = known;
            }

            public byte[] Sanitized { get; }
            public StressLogKnownFormat Known { get; }
        }
    }
}
