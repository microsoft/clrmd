// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfSymbolGnuHash
    {
        private readonly Reader _reader;
        private readonly ulong _chainsAddress;

        public ElfSymbolGnuHash(Reader reader, bool is64Bit, ulong address)
        {
            _reader = reader;

            BucketCount = reader.Read<uint>(ref address);
            SymbolOffset = reader.Read<uint>(ref address);
            BloomSize = reader.Read<uint>(ref address);
            BloomShift = reader.Read<uint>(ref address);

            if (BucketCount <= 0 || SymbolOffset == 0)
            {
                throw new InvalidDataException("ELF dump's hash bucket count or symbol offset invalid");
            }

            uint sizeTSize = is64Bit ? 8 : 4;
            address += sizeTSize * BloomSize;

            Buckets = new uint[BucketCount];
            byte[] buffer = new byte[BucketCount * Marshal.SizeOf<int>()];
            if (reader.ReadBytes(address, new Span<byte>(buffer)) != buffer.Length)
                throw new InvalidDataException("Error reading ELF dump's bucket array");

            for (int i = 0; i < BucketCount; i++)
                Buckets[i] = BitConverter.ToUInt32(buffer, i * Marshal.SizeOf<int>());

            _chainsAddress = address + (BucketCount * (uint)Marshal.SizeOf<int>());
        }

        public uint BucketCount { get; }

        public uint SymbolOffset { get; }

        public uint BloomSize { get; }

        public uint BloomShift { get; }

        public uint[] Buckets { get; }

        public IEnumerable<uint> GetPossibleSymbolIndex(string symbolName)
        {
            // This implementation completely ignores the bloom filter. The results should still be correct, but may
            // be slower to determine that a missing symbol isn't in the table.
            uint hash = Hash(symbolName);
            uint i = Buckets[hash % BucketCount] - SymbolOffset;
            for (;; i++)
            {
                int chainVal = GetChain(i);
                if((chainVal & 0xfffffffe) == (hash & 0xfffffffe))
                {
                    yield return i + SymbolOffset;
                }
                if ((chainVal & 0x1) == 0x1)
                {
                    break;
                }
            }
        }

        private static uint Hash(string symbolName)
        {
            byte[] utf8Chars = Encoding.UTF8.GetBytes(symbolName);
            uint h = 5381;
            for (int i = 0; i < utf8Chars.Length; i++)
            {
                h = unchecked((h << 5) + h + utf8Chars[i]);
            }
            return h;
        }

        private int GetChain(uint index)
        {
            return _reader.Read<int>(_chainsAddress + (index * 4));
        }
    }
}