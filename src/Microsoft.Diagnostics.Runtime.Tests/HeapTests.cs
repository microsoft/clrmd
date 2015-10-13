using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                ClrHeap heap = runtime.GetHeap();

                int count = 0;
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsNotNull(heap.GetObjectType(obj));
                    count++;
                }

                Assert.IsTrue(count > 0);
            }
        }

        [TestMethod]
        public void ServerSegmentTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

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
                ClrHeap heap = runtime.GetHeap();

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
