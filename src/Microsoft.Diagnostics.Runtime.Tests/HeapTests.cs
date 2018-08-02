using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public void OptimizedClassHistogram()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                // ensure that the optimized version is faster than the default one
                Stopwatch timing = new Stopwatch();
                var notOptimizedStatistics = new Dictionary<ClrType, TypeEntry>(32768);
                int notOptimizedObjectCount = 0;

                // with an optimized version
                timing.Start();

                foreach (var objDetails in heap.EnumerateObjectDetails())
                {
                    ClrType type = objDetails.Item3;
                    ulong size = objDetails.Item4;

                    TypeEntry entry;
                    if (!notOptimizedStatistics.TryGetValue(type, out entry))
                    {
                        entry = new TypeEntry()
                        {
                            TypeName = type.Name,
                            Size = 0
                        };
                        notOptimizedStatistics[type] = entry;
                    }
                    entry.Count++;
                    entry.Size += size;
                    notOptimizedObjectCount++;
                }

                timing.Stop();
                var notOptimizedTime = TimeSpan.FromMilliseconds(timing.ElapsedMilliseconds);


                // with an non-optimized version
                var statistics = new Dictionary<ClrType, TypeEntry>(32768);
                int objectCount = 0;
                timing.Restart();

                foreach (ulong objAddress in heap.EnumerateObjectAddresses())
                {
                    ClrType type = heap.GetObjectType(objAddress);
                    ulong size = type.GetSize(objAddress);

                    TypeEntry entry;
                    if (!statistics.TryGetValue(type, out entry))
                    {
                        entry = new TypeEntry()
                        {
                            TypeName = type.Name,
                            Size = 0
                        };
                        statistics[type] = entry;
                    }
                    entry.Count++;
                    entry.Size += size;
                    objectCount++;
                }

                timing.Stop();

                // check object count
                Assert.AreEqual(notOptimizedObjectCount, objectCount);

                // check heap content
                var types = notOptimizedStatistics.Keys;
                foreach (var type in types)
                {
                    var notOptimizedDetails = notOptimizedStatistics[type];
                    var details = statistics[type];

                    Assert.AreEqual(notOptimizedDetails.TypeName, details.TypeName);
                    Assert.AreEqual(notOptimizedDetails.Count, details.Count);
                    Assert.AreEqual(notOptimizedDetails.Size, details.Size);
                }
                Assert.AreEqual(types.Count, statistics.Count);

                // checking that optimized is faster could be flaky
                // Assert.IsTrue(notOptimizedTime > TimeSpan.FromMilliseconds(timing.ElapsedMilliseconds));
            }
        }
        class TypeEntry
        {
            public string TypeName;
            public int Count;
            public ulong Size;
        }

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
