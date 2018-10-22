using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class FinalizationQueueTests
    {
        [Fact]
        public void TestAllFinalizableObjects()
        {
            using (var dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var targetObjectsCount = 0;
                
                foreach (var address in runtime.Heap.EnumerateFinalizableObjectAddresses())
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type.Name == "DieFastA")
                        targetObjectsCount++;
                }
        
                Assert.Equal(42, targetObjectsCount);
            }
        }
        
        [Fact]
        public void TestFinalizerQueueObjects()
        {
            using (var dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var targetObjectsCount = 0;
                
                foreach (var address in runtime.EnumerateFinalizerQueueObjectAddresses())
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type.Name == "DieFastB")
                        targetObjectsCount++;
                }
        
                Assert.Equal(13, targetObjectsCount);
            }
        }
    }
}