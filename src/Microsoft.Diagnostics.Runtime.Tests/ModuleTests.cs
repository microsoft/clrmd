using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class ModuleTests
    {
        [TestMethod]
        public void TestGetTypeByName()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                ClrModule shared = runtime.GetModule("sharedlibrary.dll");
                Assert.IsNotNull(shared.GetTypeByName("Foo"));
                Assert.IsNull(shared.GetTypeByName("Types"));

                ClrModule types = runtime.GetModule("types.exe");
                Assert.IsNotNull(types.GetTypeByName("Types"));
                Assert.IsNull(types.GetTypeByName("Foo"));
            }
        }
    }
}
