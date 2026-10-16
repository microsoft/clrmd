// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities.Platform;

namespace Microsoft.Diagnostics.Runtime.MacOS.Trie
{
    internal sealed class MachOModuleExportTrie(ulong trieBaseAddress, uint trieDataSize, IMemoryReader reader, int bufferSize = 4096)
    {
        private const int MaxVisitedNodes = 4096;

        public ExportsTrieTerminal? FindSymbol(string symbolName)
        {
            using var virtualStream = new ReadVirtualStream(reader, (long)trieBaseAddress, trieDataSize);
            using Stream trie = new BufferedStream(virtualStream, bufferSize);
            return FindSymbol(trie, symbolName);
        }

        private static ExportsTrieTerminal? FindSymbol(Stream trie, string symbolName)
            => FindSymbol(trie, Encoding.UTF8.GetBytes(symbolName));

        /*
             +--------------------------------------------------------------------+
            | 1. Terminal Size     (ULEB128)                                      |
            +---------------------------------------------------------------------+
            | 2. Terminal Data     (Optional - Present ONLY if Terminal Size > 0) |
            |    ├── Flags         (ULEB128)                                      |
            |    ├── Symbol Offset (ULEB128) [RVA relative to Header]             |
            |    └── Other Info    (ULEB128 / C-String, depends on flags)         |
            +---------------------------------------------------------------------+
            | 3. Child Count       (uint8_t - 1 byte)                             |
            +---------------------------------------------------------------------+
            | 4. Child Array       (Repeated 'Child Count' times)                 |
            |    ├── Edge Label    (Null-terminated C-String)                     |
            |    └── Child Offset  (ULEB128 - byte offset from Start of Trie)     |
            +---------------------------------------------------------------------+
        */
        private static ExportsTrieTerminal? FindSymbol(Stream trie, ReadOnlySpan<byte> symbol)
        {
            long nodeOffset = 0;

            // An edge may be empty, which does not consume any part of the symbol, so the walk
            // is only guaranteed to terminate if a node is never visited twice.
            HashSet<long> visitedNodes = [];

            while (nodeOffset < trie.Length && visitedNodes.Add(nodeOffset) && visitedNodes.Count < MaxVisitedNodes)
            {
                trie.Position = nodeOffset;

                if (!trie.TryReadUleb128(out ulong terminalSize) || terminalSize > (ulong)(trie.Length - trie.Position))
                {
                    return null;
                }

                long terminalEnd = trie.Position + (long)terminalSize;
                if (symbol.IsEmpty && terminalSize > 0)
                {
                    return ExportsTrieTerminal.Read(trie, terminalEnd);
                }

                trie.Position = terminalEnd;
                bool foundChild = false;
                foreach (Child child in GetChildren(trie))
                {
                    if (ChildMatches(child, symbol))
                    {
                        symbol = symbol.Slice(child.Label.Count);
                        nodeOffset = (long)child.Offset;
                        foundChild = true;
                        break;
                    }
                }

                if (!foundChild)
                {
                    return null;
                }
            }

            return null;
        }

        private static bool ChildMatches(Child child, ReadOnlySpan<byte> symbol)
        {
            if (child.Label.Count > symbol.Length)
            {
                return false;
            }

            for (int i = 0; i < child.Label.Count; i++)
            {
                if (child.Label[i] != symbol[i])
                {
                    return false;
                }
            }

            // An empty label matches without consuming any part of the symbol
            return true;
        }

        private static IEnumerable<Child> GetChildren(Stream trie)
        {
            if (!trie.TryReadByte(out byte childCount))
            {
                yield break;
            }

            for (int i = 0; i < childCount; i++)
            {
                Child? child = Child.Read(trie);
                if (child is null)
                {
                    yield break;
                }

                yield return child.Value;
            }
        }

        private readonly record struct Child(IReadOnlyList<byte> Label, ulong Offset)
        {
            public static Child? Read(Stream trie)
            {
                var label = new List<byte>();
                while (true)
                {
                    if (!trie.TryReadByte(out byte value))
                    {
                        return null;
                    }

                    if (value == 0)
                    {
                        break;
                    }

                    label.Add(value);
                }

                if (!trie.TryReadUleb128(out ulong childOffset) || childOffset >= (ulong)trie.Length)
                {
                    return null;
                }

                return new Child(label, childOffset);
            }
        }
    }
}