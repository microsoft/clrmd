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
        public void TestExceptionProperties()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        [TestMethod]
        public void TestMinidumpExceptionProperties()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadMiniDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        private static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
            ClrException ex = thread.CurrentException;
            Assert.IsNotNull(ex);

            Assert.AreEqual("IOE Message", ex.Message);
            Assert.AreEqual("System.InvalidOperationException", ex.Type.Name);
            Assert.IsNotNull(ex.Inner);
        }
    }
}
