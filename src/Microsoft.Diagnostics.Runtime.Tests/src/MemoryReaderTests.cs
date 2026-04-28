// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MemoryReaderTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SearchMemoryTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // We make sure SearchMemory will find things that are not pointer aligned with 'i' here.
            Span<byte> buffer = stackalloc byte[sizeof(ulong) + sizeof(int)];

            foreach (ClrSegment segment in heap.Segments)
            {
                HashSet<ulong> seen = new() { 0 };
                List<ClrObject> firstSeenObjectOfType = new();

                // We will search for method tables so make sure we
                foreach (ClrObject obj in segment.EnumerateObjects())
                    if (seen.Add(obj.Type.MethodTable))
                        firstSeenObjectOfType.Add(obj);

                foreach (ClrObject obj in firstSeenObjectOfType)
                {
                    for (int i = 0; i < sizeof(int); i++)
                    {
                        ulong expectedOffset = obj.Address - (uint)i;
                        if (expectedOffset < segment.Start)
                            continue;

                        Span<byte> slice = buffer.Slice(0, sizeof(ulong) + i);
                        if (dt.DataReader.Read(expectedOffset, slice) != slice.Length)
                            continue;

                        ulong addressFound = dt.DataReader.SearchMemory(segment.Start, (int)segment.Length, buffer.Slice(0, i + sizeof(ulong)));

                        // There could still accidentally be a pattern that matches this somewhere
                        while (addressFound != 0 && addressFound < expectedOffset)
                            addressFound = dt.DataReader.SearchMemory(addressFound + 1, (int)(segment.End - (addressFound + 1)), buffer.Slice(0, i + sizeof(ulong)));

                        Assert.Equal(expectedOffset, addressFound);
                    }
                }
            }
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_TryGetDirectSpan_MatchesRead()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            ISegmentedDirectMemoryAccess direct = Assert.IsAssignableFrom<ISegmentedDirectMemoryAccess>(dt.DataReader);

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            int hits = 0;
            byte[] expected = new byte[8192];

            foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
            {
                if (obj.Address == 0)
                    continue;

                ulong size = obj.Size;
                if (size == 0 || size > (ulong)expected.Length)
                    continue;

                int len = (int)size;

                if (!direct.TryGetDirectSpan(obj.Address, len, out ReadOnlySpan<byte> span))
                    continue;   // segment-boundary case, not what this test exercises

                Assert.Equal(len, span.Length);

                int read = dt.DataReader.Read(obj.Address, expected.AsSpan(0, len));
                Assert.Equal(len, read);
                Assert.True(span.SequenceEqual(expected.AsSpan(0, len)),
                    $"direct span and Read() bytes diverged at object {obj.Address:x}");

                if (++hits >= 50)
                    break;
            }

            Assert.True(hits > 0, "fixture exposed no walkable objects (LockFreeMmfDataReader did not satisfy any TryGetDirectSpan)");
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_TryGetDirectSpan_BogusAddressFails()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            ISegmentedDirectMemoryAccess direct = Assert.IsAssignableFrom<ISegmentedDirectMemoryAccess>(dt.DataReader);

            Assert.False(direct.TryGetDirectSpan(0xBAADF00DBAADF00D, 8, out ReadOnlySpan<byte> span));
            Assert.Equal(0, span.Length);

            // Zero-length succeeds even on a bogus address by contract.
            Assert.True(direct.TryGetDirectSpan(0xBAADF00DBAADF00D, 0, out span));
            Assert.Equal(0, span.Length);
        }
    }
}

