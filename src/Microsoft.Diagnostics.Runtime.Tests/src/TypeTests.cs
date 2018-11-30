using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class TypeTests
    {
        [Fact]
        public void IntegerObjectClrType()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var field = runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_i");

                var addr = (ulong)field.GetValue(runtime.AppDomains.Single());
                var type = heap.GetObjectType(addr);
                Assert.True(type.IsPrimitive);
                Assert.False(type.IsObjectReference);
                Assert.False(type.IsValueClass);

                var value = type.GetValue(addr);
                Assert.Equal("42", value.ToString());
                Assert.IsType<int>(value);
                Assert.Equal(42, (int)value);

                Assert.Contains(addr, heap.EnumerateObjectAddresses());
            }
        }

        [Fact]
        public void ArrayComponentTypeTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                // Ensure that we always have a component for every array type.
                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    var type = heap.GetObjectType(obj);
                    Assert.True(!type.IsArray || type.ComponentType != null);

                    foreach (var field in type.Fields)
                    {
                        Assert.NotNull(field.Type);
                        Assert.True(!field.Type.IsArray || field.Type.ComponentType != null);
                        Assert.Same(heap, field.Type.Heap);
                    }
                }

                foreach (var module in runtime.Modules)
                {
                    foreach (var type in module.EnumerateTypes())
                    {
                        Assert.True(!type.IsArray || type.ComponentType != null);
                        Assert.Same(heap, type.Heap);
                    }
                }
            }
        }

        [Fact]
        public void ComponentType()
        {
            // Simply test that we can enumerate the heap.

            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    var type = heap.GetObjectType(obj);
                    Assert.NotNull(type);

                    if (type.IsArray || type.IsPointer)
                        Assert.NotNull(type.ComponentType);
                    else
                        Assert.Null(type.ComponentType);
                }
            }
        }

        [Fact]
        public void TypeEqualityTest()
        {
            // This test ensures that only one ClrType is created when we have a type loaded into two different AppDomains with two different
            // method tables.

            const string TypeName = "Foo";
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var types = (from obj in heap.EnumerateObjectAddresses()
                             let t = heap.GetObjectType(obj)
                             where t.Name == TypeName
                             select t).ToArray();

                Assert.Equal(2, types.Length);
                Assert.NotSame(types[0], types[1]);

                var module = runtime.Modules.Where(m => Path.GetFileName(m.FileName).Equals("sharedlibrary.dll", StringComparison.OrdinalIgnoreCase)).Single();
                var typeFromModule = module.GetTypeByName(TypeName);

                Assert.Equal(TypeName, typeFromModule.Name);
                Assert.Equal(types[0], typeFromModule);
            }
        }

        [Fact]
        public void VariableRootTest()
        {
            // Test to make sure that a specific static and local variable exist.

            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.Exact;

                var fooRoots = from root in heap.EnumerateRoots()
                               where root.Type.Name == "Foo"
                               select root;

                var staticRoot = fooRoots.Where(r => r.Kind == GCRootKind.StaticVar).Single();
                Assert.Contains("s_foo", staticRoot.Name);

                var arr = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).ToArray();

                var localVarRoots = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).ToArray();

                var thread = runtime.GetMainThread();
                var main = thread.GetFrame("Main");
                var inner = thread.GetFrame("Inner");

                var low = thread.StackBase;
                var high = thread.StackLimit;

                // Account for different platform stack direction.
                if (low > high)
                {
                    var tmp = low;
                    low = high;
                    high = tmp;
                }

                foreach (var localVarRoot in localVarRoots)
                    Assert.True(low <= localVarRoot.Address && localVarRoot.Address <= high);
            }
        }

        [Fact]
        public void EETypeTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var methodTables = (from obj in heap.EnumerateObjectAddresses()
                                    let type = heap.GetObjectType(obj)
                                    where !type.IsFree
                                    select heap.GetMethodTable(obj)).Unique();

                Assert.DoesNotContain(0ul, methodTables);

                foreach (var mt in methodTables)
                {
                    var type = heap.GetTypeByMethodTable(mt);
                    var eeclass = heap.GetEEClassByMethodTable(mt);
                    Assert.NotEqual(0ul, eeclass);

                    Assert.NotEqual(0ul, heap.GetMethodTableByEEClass(eeclass));
                }
            }
        }

        [Fact]
        public void MethodTableHeapEnumeration()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                foreach (var type in heap.EnumerateObjectAddresses().Select(obj => heap.GetObjectType(obj)).Unique())
                {
                    Assert.NotEqual(0ul, type.MethodTable);

                    ClrType typeFromHeap;

                    if (type.IsArray)
                    {
                        var componentType = type.ComponentType;
                        Assert.NotNull(componentType);

                        typeFromHeap = heap.GetTypeByMethodTable(type.MethodTable, componentType.MethodTable);
                    }
                    else
                    {
                        typeFromHeap = heap.GetTypeByMethodTable(type.MethodTable);
                    }

                    Assert.Equal(type.MethodTable, typeFromHeap.MethodTable);
                    Assert.Same(type, typeFromHeap);
                }
            }
        }

        [Fact]
        public void GetObjectMethodTableTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var i = 0;
                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    i++;
                    var type = heap.GetObjectType(obj);

                    if (type.IsArray)
                    {
                        ulong mt, cmt;
                        var result = heap.TryGetMethodTable(obj, out mt, out cmt);

                        Assert.True(result);
                        Assert.NotEqual(0ul, mt);
                        Assert.Equal(type.MethodTable, mt);

                        Assert.Same(type, heap.GetTypeByMethodTable(mt, cmt));
                    }
                    else
                    {
                        var mt = heap.GetMethodTable(obj);

                        Assert.NotEqual(0ul, mt);
                        Assert.Contains(mt, type.EnumerateMethodTables());

                        Assert.Same(type, heap.GetTypeByMethodTable(mt));
                        Assert.Same(type, heap.GetTypeByMethodTable(mt, 0));

                        ulong mt2, cmt;
                        var res = heap.TryGetMethodTable(obj, out mt2, out cmt);

                        Assert.True(res);
                        Assert.Equal(mt, mt2);
                        Assert.Equal(0ul, cmt);
                    }
                }
            }
        }

        [Fact]
        public void EnumerateMethodTableTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var fooObjects = (from obj in heap.EnumerateObjectAddresses()
                                  let t = heap.GetObjectType(obj)
                                  where t.Name == "Foo"
                                  select obj).ToArray();

                // There are exactly two Foo objects in the process, one in each app domain.
                // They will have different method tables.
                Assert.Equal(2, fooObjects.Length);

                var fooType = heap.GetObjectType(fooObjects[0]);
                Assert.NotSame(fooType, heap.GetObjectType(fooObjects[1]));

                var appDomainsFoo = (from root in heap.EnumerateRoots(true)
                                     where root.Kind == GCRootKind.StaticVar && root.Type == fooType
                                     select root).Single();

                var nestedExceptionFoo = fooObjects.Where(obj => obj != appDomainsFoo.Object).Single();
                var nestedExceptionFooType = heap.GetObjectType(nestedExceptionFoo);

                Assert.NotSame(nestedExceptionFooType, appDomainsFoo.Type);

                var nestedExceptionFooMethodTable = dt.DataReader.ReadPointerUnsafe(nestedExceptionFoo);
                var appDomainsFooMethodTable = dt.DataReader.ReadPointerUnsafe(appDomainsFoo.Object);

                // These are in different domains and should have different type handles:
                Assert.NotEqual(nestedExceptionFooMethodTable, appDomainsFooMethodTable);

                // The MethodTable returned by ClrType should always be the method table that lives in the "first"
                // AppDomain (in order of ClrAppDomain.Id).
                Assert.Equal(appDomainsFooMethodTable, fooType.MethodTable);

                // Ensure that we enumerate two type handles and that they match the method tables we have above.
                var methodTableEnumeration = fooType.EnumerateMethodTables().ToArray();
                Assert.Equal(2, methodTableEnumeration.Length);

                // These also need to be enumerated in ClrAppDomain.Id order
                Assert.Equal(appDomainsFooMethodTable, methodTableEnumeration[0]);
                Assert.Equal(nestedExceptionFooMethodTable, methodTableEnumeration[1]);
            }
        }

        [Fact]
        public void FieldNameAndValueTests()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var domain = runtime.AppDomains.Single();

                var fooType = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
                var obj = (ulong)runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_foo").GetValue(runtime.AppDomains.Single());

                Assert.Same(fooType, heap.GetObjectType(obj));

                TestFieldNameAndValue(fooType, obj, "i", 42);
                TestFieldNameAndValue(fooType, obj, "s", "string");
                TestFieldNameAndValue(fooType, obj, "b", true);
                TestFieldNameAndValue(fooType, obj, "f", 4.2f);
                TestFieldNameAndValue(fooType, obj, "d", 8.4);
            }
        }

        public ClrInstanceField TestFieldNameAndValue<T>(ClrType type, ulong obj, string name, T value)
        {
            var field = type.GetFieldByName(name);
            Assert.NotNull(field);
            Assert.Equal(name, field.Name);

            var v = field.GetValue(obj);
            Assert.NotNull(v);
            Assert.IsType<T>(v);

            Assert.Equal(value, (T)v);

            return field;
        }
    }

    public class ArrayTests
    {
        [Fact]
        public void ArrayOffsetsTest()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var domain = runtime.AppDomains.Single();

                var typesModule = runtime.GetModule("types.exe");
                var type = heap.GetTypeByName("Types");

                var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                var s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
                var s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
                var s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

                ulong[] expected = {s_one, s_two, s_three};

                var arrayType = heap.GetObjectType(s_array);

                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i], (ulong)arrayType.GetArrayElementValue(s_array, i));

                    var address = arrayType.GetArrayElementAddress(s_array, i);
                    var value = dt.DataReader.ReadPointerUnsafe(address);

                    Assert.NotEqual(0ul, address);
                    Assert.Equal(expected[i], value);
                }
            }
        }

        [Fact]
        public void ArrayLengthTest()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var domain = runtime.AppDomains.Single();

                var typesModule = runtime.GetModule("types.exe");
                var type = heap.GetTypeByName("Types");

                var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                var arrayType = heap.GetObjectType(s_array);

                Assert.Equal(3, arrayType.GetArrayLength(s_array));
            }
        }

        [Fact]
        public void ArrayReferenceEnumeration()
        {
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var domain = runtime.AppDomains.Single();

                var typesModule = runtime.GetModule("types.exe");
                var type = heap.GetTypeByName("Types");

                var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                var s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
                var s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
                var s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

                var arrayType = heap.GetObjectType(s_array);

                var objs = new List<ulong>();
                arrayType.EnumerateRefsOfObject(s_array, (obj, offs) => objs.Add(obj));

                // We do not guarantee the order in which these are enumerated.
                Assert.Equal(3, objs.Count);
                Assert.Contains(s_one, objs);
                Assert.Contains(s_two, objs);
                Assert.Contains(s_three, objs);
            }
        }
    }
}