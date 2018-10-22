using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class FinalizationQueueTests
    {
        [TestMethod]
        public void TestAllFinalizableObjects()
        {
            using (var dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var targetObjectsCount = 0;
                
                foreach (var address in runtime.Heap.EnumerateFinalizableObjectAddresses())
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type.Name == typeof(DieFastA).FullName)
                        targetObjectsCount++;
                }
        
                Assert.AreEqual(FinalizationQueueTarget.ObjectsCountA, targetObjectsCount);
            }
        }
        
        [TestMethod]
        public void TestFinalizerQueueObjects()
        {
            using (var dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var targetObjectsCount = 0;
                
                foreach (var address in runtime.EnumerateFinalizerQueueObjectAddresses())
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type.Name == typeof(DieFastB).FullName)
                        targetObjectsCount++;
                }
        
                Assert.AreEqual(FinalizationQueueTarget.ObjectsCountB, targetObjectsCount);
            }
        }
    }
}