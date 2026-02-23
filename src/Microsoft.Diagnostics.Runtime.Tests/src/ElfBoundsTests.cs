// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ElfBoundsTests
    {
        // ELF magic: 0x7f 'E' 'L' 'F'
        private static readonly byte[] ElfMagic = { 0x7f, 0x45, 0x4c, 0x46 };

        [Fact]
        public void RejectsExcessiveProgramHeaderCount()
        {
            // Build a minimal ELF64 header with ProgramHeaderCount = 60,000 (> 10,000 cap).
            using MemoryStream stream = CreateElf64WithProgramHeaderCount(60_000);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            {
                using ElfFile elf = new(stream);
                // ProgramHeaders are loaded lazily — access to trigger the check.
                _ = elf.ProgramHeaders;
            });
            Assert.Contains("program headers", ex.Message);
            Assert.Contains("exceeds the maximum", ex.Message);
        }

        [Fact]
        public void AcceptsReasonableProgramHeaderCount()
        {
            // An ELF with a reasonable program header count should not throw due to the cap.
            // It will likely fail for other reasons (missing data), but NOT the program header cap.
            using MemoryStream stream = CreateElf64WithProgramHeaderCount(3);
            try
            {
                using ElfFile elf = new(stream);
                // Access ProgramHeaders to trigger loading
                _ = elf.ProgramHeaders;
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("program headers") && ex.Message.Contains("exceeds"))
            {
                Assert.Fail("Should not reject an ELF with only 3 program headers as exceeding the limit.");
            }
            catch
            {
                // Other errors are expected for an incomplete ELF — that's fine.
            }
        }

        [Fact]
        public void RejectsExcessiveFileTableEntries()
        {
            // Build a minimal ELF64 coredump with an absurd file table entry count.
            using MemoryStream stream = CreateElf64CoredumpWithFileTableEntryCount(uint.MaxValue);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            {
                using ElfCoreFile core = new(stream);
                _ = core.LoadedImages;
            });
            Assert.Contains("file table", ex.Message.ToLowerInvariant());
            Assert.Contains("exceeds the maximum", ex.Message);
        }

        /// <summary>
        /// Creates a minimal ELF64 file with the specified program header count set in the header.
        /// The file contains a valid ELF header but no actual program header data.
        /// </summary>
        private static MemoryStream CreateElf64WithProgramHeaderCount(ushort programHeaderCount)
        {
            MemoryStream ms = new();
            BinaryWriter bw = new(ms);

            // ElfHeaderCommon (16 bytes e_ident):
            bw.Write(ElfMagic);           // magic (4 bytes)
            bw.Write((byte)2);            // class = 64-bit
            bw.Write((byte)1);            // data = little-endian
            bw.Write((byte)1);            // version
            bw.Write((byte)0);            // OS/ABI
            bw.Write(0u);                 // padding (4 bytes)
            bw.Write(0u);                 // padding (4 bytes)

            // Rest of ElfHeaderCommon shared fields:
            bw.Write((ushort)2);          // type = ET_EXEC (ElfHeaderType)
            bw.Write((ushort)0x3E);       // machine = EM_X86_64
            bw.Write(1u);                 // version

            // ElfHeader64 specific fields:
            bw.Write(0UL);               // entry point
            bw.Write(64UL);              // program header offset (right after the 64-byte ELF header)
            bw.Write(0UL);               // section header offset

            bw.Write(0u);                // flags
            bw.Write((ushort)64);        // eh_size
            bw.Write((ushort)56);        // program header entry size (sizeof ElfProgramHeader64)
            bw.Write(programHeaderCount); // program header count
            bw.Write((ushort)0);         // section header entry size
            bw.Write((ushort)0);         // section header count
            bw.Write((ushort)0);         // section header string index

            // Pad to ensure we have enough data for the stream (but not real program headers)
            bw.Write(new byte[256]);

            bw.Flush();
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Creates a minimal ELF64 coredump with a single PT_NOTE program header containing
        /// a file table note with an absurdly high entry count.
        /// </summary>
        private static MemoryStream CreateElf64CoredumpWithFileTableEntryCount(uint fileTableEntryCount)
        {
            MemoryStream ms = new();
            BinaryWriter bw = new(ms);

            // We need:
            // 1. Valid ELF64 header (64 bytes) with type=Core, 1 program header
            // 2. One PT_NOTE program header pointing to note data
            // 3. Note data containing a FILE note with absurd entry count

            int elfHeaderSize = 64;
            int phdrSize = 56; // sizeof(ElfProgramHeader64)
            int phdrOffset = elfHeaderSize;
            int noteDataOffset = elfHeaderSize + phdrSize;

            // Build the note:
            // ElfNoteHeader: NameSize(4) + ContentSize(4) + Type(4) = 12 bytes
            // Name: "CORE\0" padded to 8 bytes (align4(5) = 8)
            // Content: ElfFileTableHeader64 = EntryCount(8) + PageSize(8) = 16 bytes
            byte[] noteName = System.Text.Encoding.ASCII.GetBytes("CORE\0");
            uint nameSize = (uint)noteName.Length;
            uint nameSizeAligned = (nameSize + 3u) & ~3u;
            uint contentSize = 16; // ElfFileTableHeader64: EntryCount(8) + PageSize(8)

            int noteHeaderSize = 12;
            int totalNoteSize = noteHeaderSize + (int)nameSizeAligned + (int)contentSize;

            // ElfHeaderCommon (16 bytes e_ident):
            bw.Write(ElfMagic);
            bw.Write((byte)2);            // class = 64-bit
            bw.Write((byte)1);            // data = little-endian
            bw.Write((byte)1);            // version
            bw.Write((byte)0);            // OS/ABI
            bw.Write(0u);                 // padding
            bw.Write(0u);                 // padding

            // ElfHeaderCommon shared fields:
            bw.Write((ushort)4);          // type = ET_CORE (ElfHeaderType.Core)
            bw.Write((ushort)0x3E);       // machine = EM_X86_64
            bw.Write(1u);                 // version

            // ElfHeader64 specific fields:
            bw.Write(0UL);                // entry point
            bw.Write((ulong)phdrOffset);  // program header offset
            bw.Write(0UL);                // section header offset

            bw.Write(0u);                 // flags
            bw.Write((ushort)64);         // eh_size
            bw.Write((ushort)56);         // program header entry size
            bw.Write((ushort)1);          // program header count = 1
            bw.Write((ushort)0);          // section header entry size
            bw.Write((ushort)0);          // section header count
            bw.Write((ushort)0);          // section header string index

            // Program header (PT_NOTE):
            bw.Write((uint)4);            // type = PT_NOTE
            bw.Write(0u);                 // flags
            bw.Write((ulong)noteDataOffset); // file offset
            bw.Write(0UL);                // virtual address
            bw.Write(0UL);                // physical address
            bw.Write((ulong)totalNoteSize); // file size
            bw.Write((ulong)totalNoteSize); // mem size
            bw.Write(0UL);                // alignment

            // Note header:
            bw.Write(nameSize);            // NameSize
            bw.Write(contentSize);         // ContentSize
            bw.Write((uint)ElfNoteType.File); // Type = NT_FILE (0x46494c45)
            bw.Write(noteName);
            // Pad name to alignment
            for (int i = noteName.Length; i < (int)nameSizeAligned; i++)
                bw.Write((byte)0);

            // ElfFileTableHeader64: EntryCount + PageSize
            bw.Write((ulong)fileTableEntryCount); // EntryCount (absurdly large)
            bw.Write(4096UL);                      // PageSize

            bw.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
