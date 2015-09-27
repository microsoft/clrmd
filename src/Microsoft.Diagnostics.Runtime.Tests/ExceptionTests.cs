using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class ExceptionTests
    {

        [TestMethod]
        public void ExceptionPropertyTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        public static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
            ClrException ex = thread.CurrentException;
            Assert.IsNotNull(ex);

            ExceptionTestData testData = TestTargets.NestedExceptionData;
            Assert.AreEqual(testData.OuterExceptionMessage, ex.Message);
            Assert.AreEqual(testData.OuterExceptionType, ex.Type.Name);
            Assert.IsNotNull(ex.Inner);
        }
    }
}
