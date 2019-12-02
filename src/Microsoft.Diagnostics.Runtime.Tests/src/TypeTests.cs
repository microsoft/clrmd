// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class TypeTests
    {
        public static readonly string ModuleName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "types.exe" : "types.dll";

        [Fact]
        public void IntegerObjectClrType()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrModule module = runtime.GetModule(ModuleName);
            ClrType typesType = module.GetTypeByName("Types");
            ClrStaticField field = typesType.GetStaticFieldByName("s_i");

            ulong addr = (ulong)field.GetValue(runtime.AppDomains.Single());
            ClrType type = heap.GetObjectType(addr);
            Assert.NotNull(type);

            ClrObject obj = new ClrObject(addr, type);
            Assert.False(obj.IsNull);

            Assert.True(type.IsPrimitive);
            Assert.False(type.IsObjectReference);
            Assert.False(type.IsValueClass);

            var fds = obj.Type.Fields;

            int value = obj.ReadBoxed<int>();
            Assert.Equal(42, value);

            Assert.Contains(addr, heap.EnumerateObjects().Select(a => a.Address));
        }

        [FrameworkFact]
        public void ArrayComponentTypeTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // Ensure that we always have a component for every array type.
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                ClrType type = obj.Type;
                Assert.True(!type.IsArray || type.ComponentType != null);

                foreach (ClrInstanceField field in type.Fields)
                {
                    Assert.NotNull(field.Type);
                    Assert.Same(heap, field.Type.Heap);
                }
            }

            foreach (ClrModule module in runtime.AppDomains.SelectMany(ad => ad.Modules))
            {
                foreach (ClrType type in module.EnumerateTypes())
                {
                    Assert.True(!type.IsArray || type.ComponentType != null);
                    Assert.Same(heap, type.Heap);
                }
            }
        }

        [Fact]
        public void ComponentType()
        {
            // Simply test that we can enumerate the heap.

            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                ClrType type = obj.Type;
                Assert.NotNull(type);

                if (type.IsArray || type.IsPointer)
                    Assert.NotNull(type.ComponentType);
                else
                    Assert.Null(type.ComponentType);
            }
        }

        [FrameworkFact]
        public void TypeEqualityTest()
        {
            // This test ensures that only one ClrType is created when we have a type loaded into two different AppDomains with two different
            // method tables.

            const string TypeName = "Foo";
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrType[] types = (from obj in heap.EnumerateObjects()
                               let t = heap.GetObjectType(obj.Address)
                               where t.Name == TypeName
                               orderby t.TypeHandle
                               select t).ToArray();

            Assert.Equal(2, types.Length);
            Assert.NotSame(types[0], types[1]);



            ClrType[] typesFromModule = (from module in runtime.EnumerateModules()
                                         let name = Path.GetFileNameWithoutExtension(module.FileName)
                                         where name.Equals("sharedlibrary", StringComparison.OrdinalIgnoreCase)
                                         let type = module.GetTypeByName(TypeName)
                                         select type).ToArray();

            Assert.Equal(2, typesFromModule.Length);
            Assert.NotSame(types[0], types[1]);

            Assert.Same(types[0], typesFromModule[0]);
            Assert.Same(types[1], typesFromModule[1]);
        }

        [WindowsFact]
        public void VariableRootTest()
        {
            // Test to make sure that a specific static and local variable exist.

            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            IEnumerable<IClrRoot> fooRoots = from root in heap.EnumerateRoots()
                                            where root.Object.Type.Name == "Foo"
                                            select root;

            IClrRoot[] localVarRoots = fooRoots.Where(r => r.RootKind == ClrRootKind.Stack).ToArray();

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

            foreach (IClrRoot localVarRoot in localVarRoots)
                Assert.True(low <= localVarRoot.Address && localVarRoot.Address <= high);
        }

        [Fact]
        public void MethodTableHeapEnumeration()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrType type in heap.EnumerateObjects().Select(obj => heap.GetObjectType(obj.Address)).Unique())
            {
                Assert.NotEqual(0ul, type.TypeHandle);

                ClrType typeFromHeap;

                if (type.IsArray)
                {
                    ClrType componentType = type.ComponentType;
                    Assert.NotNull(componentType);

                    typeFromHeap = runtime.GetTypeByMethodTable(type.TypeHandle);
                }
                else
                {
                    typeFromHeap = runtime.GetTypeByMethodTable(type.TypeHandle);
                }

                Assert.Equal(type.TypeHandle, typeFromHeap.TypeHandle);
                Assert.Same(type, typeFromHeap);
            }
        }

        [FrameworkFact]
        public void GetObjectMethodTableTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int i = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                i++;
                ClrType type = obj.Type;
                Assert.NotNull(type);

                ulong mt = dt.DataReader.ReadPointerUnsafe(obj);
                Assert.NotEqual(0ul, mt);

                Assert.Same(type, runtime.GetTypeByMethodTable(mt));
                Assert.Equal(mt, type.TypeHandle);
            }
        }

        [FrameworkFact]
        public void EnumerateMethodTableTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrObject[] fooObjects = (from obj in heap.EnumerateObjects()
                                      where obj.Type.Name == "Foo"
                                      select obj).ToArray();

            // There are exactly two Foo objects in the process, one in each app domain.
            // They will have different method tables.
            Assert.Equal(2, fooObjects.Length);

            ClrType fooType = heap.GetObjectType(fooObjects[0]);
            ClrType fooType2 = heap.GetObjectType(fooObjects[1]);
            Assert.NotSame(fooType, fooType2);

            ClrObject appDomainsFoo = fooObjects.Where(o => o.Type.Module.AppDomain.Name.Contains("AppDomains")).Single();
            ClrObject nestedFoo = fooObjects.Where(o => o.Type.Module.AppDomain.Name.Contains("Second")).Single();

            Assert.NotSame(appDomainsFoo.Type, nestedFoo.Type);

            ulong nestedExceptionFooMethodTable = dt.DataReader.ReadPointerUnsafe(nestedFoo.Address);
            ulong appDomainsFooMethodTable = dt.DataReader.ReadPointerUnsafe(appDomainsFoo.Address);

            // These are in different domains and should have different type handles:
            Assert.NotEqual(nestedExceptionFooMethodTable, appDomainsFooMethodTable);

            // The MethodTable returned by ClrType should always be the method table that lives in the "first"
            // AppDomain (in order of ClrAppDomain.Id).
            Assert.Equal(appDomainsFooMethodTable, fooType.TypeHandle);
            Assert.Equal(nestedExceptionFooMethodTable, fooType2.TypeHandle);
        }

        [Fact]
        public void FieldNameAndValueTests()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrType fooType = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
            ulong obj = (ulong)runtime.GetModule(ModuleName).GetTypeByName("Types").GetStaticFieldByName("s_foo").GetValue(runtime.AppDomains.Single());

            Assert.Same(fooType, heap.GetObjectType(obj));

            TestFieldNameAndValue(fooType, obj, "i", 42);
            TestFieldNameAndValue(fooType, obj, "s", "string");
            TestFieldNameAndValue(fooType, obj, "b", true);
            TestFieldNameAndValue(fooType, obj, "f", 4.2f);
            TestFieldNameAndValue(fooType, obj, "d", 8.4);
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

        [Fact]
        public void CollectibleTypeTest()
        {
            CollectibleAssemblyLoadContext context = new CollectibleAssemblyLoadContext();

            RuntimeHelpers.RunClassConstructor(context.LoadFromAssemblyPath(Assembly.GetExecutingAssembly().Location)
                .GetType(typeof(CollectibleUnmanagedStruct).FullName).TypeHandle);

            RuntimeHelpers.RunClassConstructor(Assembly.GetExecutingAssembly()
                .GetType(typeof(UncollectibleUnmanagedStruct).FullName).TypeHandle);

            using DataTarget dataTarget = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);

            ClrHeap heap = dataTarget.ClrVersions.Single().CreateRuntime().Heap;

            ClrType[] types = heap.EnumerateObjects().Select(obj => obj.Type).ToArray();

            ClrType collectibleType = types.Single(type => type?.Name == typeof(CollectibleUnmanagedStruct).FullName);

            Assert.False(collectibleType.ContainsPointers);
            Assert.True(collectibleType.IsCollectible);
            Assert.NotEqual(default, collectibleType.LoaderAllocatorHandle);
            ulong obj = dataTarget.DataReader.ReadPointerUnsafe(collectibleType.LoaderAllocatorHandle);
            Assert.Equal("System.Reflection.LoaderAllocator", heap.GetObjectType(obj).Name);

            ClrType uncollectibleType = types.Single(type => type?.Name == typeof(UncollectibleUnmanagedStruct).FullName);

            Assert.False(uncollectibleType.ContainsPointers);
            Assert.False(uncollectibleType.IsCollectible);
            Assert.Equal(default, uncollectibleType.LoaderAllocatorHandle);

            context.Unload();
        }

        private struct CollectibleUnmanagedStruct
        {
            public static CollectibleUnmanagedStruct Instance = default;
        }

        private struct UncollectibleUnmanagedStruct
        {
            public static UncollectibleUnmanagedStruct Instance = default;
        }

        private sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
        {
            public CollectibleAssemblyLoadContext() : base(true)
            {
            }
        }
    }

    public class ArrayTests
    {
        [Fact]
        public void ArrayOffsetsTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
            ulong s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
            ulong s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
            ulong s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

            ulong[] expected = { s_one, s_two, s_three };

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

        [Fact]
        public void ArrayLengthTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
            ClrType arrayType = heap.GetObjectType(s_array);

            ClrObject obj = new ClrObject(s_array, arrayType);
            Assert.Equal(3, obj.Length);
        }

        [Fact]
        public void ArrayReferenceEnumeration()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ulong s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
            ulong s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
            ulong s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
            ulong s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

            ClrType arrayType = heap.GetObjectType(s_array);

            List<ulong> objs = new List<ulong>();
            ClrObject obj = heap.GetObject(s_array);
            objs.AddRange(obj.EnumerateReferences().Select(o => o.Address));

            // We do not guarantee the order in which these are enumerated.
            Assert.Equal(3, objs.Count);
            Assert.Contains(s_one, objs);
            Assert.Contains(s_two, objs);
            Assert.Contains(s_three, objs);
        }
    }
}
