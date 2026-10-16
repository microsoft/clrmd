// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.MacOS;
using Microsoft.Diagnostics.Runtime.Tests.MacOs;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MachOModuleExportTrieTests
    {
        private const ulong ImageBase = 0x100000;
        private const uint ExportTrieFileOffset = 0x200;
        private const ulong ExportTrieVmAddress = 0x1000;

        [Fact]
        public void LooksUpRuntimeInfoInExportTrie()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void LooksUpRuntimeInfoInDyldInfoOnlyExportTrie()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie, useDyldInfo: true);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void PrefersExportsTrieOverDyldInfo()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie, useDyldInfo: true, useExportsTrie: true);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void LooksUpExportWithMultiByteUleb128Values()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x3456), nodeOffset: 0x100)
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x3456, address);
        }

        [Fact]
        public void LooksUpExportWhenChildPrecedesCachedWindow()
        {
            var trieBuilder = new MachOExportTrieBuilder();
            MachOExportTrieBuilder.Node terminal = trieBuilder.CreateNode("terminal")
                .WithTerminal(MachOExport.Regular(0x42))
                .AtOffset(0x200);
            MachOExportTrieBuilder.Node intermediate = trieBuilder.CreateNode("intermediate")
                .WithChild("NetRuntimeInfo", terminal)
                .AtOffset(0x300);
            trieBuilder.Root.WithChild("_Dot", intermediate);
            byte[] trie = trieBuilder.Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void TranslatesLinkeditAddressWithNonZeroTextVmAddress()
        {
            const ulong loadedImageBase = 0x110000000;
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie, loadedImageBase, textVmAddress: 0x100000000);

            bool found = TryLookupSymbol(reader, loadedImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(loadedImageBase + 0x42, address);
        }

        [Fact]
        public void ReturnsFalseWhenExportIsMissing()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_SomeOtherExport", MachOExport.Regular(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ReturnsFalseForMalformedExportTrie()
        {
            var trieBuilder = new MachOExportTrieBuilder();
            trieBuilder.Root.WithChildAtEnd("_DotNetRuntimeInfo");
            byte[] trie = trieBuilder.Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ReturnsFalseForTruncatedUleb128()
        {
            SyntheticDataReader reader = CreateReader([0x80]);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ReturnsFalseForOverflowingUleb128()
        {
            byte[] trie = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02];
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ReturnsFalseWhenTerminalDataExceedsItsDeclaredSize()
        {
            var trieBuilder = new MachOExportTrieBuilder();
            MachOExportTrieBuilder.Node terminal = trieBuilder.CreateNode("terminal")
                .WithRawTerminal([0x00, 0x42], declaredSize: 1);
            trieBuilder.Root.WithChild("_DotNetRuntimeInfo", terminal);
            byte[] trie = trieBuilder.Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ResolvesAbsoluteExportWithoutAddingImageBase()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Absolute(0x4242))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(0x4242UL, address);
        }

        [Fact]
        public void ReturnsFalseForReexportedSymbol()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Reexported(ordinal: 1))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ResolvesStubAndResolverExport()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.StubAndResolver(0x42, resolver: 0x84))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void ResolvesExportBehindZeroLengthEdge()
        {
            // dyld follows an empty edge without consuming any part of the symbol.
            var trieBuilder = new MachOExportTrieBuilder();
            MachOExportTrieBuilder.Node terminal = trieBuilder.CreateNode("terminal")
                .WithTerminal(MachOExport.Regular(0x42));
            MachOExportTrieBuilder.Node intermediate = trieBuilder.CreateNode("intermediate")
                .WithChild("_DotNetRuntimeInfo", terminal);
            trieBuilder.Root.WithChild("", intermediate);
            byte[] trie = trieBuilder.Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void ReturnsFalseForSelfReferencingChildOffset()
        {
            var trieBuilder = new MachOExportTrieBuilder();
            MachOExportTrieBuilder.Node loop = trieBuilder.CreateNode("loop");
            trieBuilder.Root.WithChild("", loop);
            loop.WithChild("", loop);
            byte[] trie = trieBuilder.Build();
            CountingMemoryReader reader = new(CreateReader(trie));

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
            Assert.InRange(reader.ReadCount, 1, 20);
        }

        [Fact]
        public void ReturnsFalseForChildOffsetPointingAtRoot()
        {
            var trieBuilder = new MachOExportTrieBuilder();
            trieBuilder.Root.WithChild("_DotNetRuntimeInfo", trieBuilder.Root);
            byte[] trie = trieBuilder.Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.False(found);
            Assert.Equal(0UL, address);
        }

        [Fact]
        public void ResolvesExportWithTrailingTerminalData()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Regular(0x42, 0xff, 0xff, 0xff))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void ResolvesWeakExport()
        {
            byte[] trie = new MachOExportTrieBuilder()
                .AddExport("_DotNetRuntimeInfo", MachOExport.Weak(0x42))
                .Build();
            SyntheticDataReader reader = CreateReader(trie);

            bool found = TryLookupSymbol(reader, ImageBase, "DotNetRuntimeInfo", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        [Fact]
        public void FallsBackToSymbolTableWhenExportTrieIsUnavailable()
        {
            SyntheticDataReader reader = CreateSymbolTableReader("_FallbackSymbol", 0x42);
            var module = new MachOModule(reader, ImageBase, "module");

            bool found = module.TryLookupSymbol("FallbackSymbol", out ulong address);

            Assert.True(found);
            Assert.Equal(ImageBase + 0x42, address);
        }

        private static SyntheticDataReader CreateReader(
            byte[] trie,
            ulong imageBase = ImageBase,
            ulong textVmAddress = 0,
            bool useDyldInfo = false,
            bool useExportsTrie = true)
        {
            uint trieSize = checked((uint)trie.Length);
            var headerBuilder = new MachOHeaderBuilder()
                .AddSegment("__TEXT", textVmAddress, fileOffset: 0, fileSize: ExportTrieFileOffset)
                .AddSegment(
                    "__LINKEDIT",
                    textVmAddress + ExportTrieVmAddress,
                    ExportTrieFileOffset,
                    trieSize);

            if (useExportsTrie)
            {
                headerBuilder.AddExportsTrie(ExportTrieFileOffset, trieSize);
            }

            if (useDyldInfo)
            {
                // When both commands are present the exports trie command wins, so point this one
                // at a location which does not contain a trie.
                headerBuilder.AddDyldInfoExports(
                    useExportsTrie ? 0U : ExportTrieFileOffset,
                    useExportsTrie ? 8U : trieSize);
            }

            return new SyntheticDataReader(new Dictionary<ulong, byte[]>
            {
                [imageBase] = headerBuilder.Build(),
                [imageBase + ExportTrieVmAddress] = trie,
            });
        }

        private static bool TryLookupSymbol(IDataReader reader, ulong imageBase, string symbol, out ulong address)
        {
            var module = new MachOModule(reader, imageBase, "single-file-host");
            return module.TryLookupSymbol(symbol, out address);
        }

        private static SyntheticDataReader CreateSymbolTableReader(string symbol, ulong symbolValue, byte type = 0x0f, byte section = 1)
        {
            const uint symbolTableOffset = 0x200;
            const uint stringTableOffset = 0x210;
            byte[] header = new MachOHeaderBuilder()
                .AddSegment("__TEXT", vmAddress: 0, fileOffset: 0, fileSize: 0x400)
                .AddSymbolTable(
                    symbolTableOffset,
                    symbolCount: 1,
                    stringTableOffset,
                    stringTableSize: checked((uint)symbol.Length + 2))
                .AddDynamicSymbolTable(externalDefinitionsCount: 1)
                .Build();

            using MemoryStream symbolTable = new();
            using (BinaryWriter writer = new(symbolTable, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(1U);
                writer.Write(type);
                writer.Write(section);
                writer.Write((ushort)0);
                writer.Write(symbolValue);
            }

            byte[] stringTable = new byte[symbol.Length + 2];
            Encoding.ASCII.GetBytes(symbol).CopyTo(stringTable, 1);

            return new SyntheticDataReader(new Dictionary<ulong, byte[]>
            {
                [ImageBase] = header,
                [ImageBase + symbolTableOffset] = symbolTable.ToArray(),
                [ImageBase + stringTableOffset] = stringTable,
            });
        }

        private sealed class CountingMemoryReader : IDataReader
        {
            private readonly IDataReader _reader;

            public int ReadCount { get; private set; }
            public int PointerSize => _reader.PointerSize;
            public string DisplayName => _reader.DisplayName;
            public bool IsThreadSafe => _reader.IsThreadSafe;
            public OSPlatform TargetPlatform => _reader.TargetPlatform;
            public Architecture Architecture => _reader.Architecture;
            public int ProcessId => _reader.ProcessId;

            public CountingMemoryReader(IDataReader reader) => _reader = reader;

            public IEnumerable<ModuleInfo> EnumerateModules() => _reader.EnumerateModules();

            public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) =>
                _reader.GetThreadContext(threadID, contextFlags, context);

            public void FlushCachedData() => _reader.FlushCachedData();

            public int Read(ulong address, Span<byte> buffer)
            {
                ReadCount++;
                return _reader.Read(address, buffer);
            }

            public bool Read<T>(ulong address, out T value) where T : unmanaged
            {
                ReadCount++;
                return _reader.Read(address, out value);
            }

            public T Read<T>(ulong address) where T : unmanaged
            {
                ReadCount++;
                return _reader.Read<T>(address);
            }

            public bool ReadPointer(ulong address, out ulong value)
            {
                ReadCount++;
                return _reader.ReadPointer(address, out value);
            }

            public ulong ReadPointer(ulong address)
            {
                ReadCount++;
                return _reader.ReadPointer(address);
            }
        }
    }
}