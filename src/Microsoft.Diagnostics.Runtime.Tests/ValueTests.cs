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
        public void NullValueOkTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrValue fooObject = runtime.GetMainThread().GetFrame("Main").GetLocal("containsnullref");
                
                Assert.AreEqual(42, fooObject.GetObject("SetValue").GetInt32("i"));
                Assert.IsTrue(fooObject.GetObject("NullValue").IsNull);
                Assert.IsNull(fooObject.GetObject("NullValue").GetInt32OrNull("i"));
            }
        }

        [TestMethod]
        public void BoxedObjectTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                var ints = (from item in heap.EnumerateObjects()
                            where item.Type.ElementType == ClrElementType.Int32
                            select item);

                foreach (ClrObject obj in ints)
                {
                    int val = (int)obj.Type.GetValue(obj.Address);
                    ClrValue tmp = obj.Unbox();

                    int clrValue = tmp.AsInt32();
                    Assert.AreEqual(val, clrValue);
                }
            }
        }

        [TestMethod]
        public void RoundTripTest()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                foreach (ClrObject obj in heap.EnumerateObjects())
                {
                    ClrValue val = obj.AsClrValue();

                    Assert.AreEqual(obj.Type, val.Type);
                    Assert.AreEqual(obj.Address, val.Address);

                    ClrObject obj2 = val.AsObject();
                    
                    Assert.AreEqual(obj.Type, obj2.Type);
                    Assert.AreEqual(obj.Address, obj2.Address);
                }

            }
        }


        [TestMethod]
        public void ArrayElementTests()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                var arrays = (from item in heap.EnumerateObjects()
                              where item.IsArray && (item.Type.ComponentType.ElementType == ClrElementType.Int32 || item.Type.ComponentType.IsObjectReference)
                              select item);

                foreach (ClrObject obj in arrays)
                {
                    ClrValue clrValue = obj.AsClrValue();
                    int len = obj.Type.GetArrayLength(obj.Address);
                    Assert.AreEqual(len, obj.Length);

                    bool isIntArray = obj.Type.ComponentType.ElementType == ClrElementType.Int32;

                    for (int i = 0; i < len; i++)
                    {
                        if (isIntArray)
                        {
                            int value = (int)obj.Type.GetArrayElementValue(obj.Address, i);
                            int indexedValue = obj[i].AsInt32();
                            Assert.AreEqual(value, indexedValue);

                            indexedValue = clrValue[i].AsInt32();
                            Assert.AreEqual(value, indexedValue);
                        }
                        else
                        {
                            object o = obj.Type.GetArrayElementValue(obj.Address, i);
                            if (o == null)
                                continue;

                            if (o is string)
                            {
                                string value = (string)o;
                                string indexedValue = obj[i].AsString();
                                Assert.AreEqual(value, indexedValue);

                                indexedValue = clrValue[i].AsString();
                                Assert.AreEqual(value, indexedValue);
                            }
                            else
                            {
                                ulong value = (ulong)o;
                                ulong indexedValue = obj[i].AsObject().Address;
                                Assert.AreEqual(value, indexedValue);

                                indexedValue = clrValue[i].AsObject().Address;
                                Assert.AreEqual(value, indexedValue);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void AsClrValueTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();

                var ints = (from item in heap.EnumerateObjects()
                            where item.Type.ElementType == ClrElementType.Int32
                            select item);

                foreach (ClrObject obj in ints)
                {
                    int val = (int)obj.Type.GetValue(obj.Address);
                    ClrValue tmp = obj.AsClrValue();

                    int clrValue = tmp.AsInt32();
                    Assert.AreEqual(val, clrValue);
                }
            }
        }

        [TestMethod]
        public void PrimitiveVariableConversionTest()
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

                value = frame.GetLocal("st");
                Assert.AreEqual(42, value.GetInt32("i"));


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

        [TestMethod]
        public void ObjectFieldTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrStackFrame frame = runtime.GetMainThread().GetFrame("Main");

                ClrValue value = frame.GetLocal("foo");
                Assert.AreEqual(42, value.GetInt32("i"));
                Assert.AreEqual(0x42u, value.GetUInt32("ui"));
                Assert.AreEqual("string", value.GetString("s"));
                Assert.AreEqual(true, value.GetBoolean("b"));
                Assert.AreEqual(4.2f, value.GetFloat("f"));
                Assert.AreEqual(8.4, value.GetDouble("d"));
                Assert.AreEqual('c', value.GetChar("c"));
                Assert.AreEqual(0x12, value.GetByte("by"));
                Assert.AreEqual((sbyte)0x13, value.GetSByte("sby"));
                Assert.AreEqual((short)0x4242, value.GetInt16("sh"));
                Assert.AreEqual((ushort)0x4343, value.GetUInt16("ush"));
                Assert.AreEqual(0x424242ul, value.GetUInt64("ulng"));
                Assert.AreEqual(0x434343L, value.GetInt64("lng"));

                ClrObject obj = value.AsObject();
                Assert.AreEqual("Foo", obj.Type.Name);
                Assert.AreEqual(42, obj.GetInt32("i"));
                Assert.AreEqual(0x42u, obj.GetUInt32("ui"));
                Assert.AreEqual("string", obj.GetString("s"));
                Assert.AreEqual(true, obj.GetBoolean("b"));
                Assert.AreEqual(4.2f, obj.GetFloat("f"));
                Assert.AreEqual(8.4, obj.GetDouble("d"));
                Assert.AreEqual('c', obj.GetChar("c"));
                Assert.AreEqual(0x12, obj.GetByte("by"));
                Assert.AreEqual((sbyte)0x13, obj.GetSByte("sby"));
                Assert.AreEqual((short)0x4242, obj.GetInt16("sh"));
                Assert.AreEqual((ushort)0x4343, obj.GetUInt16("ush"));
                Assert.AreEqual(0x424242ul, obj.GetUInt64("ulng"));
                Assert.AreEqual(0x434343L, obj.GetInt64("lng"));
            }
        }

        [TestMethod]
        public void ObjectLocalVariableTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrThread thread = runtime.GetMainThread();
                ClrStackFrame frame = thread.GetFrame("Main");

                ClrValue value = frame.GetLocal("foo");
                ClrObject obj = value.AsObject();
                Assert.IsTrue(obj.IsValid);
                Assert.IsFalse(obj.IsNull);
                Assert.AreEqual("Foo", obj.Type.Name);
                Assert.AreSame(obj.Type, value.Type);
                Assert.AreSame(heap.GetObjectType(obj.Address), value.Type);
            }
        }



        [TestMethod]
        public void GetFieldTests()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrThread thread = runtime.GetMainThread();
                ClrStackFrame frame = thread.GetFrame("Main");

                ClrValue value = frame.GetLocal("foo");
                Assert.AreEqual("Foo", value.Type.Name);  // Ensure we have the right object.

                ClrObject obj = value.AsObject();
                Assert.IsTrue(obj.GetBoolean("b"));

                ClrValue val = obj.GetField("st").GetField("middle").GetField("inner");
                Assert.IsTrue(val.GetField("b").AsBoolean());
                Assert.IsTrue(val.GetBoolean("b"));
                
                obj = obj.GetField("st").GetField("middle").GetObject("inner");
                Assert.IsTrue(obj.GetField("b").AsBoolean());
                Assert.IsTrue(obj.GetBoolean("b"));
            }
        }

        [TestMethod]
        public void StructVariableTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrThread thread = runtime.GetMainThread();
                ClrStackFrame frame = thread.GetFrame("Main");

                ClrValue value = frame.GetLocal("s");
                Assert.AreEqual("Struct", value.Type.Name);

                CheckStruct(value);
            }
        }

        private static void CheckStruct(ClrValue value)
        {
            Assert.AreEqual(42, value.GetField("i").AsInt32());
            Assert.AreEqual("string", value.GetField("s").AsString());
            Assert.AreEqual(true, value.GetField("b").AsBoolean());
            Assert.AreEqual(4.2f, value.GetField("f").AsFloat());
            Assert.AreEqual(8.4, value.GetField("d").AsDouble());
            Assert.AreEqual("System.Object", value.GetField("o").AsObject().Type.Name);
        }

        [TestMethod]
        public void InteriorStructTest()
        {
            using (DataTarget dt = TestTargets.LocalVariables.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.GetHeap();
                ClrThread thread = runtime.GetMainThread();
                ClrStackFrame frame = thread.GetFrame("Main");

                ClrValue value = frame.GetLocal("s");
                Assert.AreEqual("Struct", value.Type.Name);
                CheckStruct(value);

                value = value.GetField("middle");
                CheckStruct(value);

                value = value.GetField("inner");
                CheckStruct(value);
            }
        }
    }
}
