// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities.Platform;

namespace Microsoft.Diagnostics.Runtime.MacOS.Trie
{
    internal readonly record struct ExportsTrieTerminal(ulong Value, ulong Flags)
    {
        private const ulong KindMask = 0x03;
        private const ulong KindAbsolute = 0x02;
        private const ulong Reexport = 0x08;

        public static ExportsTrieTerminal? Read(Stream trie, long end)
        {
            if (!trie.TryReadUleb128(out ulong terminalFlags, end) || !trie.TryReadUleb128(out ulong terminalValue, end))
            {
                return null;
            }

            return new ExportsTrieTerminal(terminalValue, terminalFlags);
        }

        public ulong? GetAddress(ulong imageBase)
        {
            // For a re-export, Value is a library ordinal rather than an address or image offset.
            if ((Flags & Reexport) != 0)
            {
                return null;
            }

            // Absolute exports contain the final address and must not be adjusted by the image base.
            if ((Flags & KindMask) == KindAbsolute)
            {
                return Value != 0 ? Value : null;
            }

            // Regular and thread-local exports contain an offset relative to the Mach-O image base.
            if (Value > ulong.MaxValue - imageBase)
            {
                return null;
            }

            ulong address = imageBase + Value;
            return address != 0 ? address : null;
        }
    }
}