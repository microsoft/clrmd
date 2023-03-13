// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            // Ensure that we never find objects within allocation contexts.
            MemoryRange[] allocationContexts = heap.EnumerateAllocationContexts().ToArray();
            Assert.NotEmpty(allocationContexts);

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

                Assert.DoesNotContain(allocationContexts, ac => ac.Contains(obj));
            }

            Assert.True(count > 0);
            Assert.True(encounteredFoo);
        }

        [Fact]
        public void AllocationContextLocation()
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // Ensure that we never find objects within allocation contexts.
            MemoryRange[] allocationContexts = heap.EnumerateAllocationContexts().ToArray();
            Assert.NotEmpty(allocationContexts);

            foreach (MemoryRange ac in allocationContexts)
            {
                Assert.True(ac.Length > 0);

                ClrSegment seg = heap.GetSegmentByAddress(ac.Start);
                Assert.NotNull(seg);
                Assert.Same(seg, heap.GetSegmentByAddress(ac.End - 1));

                Assert.True(seg.ObjectRange.Contains(ac));
            }
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
                if (seg.Length == 0)
                    continue;

                // Once without markers, once with
                NextObjectWorker(heap, seg);
                Assert.True(CheckMarkersValid(seg));
                NextObjectWorker(heap, seg);
            }
        }

        private static void NextObjectWorker(ClrHeap heap, ClrSegment seg)
        {
            ulong nextObj = heap.FindNextObjectOnSegment(seg.FirstObjectAddress);
            foreach (ClrObject obj in seg.EnumerateObjects().Skip(1))
            {
                Assert.Equal(obj.Address, nextObj);
                nextObj = heap.FindNextObjectOnSegment(obj);
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
                if (seg.Length == 0)
                {
                    continue;
                }

                ClrObject prev = heap.GetObject(seg.FirstObjectAddress);
                Assert.False(heap.FindPreviousObjectOnSegment(prev).IsValid);

                // Once without markers, once with
                PrevObjectWorker(heap, seg, prev);
                Assert.True(CheckMarkersValid(seg));
                PrevObjectWorker(heap, seg, prev);
            }
        }


        [Fact]
        public void EnumerateRange()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrSegment seg in heap.Segments)
            {
                ClrObject[] all = seg.EnumerateObjects().ToArray();

                for (int i = all.Length / 2 - 1; i > 0; i--)
                {
                    ClrObject[] slice = all[i..^i];

                    // Ensure the slice will be enumerated
                    Assert.Equal(all[i..^i], heap.EnumerateObjects(new MemoryRange(slice.First().Address, slice.Last().Address + 1)));

                    // Ensure we don't enumerate the last object when it equals the end
                    Assert.Equal(slice[..^1], heap.EnumerateObjects(new MemoryRange(slice.First().Address - 1, slice.Last().Address)));
                }

                if (all.Length > 2)
                {
                    MemoryRange range = new(seg.FirstObjectAddress - 0x10u, seg.CommittedMemory.End + 0x10);
                    Assert.Equal(all, heap.EnumerateObjects(range));
                }
            }
        }

        private static void PrevObjectWorker(ClrHeap heap, ClrSegment seg, ClrObject prev)
        {
            foreach (ClrObject curr in seg.EnumerateObjects().Skip(1))
            {
                Assert.Equal(prev.Address, heap.FindPreviousObjectOnSegment(curr));

                prev = curr;
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

            Assert.True(heap.Segments.Length > 0);

            CheckSegments(heap);
        }

        private static void CheckSegments(ClrHeap heap)
        {
            foreach (ClrSegment seg in heap.Segments)
            {
                Assert.NotEqual(0ul, seg.Start);
                Assert.NotEqual(0ul, seg.End);
                Assert.True(seg.Start <= seg.End);

                Assert.True(seg.Start < seg.CommittedMemory.End);
                Assert.True(seg.CommittedMemory.End < seg.ReservedMemory.End);
                Assert.False(seg.CommittedMemory.Overlaps(seg.ReservedMemory));
                Assert.True(seg.Length == 0 || seg.CommittedMemory.Contains(seg.ObjectRange));

                if (seg.Generation0.Length > 0)
                    Assert.True(seg.ObjectRange.Contains(seg.Generation0));

                if (seg.Generation1.Length > 0)
                    Assert.True(seg.ObjectRange.Contains(seg.Generation1));

                if (seg.Generation2.Length > 0)
                    Assert.True(seg.ObjectRange.Contains(seg.Generation2));

                Assert.True(seg.Generation2.Start == seg.Start);
                if (seg.Kind == GCSegmentKind.Ephemeral)
                {
                    Assert.True(seg.Generation2.End == seg.Generation1.Start);
                    Assert.True(seg.Generation1.End == seg.Generation0.Start);
                    Assert.True(seg.Generation0.End == seg.ObjectRange.End);
                }

                if (seg.Length == 0)
                {
                    continue;
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

        [Fact]
        public void MarkerCreation()
        {
            using DataTarget dt = TestTargets.GCRoot.LoadFullDump(GCMode.Workstation);
            using ClrRuntime rt = dt.ClrVersions.Single().CreateRuntime();

            foreach (ClrSegment seg in rt.Heap.Segments)
            {
                // Ensure we haven't touched this segment yet.
                for (int i = 0; i < seg.ObjectMarkers.Length; i++)
                    Assert.Equal(0u, seg.ObjectMarkers[i]);

                // Walk the whole heap.
                _ = seg.EnumerateObjects().Count();

                Assert.True(CheckMarkersValid(seg));
            }
        }

        private static bool CheckMarkersValid(ClrSegment seg)
        {
            bool any = false;
            uint last = 0;
            for (int i = 0; i < seg.ObjectMarkers.Length && seg.ObjectMarkers[i] != 0; i++)
            {
                any = true;

                Assert.True(last < seg.ObjectMarkers[i]);

                ClrObject obj = seg.SubHeap.Heap.GetObject(seg.FirstObjectAddress + seg.ObjectMarkers[i]);
                Assert.True(obj.IsValid);

                last = seg.ObjectMarkers[i];
            }

            return any;
        }

        [WindowsFact]
        public void DbgEngHeapEnumeration()
        {
            // This is more of a test for DbgEngDataReader than it is a test of heap enumeration.
            // Heap walking is sufficiently complicated to ensure that we didn't break the very
            // basics of data reading.

            ClrObject[] expectedObjs = GetObjects(TestTargets.Types);
            Assert.NotEmpty(expectedObjs);

            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrObject[] objs = runtime.Heap.EnumerateObjects().ToArray();

            Assert.NotEmpty(objs);
            Assert.Equal(expectedObjs, objs);
        }

        public ClrObject[] GetObjects(TestTarget target)
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = target.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            return runtime.Heap.EnumerateObjects().ToArray();
        }
    }
}
