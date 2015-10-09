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
        public void MethodHandleMultiDomainTests()
        {
            ulong[] methodDescs;
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("sharedlibrary.dll");
                ClrType type = module.GetTypeByName("Foo");
                ClrMethod method = type.GetMethod("Bar");
                methodDescs = method.EnumerateMethodDescs().ToArray();

                Assert.AreEqual(2, methodDescs.Length);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[0]);

                Assert.IsNotNull(method);
                Assert.AreEqual("Bar", method.Name);
                Assert.AreEqual("Foo", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[1]);

                Assert.IsNotNull(method);
                Assert.AreEqual("Bar", method.Name);
                Assert.AreEqual("Foo", method.Type.Name);
            }
        }

        [TestMethod]
        public void MethodHandleSingleDomainTests()
        {
            ulong methodDesc;
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("sharedlibrary.dll");
                ClrType type = module.GetTypeByName("Foo");
                ClrMethod method = type.GetMethod("Bar");
                methodDesc = method.EnumerateMethodDescs().Single();

                Assert.AreNotEqual(0ul, methodDesc);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDesc);

                Assert.IsNotNull(method);
                Assert.AreEqual("Bar", method.Name);
                Assert.AreEqual("Foo", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {

                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("sharedlibrary.dll");
                ClrType type = module.GetTypeByName("Foo");
                ClrMethod method = type.GetMethod("Bar");
                Assert.AreEqual(methodDesc, method.EnumerateMethodDescs().Single());
            }
        }
    }
}
