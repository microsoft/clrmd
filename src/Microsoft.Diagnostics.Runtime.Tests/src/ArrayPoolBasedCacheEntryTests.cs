// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.MemoryMappedFiles;
using Microsoft.Diagnostics.Runtime.Windows;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ArrayPoolBasedCacheEntryTests
    {
        private const ulong BaseAddress = 0x10000;
        private const int DataOffset = 17;

        [Fact]
        public unsafe void ReadsCachedPagesOnAnyHostPlatform()
        {
            int pageSize = Environment.SystemPageSize;
            byte[] data = new byte[2 * pageSize + 37];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 251);

            using MemoryMappedFile mappedFile = MemoryMappedFile.CreateNew(mapName: null, capacity: DataOffset + data.Length);
            using (MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor())
                view.WriteArray(DataOffset, data, 0, data.Length);

            uint bytesPagedIn = 0;
            MinidumpSegment segment = new(DataOffset, BaseAddress, (ulong)data.Length);
            using ArrayPoolBasedCacheEntry entry = new(mappedFile, segment, size => bytesPagedIn += size);

            byte[] buffer = new byte[data.Length + 16];
            for (int pass = 0; pass < 2; pass++)
            {
                // Repeat each read to cover both page-in and already-cached copies.
                foreach ((int offset, int count) in new[] { (0, data.Length + 10), (17, 23), (pageSize - 7, 19), (2 * pageSize + 11, 42) })
                {
                    Array.Fill(buffer, (byte)0xcc);
                    int expected = Math.Min(count, data.Length - offset);
                    uint bytesRead;
                    fixed (byte* pBuffer = buffer)
                        entry.GetDataForAddress(BaseAddress + (uint)offset, (uint)count, new IntPtr(pBuffer + 3), out bytesRead);

                    int read = (int)bytesRead;
                    Assert.Equal(expected, read);
                    Assert.Equal(data.AsSpan(offset, expected).ToArray(), buffer.AsSpan(3, read).ToArray());
                    Assert.All(buffer.AsSpan(0, 3).ToArray(), value => Assert.Equal((byte)0xcc, value));
                    Assert.All(buffer.AsSpan(3 + read).ToArray(), value => Assert.Equal((byte)0xcc, value));
                }

                Assert.Equal((uint)data.Length, bytesPagedIn);
            }
        }
    }
}
