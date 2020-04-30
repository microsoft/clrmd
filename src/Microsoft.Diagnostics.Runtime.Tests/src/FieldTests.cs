// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class FieldTests
    {
        [Fact]
        public void TestInstanceFieldModifiers()
        {
            using DataTarget dt = TestTargets.NestedTypes.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule(TypeTests.NestedTypesModuleName);
            ClrType program = module.GetTypeByName("Program");

            ClrField publicField = program.GetInstanceFieldByName("publicField");
            Assert.True(publicField.IsPublic);

            ClrField privateField = program.GetInstanceFieldByName("privateField");
            Assert.True(privateField.IsPrivate);

            ClrField internalField = program.GetInstanceFieldByName("internalField");
            Assert.True(internalField.IsInternal);

            ClrField protectedField = program.GetInstanceFieldByName("protectedField");
            Assert.True(protectedField.IsProtected);
        }

        [Fact]
        public void TestStaticFieldModifiers()
        {
            using DataTarget dt = TestTargets.NestedTypes.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule(TypeTests.NestedTypesModuleName);
            ClrType program = module.GetTypeByName("Program");

            ClrStaticField publicField = program.GetStaticFieldByName("s_publicField");
            Assert.True(publicField.IsPublic);

            ClrStaticField privateField = program.GetStaticFieldByName("s_privateField");
            Assert.True(privateField.IsPrivate);

            ClrStaticField internalField = program.GetStaticFieldByName("s_internalField");
            Assert.True(internalField.IsInternal);

            ClrStaticField protectedField = program.GetStaticFieldByName("s_protectedField");
            Assert.True(protectedField.IsProtected);
        }

        [Fact]
        public void InstanceFieldProperties()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrType foo = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
            Assert.NotNull(foo);

            CheckField(foo, "i", ClrElementType.Int32, "System.Int32", 4);

            CheckField(foo, "s", ClrElementType.String, "System.String", IntPtr.Size);
            CheckField(foo, "b", ClrElementType.Boolean, "System.Boolean", 1);
            CheckField(foo, "f", ClrElementType.Float, "System.Single", 4);
            CheckField(foo, "d", ClrElementType.Double, "System.Double", 8);
            CheckField(foo, "o", ClrElementType.Object, "System.Object", IntPtr.Size);
        }

        private static void CheckField(ClrType type, string fieldName, ClrElementType element, string typeName, int size)
        {
            ClrInstanceField field = type.GetInstanceFieldByName(fieldName);
            Assert.NotNull(field);
            Assert.NotNull(field.Type);

            Assert.Equal(element, field.ElementType);
            Assert.Equal(typeName, field.Type.Name);
            Assert.Equal(size, field.Size);
        }
    }
}
