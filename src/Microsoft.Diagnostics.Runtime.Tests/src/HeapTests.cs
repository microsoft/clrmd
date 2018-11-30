// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class HeapTests
    {
        [Fact]
        public void HeapEnumeration()
        {
            // Simply test that we can enumerate the heap.
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                bool encounteredFoo = false;
                int count = 0;
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    ClrType type = heap.GetObjectType(obj);
                    Assert.NotNull(type);
                    if (type.Name == "Foo")
                        encounteredFoo = true;

                    count++;
                }

                Assert.True(encounteredFoo);
                Assert.True(count > 0);
            }
        }

        [Fact]
        public void HeapEnumerationMatches()
        {
            // Simply test that we can enumerate the heap.
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                List<ClrObject> objects = new List<ClrObject>(heap.EnumerateObjects());

                int count = 0;
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    ClrObject actual = objects[count++];

                    Assert.Equal(obj, actual.Address);

                    ClrType type = heap.GetObjectType(obj);
                    Assert.Equal(type, actual.Type);
                }

                Assert.True(count > 0);
            }
        }

        [Fact]
        public void HeapCachedEnumerationMatches()
        {
            // Simply test that we can enumerate the heap.
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                List<ClrObject> expectedList = new List<ClrObject>(heap.EnumerateObjects());

                heap.CacheHeap(CancellationToken.None);
                Assert.True(heap.IsHeapCached);
                List<ClrObject> actualList = new List<ClrObject>(heap.EnumerateObjects());

                Assert.True(actualList.Count > 0);
                Assert.Equal(expectedList.Count, actualList.Count);

                for (int i = 0; i < actualList.Count; i++)
                {
                    ClrObject expected = expectedList[i];
                    ClrObject actual = actualList[i];

                    Assert.True(expected == actual);
                    Assert.Equal(expected, actual);
                }
            }
        }

        [Fact]
        public void ServerSegmentTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                Assert.True(runtime.ServerGC);

                CheckSegments(heap);
            }
        }

        [Fact]
        public void WorkstationSegmentTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Workstation))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                Assert.False(runtime.ServerGC);

                CheckSegments(heap);
            }
        }

        private static void CheckSegments(ClrHeap heap)
        {
            foreach (ClrSegment seg in heap.Segments)
            {
                Assert.NotEqual(0ul, seg.Start);
                Assert.NotEqual(0ul, seg.End);
                Assert.True(seg.Start <= seg.End);

                Assert.True(seg.Start < seg.CommittedEnd);
                Assert.True(seg.CommittedEnd < seg.ReservedEnd);

                if (!seg.IsEphemeral)
                {
                    Assert.Equal(0ul, seg.Gen0Length);
                    Assert.Equal(0ul, seg.Gen1Length);
                }

                foreach (ulong obj in seg.EnumerateObjectAddresses())
                {
                    ClrSegment curr = heap.GetSegmentByAddress(obj);
                    Assert.Same(seg, curr);
                }
            }
        }
    }
}