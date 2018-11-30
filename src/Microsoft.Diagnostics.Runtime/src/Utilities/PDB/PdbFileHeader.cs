// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class PdbFileHeader
    {
        //internal PdbFileHeader(int pageSize) {
        //  this.magic = new byte[32] {
        //            0x4D, 0x69, 0x63, 0x72, 0x6F, 0x73, 0x6F, 0x66, // "Microsof"
        //            0x74, 0x20, 0x43, 0x2F, 0x43, 0x2B, 0x2B, 0x20, // "t C/C++ "
        //            0x4D, 0x53, 0x46, 0x20, 0x37, 0x2E, 0x30, 0x30, // "MSF 7.00"
        //            0x0D, 0x0A, 0x1A, 0x44, 0x53, 0x00, 0x00, 0x00  // "^^^DS^^^"
        //        };
        //  this.pageSize = pageSize;
        //}

        internal PdbFileHeader(Stream reader, BitAccess bits)
        {
            bits.MinCapacity(56);
            reader.Seek(0, SeekOrigin.Begin);
            bits.FillBuffer(reader, 52);

            Magic = new byte[32];
            bits.ReadBytes(Magic); //   0..31
            bits.ReadInt32(out PageSize); //  32..35
            bits.ReadInt32(out FreePageMap); //  36..39
            bits.ReadInt32(out PagesUsed); //  40..43
            bits.ReadInt32(out DirectorySize); //  44..47
            bits.ReadInt32(out Zero); //  48..51

            int directoryPages = ((DirectorySize + PageSize - 1) / PageSize * 4 + PageSize - 1) / PageSize;
            DirectoryRoot = new int[directoryPages];
            bits.FillBuffer(reader, directoryPages * 4);
            bits.ReadInt32(DirectoryRoot);
        }

        internal readonly byte[] Magic;
        internal readonly int PageSize;
        internal int FreePageMap;
        internal int PagesUsed;
        internal int DirectorySize;
        internal readonly int Zero;
        internal int[] DirectoryRoot;
    }
}