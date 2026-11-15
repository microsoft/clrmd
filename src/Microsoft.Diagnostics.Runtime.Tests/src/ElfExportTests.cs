// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ElfExportTests
    {
        private const string SymbolName = "DotNetRuntimeContractDescriptor";
        private const uint ImageSize = 0x800;
        private const uint DynamicOffset = 0x200;
        private const uint HashOffset = 0x300;
        private const uint SymbolOffset = 0x400;
        private const uint StringOffset = 0x500;
        private const uint ExportOffset = 0x600;

        [Theory]
        [InlineData(false, false, 0UL)]
        [InlineData(false, true, 0UL)]
        [InlineData(true, false, 0UL)]
        [InlineData(true, true, 0UL)]
        [InlineData(false, false, 0x400000UL)]
        [InlineData(false, true, 0x400000UL)]
        [InlineData(true, false, 0x400000UL)]
        [InlineData(true, true, 0x400000UL)]
        public void FindsExportInMemory(bool is64Bit, bool relocated, ulong preferredBase)
        {
            ulong imageBase = is64Bit ? 0x0000ffff70000000UL : 0x70000000UL;
            using MemoryStream stream = CreateImage(is64Bit, preferredBase, relocated ? (long)(imageBase - preferredBase) : 0);
            using ElfFile elf = new(stream, imageBase, leaveOpen: true, isVirtual: true);

            AssertExport(elf, preferredBase);
            AssertTableAddresses(elf, imageBase);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void FindsExportThroughModuleInfo(bool is64Bit, bool relocated)
        {
            ulong imageBase = is64Bit ? 0x0000ffff70000000UL : 0x70000000UL;
            using MemoryStream stream = CreateImage(is64Bit, preferredBase: 0, relocated ? (long)imageBase : 0);
            SyntheticDataReader reader = new(new() { [imageBase] = stream.ToArray() },
                is64Bit ? 8 : 4, is64Bit ? Architecture.X64 : Architecture.X86);
            ModuleInfo module = ModuleInfo.TryCreate(reader, imageBase, "hello");

            Assert.NotNull(module);
            Assert.Equal(ModuleKind.Elf, module.Kind);
            Assert.Equal(imageBase + ExportOffset, module.GetExportSymbolAddress(SymbolName));
            Assert.Equal(0UL, module.GetExportSymbolAddress("MissingExport"));
        }

        [Theory]
        [InlineData(false, false, 0x1000L)]
        [InlineData(false, true, 0x1000L)]
        [InlineData(true, false, 0x1000L)]
        [InlineData(true, true, 0x1000L)]
        [InlineData(false, false, -0x1000L)]
        [InlineData(false, true, -0x1000L)]
        [InlineData(true, false, -0x1000L)]
        [InlineData(true, true, -0x1000L)]
        public void FindsExportNearPreferredBase(bool is64Bit, bool relocated, long loadBias)
        {
            const ulong preferredBase = 0x400000;
            ulong imageBase = (ulong)((long)preferredBase + loadBias);
            using MemoryStream stream = CreateImage(is64Bit, preferredBase, relocated ? loadBias : 0);
            using ElfFile elf = new(stream, imageBase, leaveOpen: true, isVirtual: true);

            AssertExport(elf, preferredBase);
            AssertTableAddresses(elf, imageBase);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindsExportInFixedAddressExecutable(bool is64Bit)
        {
            const ulong imageBase = 0x400000;
            using MemoryStream stream = CreateImage(is64Bit, imageBase, pointerBias: 0);
            using ElfFile elf = new(stream, imageBase, leaveOpen: true, isVirtual: true);

            AssertExport(elf, imageBase);
            AssertTableAddresses(elf, imageBase);
        }

        [Theory]
        [InlineData(false, 0UL)]
        [InlineData(true, 0UL)]
        [InlineData(false, 0x400000UL)]
        [InlineData(true, 0x400000UL)]
        public void FindsExportInFile(bool is64Bit, ulong preferredBase)
        {
            using MemoryStream stream = CreateImage(is64Bit, preferredBase, pointerBias: 0);
            using ElfFile elf = new(stream);

            AssertExport(elf, preferredBase);
            AssertTableAddresses(elf, preferredBase);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public void PreservesNullDynamicPointers(bool is64Bit, int entryIndex)
        {
            const ulong imageBase = 0x70000000;
            using MemoryStream stream = CreateImage(is64Bit, preferredBase: 0, pointerBias: 0);
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            int pointerSize = is64Bit ? 8 : 4;
            stream.Position = DynamicOffset + entryIndex * pointerSize * 2 + pointerSize;
            WriteAddress(writer, is64Bit, 0);
            using ElfFile elf = new(stream, imageBase, leaveOpen: true, isVirtual: true);

            ElfDynamicSection section = Assert.IsType<ElfDynamicSection>(elf.DynamicSection);
            ulong address = entryIndex switch
            {
                0 => section.GnuHashTableVA,
                1 => section.StringTableVA,
                _ => section.SymbolTableVA
            };
            Assert.Equal(0UL, address);
            Assert.False(elf.TryGetExportSymbol(SymbolName, out _));
        }

        private static void AssertExport(ElfFile elf, ulong preferredBase)
        {
            Assert.True(elf.TryGetExportSymbol(SymbolName, out ulong value));
            Assert.Equal(preferredBase + ExportOffset, value);
            Assert.False(elf.TryGetExportSymbol("MissingExport", out _));
        }

        private static void AssertTableAddresses(ElfFile elf, ulong tableBase)
        {
            ElfDynamicSection section = Assert.IsType<ElfDynamicSection>(elf.DynamicSection);
            Assert.Equal(tableBase + HashOffset, section.GnuHashTableVA);
            Assert.Equal(tableBase + StringOffset, section.StringTableVA);
            Assert.Equal(tableBase + SymbolOffset, section.SymbolTableVA);
            Assert.Equal((ulong)Encoding.UTF8.GetByteCount("\0" + SymbolName + "\0"), section.StringTableSize);
        }

        private static MemoryStream CreateImage(bool is64Bit, ulong preferredBase, long pointerBias)
        {
            MemoryStream stream = new(new byte[ImageSize], writable: true);
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            ushort headerSize = is64Bit ? (ushort)64 : (ushort)52;
            ushort programHeaderSize = is64Bit ? (ushort)56 : (ushort)32;
            uint dynamicSize = is64Bit ? 80u : 40u;
            byte[] strings = Encoding.UTF8.GetBytes("\0" + SymbolName + "\0");
            ulong tableBase = unchecked(preferredBase + (ulong)pointerBias);

            writer.Write(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', is64Bit ? (byte)2 : (byte)1, 1, 1, 0 });
            writer.Write(0UL);
            writer.Write(preferredBase == 0 ? (ushort)3 : (ushort)2); // ET_DYN or ET_EXEC
            writer.Write(is64Bit ? (ushort)62 : (ushort)3);
            writer.Write(1u);
            WriteAddress(writer, is64Bit, 0);
            WriteAddress(writer, is64Bit, headerSize);
            WriteAddress(writer, is64Bit, 0);
            writer.Write(0u);
            writer.Write(headerSize);
            writer.Write(programHeaderSize);
            writer.Write((ushort)2);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);

            WriteProgramHeader(writer, is64Bit, ElfProgramHeaderType.Load, 0, preferredBase, ImageSize);
            WriteProgramHeader(writer, is64Bit, ElfProgramHeaderType.Dynamic, DynamicOffset, preferredBase + DynamicOffset, dynamicSize);

            stream.Position = DynamicOffset;
            WriteDynamicEntry(writer, is64Bit, ElfDynamicEntryTag.GnuHash, tableBase + HashOffset);
            WriteDynamicEntry(writer, is64Bit, ElfDynamicEntryTag.StrTab, tableBase + StringOffset);
            WriteDynamicEntry(writer, is64Bit, ElfDynamicEntryTag.SymTab, tableBase + SymbolOffset);
            WriteDynamicEntry(writer, is64Bit, ElfDynamicEntryTag.StrSz, (ulong)strings.Length);
            WriteDynamicEntry(writer, is64Bit, ElfDynamicEntryTag.Null, 0);

            // One GNU hash bucket, one bloom word, and one chain for symbol index 1.
            stream.Position = HashOffset;
            writer.Write(1u);
            writer.Write(1u);
            writer.Write(1u);
            writer.Write(0u);
            WriteAddress(writer, is64Bit, 0);
            writer.Write(1u);
            uint hash = 5381;
            foreach (char c in SymbolName)
                hash = unchecked(hash * 33 + c);
            writer.Write(hash | 1);

            stream.Position = SymbolOffset + (is64Bit ? 24 : 16);
            writer.Write(1u); // st_name
            if (is64Bit)
            {
                writer.Write((byte)0x11); // STB_GLOBAL | STT_OBJECT
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write(preferredBase + ExportOffset);
                writer.Write(8UL);
            }
            else
            {
                writer.Write((uint)(preferredBase + ExportOffset));
                writer.Write(4u);
                writer.Write((byte)0x11);
                writer.Write((byte)0);
                writer.Write((ushort)1);
            }

            stream.Position = StringOffset;
            writer.Write(strings);
            stream.Position = 0;
            return stream;
        }

        private static void WriteProgramHeader(BinaryWriter writer, bool is64Bit, ElfProgramHeaderType type, ulong offset, ulong address, ulong size)
        {
            writer.Write((uint)type);
            if (is64Bit)
                writer.Write(6u);
            WriteAddress(writer, is64Bit, offset);
            WriteAddress(writer, is64Bit, address);
            WriteAddress(writer, is64Bit, address);
            WriteAddress(writer, is64Bit, size);
            WriteAddress(writer, is64Bit, size);
            if (!is64Bit)
                writer.Write(6u);
            WriteAddress(writer, is64Bit, 0x1000);
        }

        private static void WriteDynamicEntry(BinaryWriter writer, bool is64Bit, ElfDynamicEntryTag tag, ulong value)
        {
            WriteAddress(writer, is64Bit, (ulong)tag);
            WriteAddress(writer, is64Bit, value);
        }

        private static void WriteAddress(BinaryWriter writer, bool is64Bit, ulong value)
        {
            if (is64Bit)
                writer.Write(value);
            else
                writer.Write((uint)value);
        }
    }
}