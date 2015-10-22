using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class ValueTests
    {
        [TestMethod]
        public void PrimitiveConversionTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrThread thread = runtime.GetMainThread();


                ClrStackFrame frame = thread.GetFrame("Inner");

                ClrValue value = frame.GetLocal("b");
                Assert.AreEqual(true, value.AsBoolean());

                value = frame.GetLocal("c");
                Assert.AreEqual('c', value.AsChar());

                value = frame.GetLocal("s");
                Assert.AreEqual("hello world", value.AsString());


                frame = thread.GetFrame("Middle");

                value = frame.GetLocal("b");
                Assert.AreEqual(0x42, value.AsByte());
                
                value = frame.GetLocal("sb");
                Assert.AreEqual(0x43, value.AsSByte());

                value = frame.GetLocal("sh");
                Assert.AreEqual(0x4242, value.AsInt16());

                value = frame.GetLocal("ush");
                Assert.AreEqual(0x4243, value.AsUInt16());

                value = frame.GetLocal("i");
                Assert.AreEqual(0x42424242, value.AsInt32());

                value = frame.GetLocal("ui");
                Assert.AreEqual(0x42424243u, value.AsUInt32());


                frame = thread.GetFrame("Outer");

                value = frame.GetLocal("f");
                Assert.AreEqual(42.0f, value.AsFloat());

                value = frame.GetLocal("d");
                Assert.AreEqual(43.0, value.AsDouble());

                value = frame.GetLocal("ptr");
                Assert.AreEqual(new IntPtr(0x42424242), value.AsIntPtr());

                value = frame.GetLocal("uptr");
                Assert.AreEqual(new UIntPtr(0x43434343), value.AsUIntPtr());
            }
        }
    }
}
