// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrStaticField field = runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_i");

                ulong addr = (ulong)field.GetValue(runtime.AppDomains.Single());
                ClrType type = heap.GetObjectType(addr);
                Assert.True(type.IsPrimitive);
                Assert.False(type.IsObjectReference);
                Assert.False(type.IsValueClass);

                object value = type.GetValue(addr);
                Assert.Equal("42", value.ToString());
                Assert.IsType<int>(value);
                Assert.Equal(42, (int)value);

                Assert.Contains(addr, heap.EnumerateObjectAddresses());
            }
        }

        [Fact]
        public void ArrayComponentTypeTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                // Ensure that we always have a component for every array type.
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    ClrType type = heap.GetObjectType(obj);
                    Assert.True(!type.IsArray || type.ComponentType != null);

                    foreach (ClrInstanceField field in type.Fields)
                    {
                        Assert.NotNull(field.Type);
                        Assert.True(!field.Type.IsArray || field.Type.ComponentType != null);
                        Assert.Same(heap, field.Type.Heap);
                    }
                }

                foreach (ClrModule module in runtime.Modules)
                {
                    foreach (ClrType type in module.EnumerateTypes())
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

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    ClrType type = heap.GetObjectType(obj);
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
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrType[] types = (from obj in heap.EnumerateObjectAddresses()
                             let t = heap.GetObjectType(obj)
                             where t.Name == TypeName
                             select t).ToArray();

                Assert.Equal(2, types.Length);
                Assert.NotSame(types[0], types[1]);

                ClrModule module = runtime.Modules.Single(m => Path.GetFileName(m.FileName).Equals("sharedlibrary.dll", StringComparison.OrdinalIgnoreCase));
                ClrType typeFromModule = module.GetTypeByName(TypeName);

                Assert.Equal(TypeName, typeFromModule.Name);
                Assert.Equal(types[0], typeFromModule);
            }
        }

        [Fact]
        public void VariableRootTest()
        {
            // Test to make sure that a specific static and local variable exist.

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.Exact;

                IEnumerable<ClrRoot> fooRoots = from root in heap.EnumerateRoots()
                               where root.Type.Name == "Foo"
                               select root;

                ClrRoot staticRoot = fooRoots.Single(r => r.Kind == GCRootKind.StaticVar);
                Assert.Contains("s_foo", staticRoot.Name);

                ClrRoot[] arr = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).ToArray();

                ClrRoot[] localVarRoots = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).ToArray();

                ClrThread thread = runtime.GetMainThread();
                ClrStackFrame main = thread.GetFrame("Main");
                ClrStackFrame inner = thread.GetFrame("Inner");

                ulong low = thread.StackBase;
                ulong high = thread.StackLimit;

                // Account for different platform stack direction.
                if (low > high)
                {
                    ulong tmp = low;
                    low = high;
                    high = tmp;
                }

                foreach (ClrRoot localVarRoot in localVarRoots)
                    Assert.True(low <= localVarRoot.Address && localVarRoot.Address <= high);
            }
        }

        [Fact]
        public void EETypeTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                HashSet<ulong> methodTables = (from obj in heap.EnumerateObjectAddresses()
                                    let type = heap.GetObjectType(obj)
                                    where !type.IsFree
                                    select heap.GetMethodTable(obj)).Unique();

                Assert.DoesNotContain(0ul, methodTables);

                foreach (ulong mt in methodTables)
                {
                    ClrType type = heap.GetTypeByMethodTable(mt);
                    ulong eeclass = heap.GetEEClassByMethodTable(mt);
                    Assert.NotEqual(0ul, eeclass);

                    Assert.NotEqual(0ul, heap.GetMethodTableByEEClass(eeclass));
                }
            }
        }

        [Fact]
        public void MethodTableHeapEnumeration()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                foreach (ClrType type in heap.EnumerateObjectAddresses().Select(obj => heap.GetObjectType(obj)).Unique())
                {
                    Assert.NotEqual(0ul, type.MethodTable);

                    ClrType typeFromHeap;

                    if (type.IsArray)
                    {
                        ClrType componentType = type.ComponentType;
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
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                int i = 0;
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    i++;
                    ClrType type = heap.GetObjectType(obj);

                    if (type.IsArray)
                    {
                        bool result = heap.TryGetMethodTable(obj, out ulong mt, out ulong cmt);

                        Assert.True(result);
                        Assert.NotEqual(0ul, mt);
                        Assert.Equal(type.MethodTable, mt);

                        Assert.Same(type, heap.GetTypeByMethodTable(mt, cmt));
                    }
                    else
                    {
                        ulong mt = heap.GetMethodTable(obj);

                        Assert.NotEqual(0ul, mt);
                        ulong[] collection = type.EnumerateMethodTables().ToArray();
                        Assert.Contains(type.MethodTable, collection);

                        Assert.Same(type, heap.GetTypeByMethodTable(mt));
                        Assert.Same(type, heap.GetTypeByMethodTable(mt, 0));

                        bool res = heap.TryGetMethodTable(obj, out ulong mt2, out ulong cmt);

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
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ulong[] fooObjects = (from obj in heap.EnumerateObjectAddresses()
                                  let t = heap.GetObjectType(obj)
                                  where t.Name == "Foo"
                                  select obj).ToArray();

                // There are exactly two Foo objects in the process, one in each app domain.
                // They will have different method tables.
                Assert.Equal(2, fooObjects.Length);

                ClrType fooType = heap.GetObjectType(fooObjects[0]);
                Assert.NotSame(fooType, heap.GetObjectType(fooObjects[1]));

                ClrRoot appDomainsFoo = (from root in heap.EnumerateRoots(true)
                                     where root.Kind == GCRootKind.StaticVar && root.Type == fooType
                                     select root).Single();

                ulong nestedExceptionFoo = fooObjects.Single(obj => obj != appDomainsFoo.Object);
                ClrType nestedExceptionFooType = heap.GetObjectType(nestedExceptionFoo);

                Assert.NotSame(nestedExceptionFooType, appDomainsFoo.Type);

                ulong nestedExceptionFooMethodTable = dt.DataReader.ReadPointerUnsafe(nestedExceptionFoo);
                ulong appDomainsFooMethodTable = dt.DataReader.ReadPointerUnsafe(appDomainsFoo.Object);

                // These are in different domains and should have different type handles:
                Assert.NotEqual(nestedExceptionFooMethodTable, appDomainsFooMethodTable);

                // The MethodTable returned by ClrType should always be the method table that lives in the "first"
                // AppDomain (in order of ClrAppDomain.Id).
                Assert.Equal(appDomainsFooMethodTable, fooType.MethodTable);

                // Ensure that we enumerate two type handles and that they match the method tables we have above.
                ulong[] methodTableEnumeration = fooType.EnumerateMethodTables().ToArray();
                Assert.Equal(2, methodTableEnumeration.Length);

                // These also need to be enumerated in ClrAppDomain.Id order
                Assert.Equal(appDomainsFooMethodTable, methodTableEnumeration[0]);
                Assert.Equal(nestedExceptionFooMethodTable, methodTableEnumeration[1]);
            }
        }

        [Fact]
        public void FieldNameAndValueTests()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrAppDomain domain = runtime.AppDomains.Single();

                ClrType fooType = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
                ulong obj = (ulong)runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_foo").GetValue(runtime.AppDomains.Single());

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
            ClrInstanceField field = type.GetFieldByName(name);
            Assert.NotNull(field);
            Assert.Equal(name, field.Name);

            object v = field.GetValue(obj);
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
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrAppDomain domain = runtime.AppDomains.Single();

                ClrModule typesModule = runtime.GetModule("types.exe");
                ClrType type = heap.GetTypeByName("Types");

                ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                ulong s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
                ulong s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
                ulong s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

                ulong[] expected = {s_one, s_two, s_three};

                ClrType arrayType = heap.GetObjectType(s_array);

                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i], (ulong)arrayType.GetArrayElementValue(s_array, i));

                    ulong address = arrayType.GetArrayElementAddress(s_array, i);
                    ulong value = dt.DataReader.ReadPointerUnsafe(address);

                    Assert.NotEqual(0ul, address);
                    Assert.Equal(expected[i], value);
                }
            }
        }

        [Fact]
        public void ArrayLengthTest()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrAppDomain domain = runtime.AppDomains.Single();

                ClrModule typesModule = runtime.GetModule("types.exe");
                ClrType type = heap.GetTypeByName("Types");

                ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                ClrType arrayType = heap.GetObjectType(s_array);

                Assert.Equal(3, arrayType.GetArrayLength(s_array));
            }
        }

        [Fact]
        public void ArrayReferenceEnumeration()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrAppDomain domain = runtime.AppDomains.Single();

                ClrModule typesModule = runtime.GetModule("types.exe");
                ClrType type = heap.GetTypeByName("Types");

                ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
                ulong s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
                ulong s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
                ulong s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

                ClrType arrayType = heap.GetObjectType(s_array);

                List<ulong> objs = new List<ulong>();
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