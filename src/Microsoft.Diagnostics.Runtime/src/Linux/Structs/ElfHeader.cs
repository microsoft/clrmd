// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfHeader
    {
        private const int EI_NIDENT = 16;

        private const byte Magic0 = 0x7f;
        private const byte Magic1 = (byte)'E';
        private const byte Magic2 = (byte)'L';
        private const byte Magic3 = (byte)'F';

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = EI_NIDENT)]
        public byte[] Ident;
        public ElfHeaderType Type;
        public ushort Machine;
        public uint Version;

        public IntPtr Entry;
        public IntPtr ProgramHeaderOffset;
        public IntPtr SectionHeaderOffset;

        public uint Flags;
        public ushort EHSize;
        public ushort ProgramHeaderEntrySize;
        public ushort ProgramHeaderCount;
        public ushort SectionHeaderEntrySize;
        public ushort SectionHeaderCount;
        public ushort SectionHeaderStringIndex;

        public ElfClass Class => (ElfClass)Ident[4];

        public ElfData Data => (ElfData)Ident[5];

        public bool IsValid
        {
            get
            {
                if (Ident[0] != Magic0 ||
                    Ident[1] != Magic1 ||
                    Ident[2] != Magic2 ||
                    Ident[3] != Magic3)
                    return false;

                return Data == ElfData.LittleEndian;
            }
        }


        public void Validate(string filename)
        {
            if (Ident[0] != Magic0 ||
                Ident[1] != Magic1 ||
                Ident[2] != Magic2 ||
                Ident[3] != Magic3)
                throw new InvalidDataException($"{MakeFileName(filename)} does not contain a valid ELF header.");

            if (Data != ElfData.LittleEndian)
                throw new InvalidDataException($"{MakeFileName(filename)} is BigEndian, which is unsupported");
        }

        private static string MakeFileName(string filename) => string.IsNullOrWhiteSpace(filename) ? "This coredump" : $"'{filename}'";
    }
}