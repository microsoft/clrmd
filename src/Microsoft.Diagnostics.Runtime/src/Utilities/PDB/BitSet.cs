// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal struct BitSet
    {
        internal BitSet(BitAccess bits)
        {
            bits.ReadInt32(out _size); // 0..3 : Number of words
            _words = new uint[_size];
            bits.ReadUInt32(_words);
        }

        internal bool IsSet(int index)
        {
            int word = index / 32;
            if (word >= _size) return false;

            return (_words[word] & GetBit(index)) != 0;
        }

        private static uint GetBit(int index)
        {
            return (uint)1 << (index % 32);
        }

        internal bool IsEmpty => _size == 0;

        private readonly int _size;
        private readonly uint[] _words;
    }
}