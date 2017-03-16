using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Summary description for GCRootTests
    /// </summary>
    [TestClass]
    public class GCRootTests
    {
        [TestMethod]
        public void ObjectSetAddRemove()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsFalse(hash.Contains(obj));
                    hash.Add(obj);
                    Assert.IsTrue(hash.Contains(obj));
                }

                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsTrue(hash.Contains(obj));
                    hash.Remove(obj);
                    Assert.IsFalse(hash.Contains(obj));
                }
            }
        }

        [TestMethod]
        public void ObjectSetTryAdd()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsFalse(hash.Contains(obj));
                    Assert.IsTrue(hash.TryAdd(obj));
                    Assert.IsTrue(hash.Contains(obj));
                    Assert.IsFalse(hash.TryAdd(obj));
                    Assert.IsTrue(hash.Contains(obj));
                }
            }
        }
    }
}
