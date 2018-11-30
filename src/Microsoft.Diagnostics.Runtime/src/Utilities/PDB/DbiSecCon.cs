// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal struct DbiSecCon
    {
        internal DbiSecCon(BitAccess bits)
        {
            bits.ReadInt16(out section);
            bits.ReadInt16(out pad1);
            bits.ReadInt32(out offset);
            bits.ReadInt32(out size);
            bits.ReadUInt32(out flags);
            bits.ReadInt16(out module);
            bits.ReadInt16(out pad2);
            bits.ReadUInt32(out dataCrc);
            bits.ReadUInt32(out relocCrc);
        }

        internal short section; // 0..1
        internal short pad1; // 2..3
        internal int offset; // 4..7
        internal int size; // 8..11
        internal uint flags; // 12..15
        internal short module; // 16..17
        internal short pad2; // 18..19
        internal uint dataCrc; // 20..23
        internal uint relocCrc; // 24..27
    }
}