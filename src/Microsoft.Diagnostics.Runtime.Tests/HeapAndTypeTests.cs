using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class HeapAndTypeTests
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
                foreach (ulong obj in heap.EnumerateObjects())
                {
                    Assert.IsNotNull(heap.GetObjectType(obj));
                    count++;
                }

                Assert.IsTrue(count > 0);
            }
        }

        [TestMethod]
        public void ComponentType()
        {
            // Simply test that we can enumerate the heap.

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                
                foreach (ulong obj in heap.EnumerateObjects())
                {
                    var type = heap.GetObjectType(obj);
                    Assert.IsNotNull(type);

                    if (type.IsArray || type.IsPointer)
                        Assert.IsNotNull(type.ComponentType);
                    else
                        Assert.IsNull(type.ComponentType);
                }
            }
        }
    }
}
