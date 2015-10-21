using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class StackTests
    {
        [TestMethod]
        public void ObjectArgumentAndLocalTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrStackFrame frame = runtime.GetMainThread().GetFrame("Main");

                ClrObject args = frame.Arguments.Single().AsObject();
                Assert.IsFalse(args.IsNull);
                Assert.IsTrue(args.IsValid);

                Assert.AreEqual("System.String[]", args.Type.Name);
                
                ClrObject foo = frame.Locals.Single().AsObject();
                Assert.IsFalse(foo.IsNull);
                Assert.IsTrue(foo.IsValid);

                Assert.AreEqual("Foo", foo.Type.Name);
            }
        }
    }
}
