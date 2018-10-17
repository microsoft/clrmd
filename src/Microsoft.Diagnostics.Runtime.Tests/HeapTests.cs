using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class HeapTests
    {
        [TestMethod]
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
                    Assert.IsNotNull(type);
                    if (type.Name == "Foo")
                        encounteredFoo = true;

                    count++;
                }

                Assert.IsTrue(encounteredFoo);
                Assert.IsTrue(count > 0);
            }
        }

        [TestMethod]
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

                    Assert.AreEqual(obj, actual.Address);

                    ClrType type = heap.GetObjectType(obj);
                    Assert.AreEqual(type, actual.Type);
                }

                Assert.IsTrue(count > 0);
            }
        }

        [TestMethod]
        public void HeapCachedEnumerationMatches()
        {
            // Simply test that we can enumerate the heap.
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                List<ClrObject> expectedList = new List<ClrObject>(heap.EnumerateObjects());

                heap.CacheHeap(CancellationToken.None);
                Assert.IsTrue(heap.IsHeapCached);
                List<ClrObject> actualList = new List<ClrObject>(heap.EnumerateObjects());

                Assert.IsTrue(actualList.Count > 0);
                Assert.AreEqual(expectedList.Count, actualList.Count);

                for (int i = 0; i < actualList.Count; i++)
                {
                    ClrObject expected = expectedList[i];
                    ClrObject actual = actualList[i];

                    Assert.IsTrue(expected == actual);
                    Assert.AreEqual(expected, actual);
                }
            }
        }

        [TestMethod]
        public void ServerSegmentTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                Assert.IsTrue(runtime.ServerGC);
                
                CheckSegments(heap);
            }
        }

        [TestMethod]
        public void WorkstationSegmentTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Workstation))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                Assert.IsFalse(runtime.ServerGC);

                CheckSegments(heap);
            }
        }

        private static void CheckSegments(ClrHeap heap)
        {
            foreach (ClrSegment seg in heap.Segments)
            {
                Assert.AreNotEqual(0ul, seg.Start);
                Assert.AreNotEqual(0ul, seg.End);
                Assert.IsTrue(seg.Start <= seg.End);

                Assert.IsTrue(seg.Start < seg.CommittedEnd);
                Assert.IsTrue(seg.CommittedEnd < seg.ReservedEnd);

                if (!seg.IsEphemeral)
                {
                    Assert.AreEqual(0ul, seg.Gen0Length);
                    Assert.AreEqual(0ul, seg.Gen1Length);
                }

                foreach (ulong obj in seg.EnumerateObjectAddresses())
                {
                    ClrSegment curr = heap.GetSegmentByAddress(obj);
                    Assert.AreSame(seg, curr);
                }
            }
        }
    }
}
