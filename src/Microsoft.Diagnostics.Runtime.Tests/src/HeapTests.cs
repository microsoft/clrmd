// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class HeapTests
    {
        [Fact]
        public void HeapEnumeration()
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            bool encounteredFoo = false;
            int count = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                ClrType type = heap.GetObjectType(obj);
                Assert.NotNull(type);
                string name = type.Name;
                if (type.Name == "Foo")
                    encounteredFoo = true;

                count++;
            }

            Assert.True(count > 0);
            Assert.True(encounteredFoo);
        }

        [Fact]
        public void SegmentEnumeration()
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrObject[] objs = heap.EnumerateObjects().ToArray();

            // Enumerating each segment and then each object on each segment should produce
            // the same enumeration as ClrHeap.EnumerateObjects().
            int index = 0;
            foreach (ClrSegment seg in heap.Segments)
            {
                foreach (ClrObject obj in seg.EnumerateObjects())
                {
                    Assert.Equal(objs[index], obj);
                    index++;
                }
            }

            ClrSegment large = heap.Segments.Single(s => s.IsLargeObjectSegment);
            large.EnumerateObjects().ToArray();

            Assert.Equal(objs.Length, index);
        }

        [Fact]
        public void NextObject()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrSegment seg in heap.Segments)
            {
                ulong nextObj = seg.GetNextObjectAddress(seg.FirstObjectAddress);
                foreach (ClrObject obj in seg.EnumerateObjects().Skip(1))
                {
                    Assert.Equal(nextObj, obj.Address);
                    nextObj = seg.GetNextObjectAddress(obj);
                }
            }
        }

        [Fact]
        public void PrevObject()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrSegment seg in heap.Segments)
            {
                ClrObject prev = heap.GetObject(seg.FirstObjectAddress);
                Assert.Equal(0ul, seg.GetPreviousObjectAddress(prev));

                foreach (ClrObject curr in seg.EnumerateObjects().Skip(1))
                {
                    Assert.Equal(prev.Address, seg.GetPreviousObjectAddress(curr));

                    prev = curr;
                }
            }
        }

        [Fact]
        public void HeapEnumerationWhileClearingCache()
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            ClrObject[] objects = heap.EnumerateObjects().ToArray();
            Assert.NotEmpty(objects);

            int i = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                Assert.Equal(objects[i].Address, obj.Address);
                Assert.Equal(objects[i].Type, obj.Type);

                if ((i % 8) == 0)
                    runtime.FlushCachedData();

                i++;
            }
        }

        [Fact]
        public void ServerSegmentTests()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Assert.True(heap.IsServer);

            CheckSegments(heap);
        }

        [Fact]
        public void WorkstationSegmentTests()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Workstation);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Assert.False(heap.IsServer);

            Assert.True(heap.Segments.Count > 0);

            CheckSorted(heap.Segments);
            CheckSegments(heap);
        }

        private void CheckSorted(IReadOnlyList<ClrSegment> segments)
        {
            ClrSegment last = null;
            foreach (ClrSegment seg in segments)
            {
                if (last != null)
                    Assert.True(last.Start < seg.Start);

                last = seg;
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

                if (!seg.IsEphemeralSegment)
                {
                    Assert.Equal(0ul, seg.Gen0Length);
                    Assert.Equal(0ul, seg.Gen1Length);
                }

                int count = 0;
                foreach (ulong obj in seg.EnumerateObjects())
                {
                    ClrSegment curr = heap.GetSegmentByAddress(obj);
                    Assert.Same(seg, curr);
                    count++;
                }

                Assert.True(count >= 1);
            }
        }
    }
}
