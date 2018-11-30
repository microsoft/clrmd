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
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var encounteredFoo = false;
                var count = 0;
                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    var type = heap.GetObjectType(obj);
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
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var objects = new List<ClrObject>(heap.EnumerateObjects());

                var count = 0;
                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    var actual = objects[count++];

                    Assert.Equal(obj, actual.Address);

                    var type = heap.GetObjectType(obj);
                    Assert.Equal(type, actual.Type);
                }

                Assert.True(count > 0);
            }
        }

        [Fact]
        public void HeapCachedEnumerationMatches()
        {
            // Simply test that we can enumerate the heap.
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var expectedList = new List<ClrObject>(heap.EnumerateObjects());

                heap.CacheHeap(CancellationToken.None);
                Assert.True(heap.IsHeapCached);
                var actualList = new List<ClrObject>(heap.EnumerateObjects());

                Assert.True(actualList.Count > 0);
                Assert.Equal(expectedList.Count, actualList.Count);

                for (var i = 0; i < actualList.Count; i++)
                {
                    var expected = expectedList[i];
                    var actual = actualList[i];

                    Assert.True(expected == actual);
                    Assert.Equal(expected, actual);
                }
            }
        }

        [Fact]
        public void ServerSegmentTests()
        {
            using (var dt = TestTargets.Types.LoadFullDump(GCMode.Server))
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                Assert.True(runtime.ServerGC);

                CheckSegments(heap);
            }
        }

        [Fact]
        public void WorkstationSegmentTests()
        {
            using (var dt = TestTargets.Types.LoadFullDump(GCMode.Workstation))
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                Assert.False(runtime.ServerGC);

                CheckSegments(heap);
            }
        }

        private static void CheckSegments(ClrHeap heap)
        {
            foreach (var seg in heap.Segments)
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

                foreach (var obj in seg.EnumerateObjectAddresses())
                {
                    var curr = heap.GetSegmentByAddress(obj);
                    Assert.Same(seg, curr);
                }
            }
        }
    }
}