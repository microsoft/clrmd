using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class DynamicTests
    {
        [TestMethod]
        public void DynamicFieldValueTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrStackFrame frame = runtime.GetMainThread().GetFrame("Main");

                dynamic foo = frame.GetLocal("foo").Dynamic;
                Assert.AreEqual(42, foo.i);
                Assert.AreEqual('c', foo.c);
                
                Assert.AreEqual(42, foo.i);
                Assert.AreEqual(0x42u, foo.ui);
                Assert.AreEqual("string", foo.s);
                Assert.AreEqual(true, foo.b);
                Assert.AreEqual(4.2f, foo.f);
                Assert.AreEqual(8.4, foo.d);
                Assert.AreEqual('c', foo.c);
                Assert.AreEqual(0x12, foo.by);
                Assert.AreEqual((sbyte)0x13, foo.sby);
                Assert.AreEqual((short)0x4242, foo.sh);
                Assert.AreEqual((ushort)0x4343, foo.ush);
                Assert.AreEqual(0x424242ul, foo.ulng);
                Assert.AreEqual(0x434343L, foo.lng);
            }
        }


        [TestMethod]
        public void DynamicComparisonTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrStackFrame frame = runtime.GetMainThread().GetFrame("Main");

                dynamic foo = frame.GetLocal("foo").Dynamic;

                

                Assert.IsTrue(foo.i == 42);
                Assert.IsTrue(foo.i <= 43);
                Assert.IsTrue(foo.i >= 41);
                Assert.IsTrue(foo.i < 43);
                Assert.IsTrue(foo.i > 41);
                Assert.IsTrue(43 > foo.i);  // Ensure the other order works

                // TODO: Do the same with the rest of the types, see DynamicFieldValueTest.
            }
        }


        [TestMethod]
        public void DynamicConversionTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrStackFrame frame = runtime.GetMainThread().GetFrame("Main");

                ClrValue foo = frame.GetLocal("foo");


                // TODO: See DynamicFieldValueTests for a full list of what should be implemented
                dynamic di = foo.GetField("i").Dynamic;
                int i = di;
                Assert.AreEqual(42, i);

                // DESIGN:  We should always be able to convert an int to a long, right?
                long l = di;
                Assert.AreEqual(42L, l);

                // DESIGN:  However, this should probably NOT work right?  We can't write this in C#:
                //          int i = 7;  int ui = i;
                //          Should it work?  If so implement it.  If not, write a negative test case to ensure it
                //          throws some kind of sensible exception.
                //uint ui = di;
                //Assert.AreEqual(42u, ui);


                // TODO:  Create test cases for the other types here:
                //   dynamic dc = foo.GetField("c").Dynamic;
                //   char c = dc;
                //   Assert.AreEqual('c', c);
                //   ... etc ...
            }
        }

        [TestMethod]
        public void DynamicArrayTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                // TODO:  Only testing int and string arrays here, need to add more tests, but that requires updating the Targets to have objects of the correct type.
                var arrays = (from item in heap.EnumerateObjects()
                              where item.IsArray && (item.Type.ComponentType.ElementType == ClrElementType.Int32 || item.Type.ComponentType.IsString)
                              select item);

                foreach (ClrObject obj in arrays)
                {
                    dynamic array = obj.Dynamic;

                    int len = obj.Length;
                    Assert.AreEqual(len, array.Length);
                    
                    for (int i = 0; i < len; i++)
                    {
                        if (obj.Type.ComponentType.IsString)
                        {
                            string s = obj[i].AsString();
                            string t = array[i];

                            Assert.AreEqual(s, t);
                        }
                        else
                        {
                            int j = obj[i].AsInt32();
                            int k = array[i];

                            Assert.AreEqual(j, k);
                        }


                        ClrValue tmp = obj[i];

                    }
                }
            }
        }
    }
}