using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class MethodTests
    {
        [TestMethod]
        public void MethodHandleTests()
        {
            ulong[] methodDescs;
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("sharedlibrary.dll");
                ClrType type = module.GetTypeByName("Foo");
                ClrMethod method = type.GetMethod("Bar");
                methodDescs = method.EnumerateMethodHandles().ToArray();

                Assert.AreEqual(2, methodDescs.Length);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodFromHandle(methodDescs[0]);

                Assert.IsNotNull(method);
                Assert.AreEqual("Bar", method.Name);
                Assert.AreEqual("Foo", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodFromHandle(methodDescs[1]);

                Assert.IsNotNull(method);
                Assert.AreEqual("Bar", method.Name);
                Assert.AreEqual("Foo", method.Type.Name);
            }
        }
    }
}
