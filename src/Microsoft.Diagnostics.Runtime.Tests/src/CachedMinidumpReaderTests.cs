// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.Windows;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class CachedMinidumpReaderTests
    {
        private const ulong BaseAddress = 0x10000;
        private const uint DataRva = 148;

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        public void ReadsCachedPagesOnAnyHostPlatform(int pointerSize)
        {
            int pageSize = Environment.SystemPageSize;
            byte[] data = new byte[2 * pageSize + 37];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 251);

            string path = Path.GetTempFileName();
            try
            {
                using FileStream stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                WriteMinidump(stream, pointerSize, data);

                CacheOptions options = new() { MaxDumpCacheSize = CachedMemoryReader.MinimumCacheSize };
                using Minidump dump = new(path, stream, options, leaveOpen: true);
                Assert.Equal(pointerSize, dump.PointerSize);
                CachedMemoryReader reader = Assert.IsType<CachedMemoryReader>(dump.MemoryReader);

                byte[] buffer = new byte[data.Length + 16];
                for (int pass = 0; pass < 2; pass++)
                {
                    // Repeat each read to cover both page-in and already-cached copies.
                    foreach ((int offset, int count) in new[] { (0, data.Length + 10), (17, 23), (pageSize - 7, 19), (2 * pageSize + 11, 42) })
                    {
                        Array.Fill(buffer, (byte)0xcc);
                        int expected = Math.Min(count, data.Length - offset);
                        int read = reader.Read(BaseAddress + (uint)offset, buffer.AsSpan(3, count));

                        Assert.Equal(expected, read);
                        Assert.Equal(data.AsSpan(offset, expected).ToArray(), buffer.AsSpan(3, read).ToArray());
                        Assert.All(buffer.AsSpan(0, 3).ToArray(), value => Assert.Equal((byte)0xcc, value));
                        Assert.All(buffer.AsSpan(3 + read).ToArray(), value => Assert.Equal((byte)0xcc, value));
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void WriteMinidump(FileStream stream, int pointerSize, byte[] data)
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // Header and three stream directories: system info, modules, and memory.
            writer.Write(0x504d444dU);
            writer.Write((ushort)0xa793);
            writer.Write((ushort)0);
            writer.Write(3U);
            writer.Write(32U);
            writer.Write(0U);
            writer.Write(0U);
            writer.Write(0UL);

            writer.Write((uint)MinidumpStreamType.SystemInfoStream);
            writer.Write(56U);
            writer.Write(68U);
            writer.Write((uint)MinidumpStreamType.ModuleListStream);
            writer.Write(4U);
            writer.Write(124U);
            writer.Write((uint)MinidumpStreamType.MemoryListStream);
            writer.Write(20U);
            writer.Write(128U);

            writer.Write((ushort)(pointerSize == 4 ? MinidumpProcessorArchitecture.Intel : MinidumpProcessorArchitecture.Amd64));
            writer.Write(new byte[54]);
            writer.Write(0U);
            writer.Write(1U);
            writer.Write(BaseAddress);
            writer.Write((uint)data.Length);
            writer.Write(DataRva);
            writer.Write(data);
            writer.Flush();

            // Force cached-reader selection without needing a large memory segment.
            stream.SetLength(CachedMemoryReader.MinimumCacheSize + 1L);
            stream.Position = 0;
        }
    }
}
