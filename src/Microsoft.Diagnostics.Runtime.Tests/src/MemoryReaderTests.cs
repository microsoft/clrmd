// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MemoryReaderTests
    {
        [Fact]
        public void SearchMemoryTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (var segment in heap.Segments)
            {
                HashSet<ulong> seen = new HashSet<ulong>() { 0 };
                List<ClrObject> firstSeenObjectOfType = new List<ClrObject>();

                // We will search for method tables so make sure we
                foreach (ClrObject obj in segment.EnumerateObjects())
                    if (seen.Add(obj.Type.MethodTable))
                        firstSeenObjectOfType.Add(obj);

                foreach (ClrObject obj in firstSeenObjectOfType)
                {
                    // We make sure SearchMemory will find things that are not pointer aligned with 'i' here.
                    Span<byte> buffer = stackalloc byte[sizeof(ulong) + sizeof(int)];
                    for (int i = 0; i < sizeof(int); i++)
                    {
                        ulong expectedOffset = obj.Address - (uint)i;
                        if (expectedOffset < segment.Start)
                            continue;

                        Span<byte> slice = buffer.Slice(0, sizeof(ulong) + i);
                        if (!dt.DataReader.Read(expectedOffset, slice, out int read) || read != slice.Length)
                            continue;

                        ulong addressFound = dt.DataReader.SearchMemory(segment.Start, (int)segment.Length, buffer.Slice(0, i + sizeof(ulong)));

                        // There could still accidently be a pattern that matches this somewhere
                        while (addressFound != 0 && addressFound < expectedOffset)
                            addressFound = dt.DataReader.SearchMemory(addressFound + 1, (int)(segment.End - (addressFound + 1)), buffer.Slice(0, i + sizeof(ulong)));

                        Assert.Equal(expectedOffset, addressFound);
                    }
                }
            }
        }
    }
}
