// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Principal;
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
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrModule module = runtime.GetModule(ModuleName);
            ClrType typesType = module.GetTypeByName("Types");
            ClrStaticField field = typesType.GetStaticFieldByName("s_i");

            ClrObject obj = field.ReadObject();
            Assert.False(obj.IsNull);

            ClrType type = obj.Type;
            Assert.NotNull(type);

            Assert.True(type.IsPrimitive);
            Assert.False(type.IsObjectReference);
            Assert.False(type.IsValueClass);

            var fds = obj.Type.Fields;

            int value = obj.ReadBoxed<int>();
            Assert.Equal(42, value);

            Assert.Contains(obj.Address, heap.EnumerateObjects().Select(a => a.Address));
        }

        [FrameworkFact]
        public void ArrayComponentTypeTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
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
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
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

        [Fact]
        public void AsEnumTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrType appDomain = runtime.Heap.GetObjectsOfType("System.AppDomain").First().Type;
            ClrInstanceField field = appDomain.Fields.Single(f => f.Name == "_PrincipalPolicy");
            Assert.True(field.Type.IsEnum);

            ClrEnum clrEnum = field.Type.AsEnum();
            Assert.NotNull(clrEnum);

            string[] propertyNames = clrEnum.GetEnumNames().ToArray();
            Assert.NotEmpty(propertyNames);
            Assert.Contains("NoPrincipal", propertyNames);
            Assert.Contains("UnauthenticatedPrincipal", propertyNames);
            Assert.Contains("WindowsPrincipal", propertyNames);

            Assert.Equal(ClrElementType.Int32, clrEnum.ElementType);

            Assert.Equal(PrincipalPolicy.NoPrincipal, clrEnum.GetEnumValue<PrincipalPolicy>(nameof(PrincipalPolicy.NoPrincipal)));
            Assert.Equal(PrincipalPolicy.UnauthenticatedPrincipal, clrEnum.GetEnumValue<PrincipalPolicy>(nameof(PrincipalPolicy.UnauthenticatedPrincipal)));
            Assert.Equal(PrincipalPolicy.WindowsPrincipal, clrEnum.GetEnumValue<PrincipalPolicy>(nameof(PrincipalPolicy.WindowsPrincipal)));
        }

        [Fact]
        public void AirtyTest()
        {
            // https://github.com/microsoft/clrmd/issues/394
            string name = ClrmdType.FixGenerics("Microsoft.Diagnostics.Runtime.Tests.TypeTests+GenericTest`2[[System.String, System.Private.CoreLib],[System.Collections.Generic.List`1[[System.Collections.Generic.IEnumerable`1[[System.Int32, System.Private.CoreLib]][,], System.Private.CoreLib]][], System.Private.CoreLib]]");
            const string expected = "Microsoft.Diagnostics.Runtime.Tests.TypeTests+GenericTest<System.String, System.Collections.Generic.List<System.Collections.Generic.IEnumerable<System.Int32>[,]>[]>";

            Assert.Equal(expected, name);

            name = ClrmdType.FixGenerics("Microsoft.Diagnostics.Runtime.Tests.TypeTests+GenericTest[[System.String, System.Private.CoreLib],[System.Collections.Generic.List[[System.Collections.Generic.IEnumerable[[System.Int32, System.Private.CoreLib]][,], System.Private.CoreLib]][], System.Private.CoreLib]]");

            Assert.Equal(expected, name);

            Assert.Equal("MyAssembly.Test<System.String>", ClrmdType.FixGenerics("MyAssembly.Test`1[[System.String, mscorlib]]"));
            Assert.Equal("MyAssembly.Test<System.String>", ClrmdType.FixGenerics("MyAssembly.Test[[System.String, mscorlib]]"));
        }

        [FrameworkFact]
        public void TypeEqualityTest()
        {
            // This test ensures that only one ClrType is created when we have a type loaded into two different AppDomains with two different
            // method tables.

            const string TypeName = "Foo";
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrType[] types = (from obj in heap.EnumerateObjects()
                               let t = heap.GetObjectType(obj.Address)
                               where t.Name == TypeName
                               orderby t.MethodTable
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
            Assert.NotEqual(types[0], types[1]);

            if (dt.CacheOptions.CacheTypes)
            {
                Assert.Same(types[0], typesFromModule[0]);
                Assert.Same(types[1], typesFromModule[1]);
            }
            else
            {
                Assert.Equal(types[0], typesFromModule[0]);
                Assert.Equal(types[1], typesFromModule[1]);
            }

            // Get new types
            runtime.FlushCachedData();


            ClrType[] newTypes = (from module in runtime.EnumerateModules()
                                  let name = Path.GetFileNameWithoutExtension(module.FileName)
                                  where name.Equals("sharedlibrary", StringComparison.OrdinalIgnoreCase)
                                  let type = module.GetTypeByName(TypeName)
                                  select type).ToArray();

            Assert.Equal(2, newTypes.Length);
            for (int i = 0; i < newTypes.Length; i++)
            {
                Assert.NotSame(typesFromModule[i], newTypes[i]);
                Assert.Equal(typesFromModule[i], newTypes[i]);
            }

            // Even though these are the same underlying type defined in sharedlibrary's metadata,
            // they have different MethodTables, Parent modules, and parent domains.  These do not
            // compare as equal.
            Assert.NotEqual(typesFromModule[0], typesFromModule[1]);
        }

        [WindowsFact]
        public void VariableRootTest()
        {
            // Test to make sure that a specific static and local variable exist.

            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
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
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrType type in heap.EnumerateObjects().Select(obj => heap.GetObjectType(obj.Address)).Unique())
            {
                Assert.NotEqual(0ul, type.MethodTable);

                ClrType typeFromHeap;

                if (type.IsArray)
                {
                    ClrType componentType = type.ComponentType;
                    Assert.NotNull(componentType);

                    typeFromHeap = runtime.GetTypeByMethodTable(type.MethodTable);
                }
                else
                {
                    typeFromHeap = runtime.GetTypeByMethodTable(type.MethodTable);
                }

                Assert.Equal(type.MethodTable, typeFromHeap.MethodTable);

                if (dt.CacheOptions.CacheTypes)
                    Assert.Same(type, typeFromHeap);
                else
                    Assert.Equal(type, typeFromHeap);
            }
        }

        [FrameworkFact]
        public void GetObjectMethodTableTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int i = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                i++;
                ClrType type = obj.Type;
                Assert.NotNull(type);

                ulong mt = dt.DataReader.ReadPointerUnsafe(obj);
                Assert.NotEqual(0ul, mt);

                if (dt.CacheOptions.CacheTypes)
                    Assert.Same(type, runtime.GetTypeByMethodTable(mt));
                else
                    Assert.Equal(type, runtime.GetTypeByMethodTable(mt));

                Assert.Equal(mt, type.MethodTable);
            }
        }

        [FrameworkFact]
        public void EnumerateMethodTableTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
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
            Assert.Equal(appDomainsFooMethodTable, fooType.MethodTable);
            Assert.Equal(nestedExceptionFooMethodTable, fooType2.MethodTable);
        }

        [Fact]
        public void PrimitiveTypeEquality()
        {
            // Make sure ClrmdPrimitiveType always equals "real" ClrmdTypes if their ElementTypes are equal.
            // ClrmdPrimitiveType are fake, mocked up types we create if we aren't able to create the real
            // ClrType for a field.

            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheTypes = false;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            foreach ((ulong mt, uint _) in runtime.BaseClassLibrary.EnumerateTypeDefToMethodTableMap())
            {
                ClrType type = runtime.GetTypeByMethodTable(mt);
                if (type != null && type.IsPrimitive)
                {
                    // We are hoping that creating a type through a MT will result in a real ClrmdType and
                    // not a ClrmdPrimitiveType.  A ClrmdPrimitiveType is there to mock up a type we cannot
                    // find.
                    Assert.IsType<ClrmdType>(type);

                    ClrmdType ct = (ClrmdType)type;

                    ClrmdPrimitiveType prim = new ClrmdPrimitiveType((ITypeHelpers)type.ClrObjectHelpers, runtime.BaseClassLibrary, runtime.Heap, ct.ElementType);
                    Assert.True(ct == prim);
                    Assert.True(prim == ct);
                }
            }
        }


        [Fact]
        public void InnerStructSizeTest()
        {
            // https://github.com/microsoft/clrmd/issues/101
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule sharedLibrary = runtime.GetModule("sharedlibrary.dll");
            ClrType structTestClass = sharedLibrary.GetTypeByName("StructTestClass");
            ClrType structTest = sharedLibrary.GetTypeByName("Struct");
            Assert.NotNull(structTest);

            ClrInstanceField field = structTestClass.GetFieldByName("s");
            if (dt.CacheOptions.CacheTypes)
                Assert.Same(structTest, field.Type);
            else
                Assert.Equal(structTest, field.Type);

            Assert.Equal(sizeof(int), field.Size);

            ClrInstanceField nes = structTestClass.GetFieldByName("nes");
            Assert.Equal(0, nes.Size);

            ClrInstanceField es = nes.Type.GetFieldByName("es");
            Assert.Equal(0, es.Size);
        }

        [Fact(Skip = "This looks like a bug in mscordac and not ClrMD.")]
        public void StringEmptyIsObtainableTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrType stringType = heap.StringType;
            Assert.NotNull(stringType);

            ClrStaticField empty = stringType.GetStaticFieldByName("Empty");
            Assert.NotNull(empty);

            string value = empty.ReadString();
            Assert.Equal(string.Empty, value);
        }

        [Fact]
        public void ComponentTypeEventuallyFilledTest()
        {
            // https://github.com/microsoft/clrmd/issues/108
            // Ensure that a previously created type with a erronous null ComponentType eventually
            // gets its ComponentType set.

            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrType fooType = runtime.GetModule(ModuleName).GetTypeByName("Types");
            ClrStaticField cq = fooType.GetStaticFieldByName("s_cq");
            Assert.NotNull(cq);

            ClrInstanceField m_head = cq.Type.GetFieldByName("m_head");
            ClrInstanceField m_array = m_head.Type.GetFieldByName("m_array");
            ClrElementType elementType = m_array.ElementType;
            ClrType componentType = m_array.Type.ComponentType;

            // If this assert fails, remove the test.  This value is null because currently CLR's
            // debugging layer doesn't tell us the component type of an array.  If we eventually
            // fix that issue, we would return a non-null m_array.Type.ComponentType, causing
            // this test to fail but the underlying issue would be fixed.
            Assert.Null(componentType);

            ClrObject m_arrayObj = cq.ReadObject().GetObjectField("m_head").GetObjectField("m_array");

            // Ensure we are looking at the same ClrType
            if (dt.CacheOptions.CacheTypes)
                Assert.Same(m_array.Type, m_arrayObj.Type);
            else
                Assert.Equal(m_array.Type, m_arrayObj.Type);

            // Assert that we eventually filled in ComponentType after we got a real object for the type
            Assert.NotNull(m_arrayObj.Type.ComponentType);
        }

        [Fact]
        public void FieldNameAndValueTests()
        {
            // TODO: test reading structs from instance/static fields
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrType fooType = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
            ClrObject obj = runtime.GetModule(ModuleName).GetTypeByName("Types").GetStaticFieldByName("s_foo").ReadObject();

            if (dt.CacheOptions.CacheTypes)
            {
                Assert.Same(fooType, obj.Type);
                Assert.Same(fooType, heap.GetObjectType(obj.Address));
            }
            else
            {
                Assert.Equal(fooType, obj.Type);
                Assert.Equal(fooType, heap.GetObjectType(obj.Address));
            }

            TestFieldNameAndValue(fooType, obj, "i", 42);
            TestFieldNameAndValue(fooType, obj, "s", "string");
            TestFieldNameAndValue(fooType, obj, "b", true);
            TestFieldNameAndValue(fooType, obj, "f", 4.2f);
            TestFieldNameAndValue(fooType, obj, "d", 8.4);
        }

        public ClrInstanceField TestFieldNameAndValue(ClrType type, ulong obj, string name, string value)
        {
            ClrInstanceField field = type.GetFieldByName(name);
            Assert.NotNull(field);
            Assert.Equal(name, field.Name);

            string str = field.ReadString(obj, interior: false);
            Assert.Equal(value, str);

            return field;
        }

        public ClrInstanceField TestFieldNameAndValue<T>(ClrType type, ulong obj, string name, T value)
            where T : unmanaged
        {
            ClrInstanceField field = type.GetFieldByName(name);
            Assert.NotNull(field);
            Assert.Equal(name, field.Name);

            T t = field.Read<T>(obj, interior: false);
            Assert.Equal(value, t);

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

            ClrHeap heap = dataTarget.ClrVersions.Single(v => v.ModuleInfo.FileName.EndsWith("coreclr.dll", true, null)).CreateRuntime().Heap;

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
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ulong s_array = type.GetStaticFieldByName("s_array").ReadObject();
            ulong s_one = type.GetStaticFieldByName("s_one").ReadObject();
            ulong s_two = type.GetStaticFieldByName("s_two").ReadObject();
            ulong s_three = type.GetStaticFieldByName("s_three").ReadObject();

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
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ClrObject obj = type.GetStaticFieldByName("s_array").ReadObject();
            Assert.Equal(3, obj.Length);
        }

        [Fact]
        public void ArrayReferenceEnumeration()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrAppDomain domain = runtime.AppDomains.Single();

            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ulong s_array = type.GetStaticFieldByName("s_array").ReadObject();
            ulong s_one = type.GetStaticFieldByName("s_one").ReadObject();
            ulong s_two = type.GetStaticFieldByName("s_two").ReadObject();
            ulong s_three = type.GetStaticFieldByName("s_three").ReadObject();

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
