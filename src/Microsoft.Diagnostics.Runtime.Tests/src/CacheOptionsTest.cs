// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class CacheOptionsTest
    {
        [Fact]
        public void MethodCachingTest()
        {
            // Test that when we cache method names they are not re-read
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheMethods = true;

            // We want to make sure we are getting the same string because it was cached,
            // not because it was interned
            dt.CacheOptions.CacheMethodNames = StringCaching.Cache;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            ClrMethod method = type.GetMethod("Bar");
            Assert.NotEqual(0ul, method.MethodDesc);  // Sanity test

            ClrMethod method2 = type.GetMethod("Bar");
            Assert.Equal(method, method2);
            Assert.Same(method, method2);

            string signature1 = method.Signature;
            string signature2 = method2.Signature;

            Assert.NotNull(signature1);
            Assert.Equal(signature1, signature2);

            Assert.Equal(signature1, method.Signature);
            Assert.Same(signature1, method.Signature);
        }

        [Fact]
        public void NoMethodCachingTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheMethods = false;
            dt.CacheOptions.CacheMethodNames = StringCaching.None;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            ClrMethod method = type.GetMethod("Bar");
            Assert.NotEqual(0ul, method.MethodDesc);  // Sanity test

            ClrMethod method2 = type.GetMethod("Bar");
            Assert.Equal(method, method2);
            Assert.NotSame(method, method2);

            string signature1 = method.Signature;
            string signature2 = method2.Signature;

            Assert.NotNull(signature1);
            Assert.Equal(signature1, signature2);

            Assert.Equal(signature1, method.Signature);
            Assert.NotSame(signature1, method.Signature);
            Assert.NotSame(method2.Signature, method.Signature);

            // Ensure that we can swap this at runtime and that we get interned strings
            dt.CacheOptions.CacheMethodNames = StringCaching.Intern;

            Assert.NotNull(method.Signature);
            Assert.Same(method2.Signature, method.Signature);
            Assert.Same(method.Signature, string.Intern(method.Signature));
        }

        [Fact]
        public void TypeCachingTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheTypes = true;
            dt.CacheOptions.CacheTypeNames = StringCaching.Cache;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            Assert.NotEqual(0ul, type.MethodTable);  // Sanity test

            Assert.Equal("Foo", type.Name);
            Assert.NotSame("Foo", type.Name);
            Assert.Same(type.Name, type.Name);
        }

        [Fact]
        public void NoTypeCachingTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheTypes = false;
            dt.CacheOptions.CacheTypeNames = StringCaching.None;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            Assert.NotEqual(0ul, type.MethodTable);  // Sanity test

            ClrType type2 = runtime.GetTypeByMethodTable(type.MethodTable);
            Assert.Equal(type, type2);
            Assert.NotSame(type, type2);

            Assert.NotNull(type.Name);
            Assert.Equal(type.Name, type.Name);
            Assert.NotSame(type.Name, type.Name);

            dt.CacheOptions.CacheTypeNames = StringCaching.Intern;
            Assert.Same(type.Name, type.Name);
            Assert.Same(type.Name, string.Intern(type.Name));
        }

        [Fact]
        public void FieldCachingTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheFields = true;
            dt.CacheOptions.CacheFieldNames = StringCaching.Cache;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            Assert.NotEqual(0ul, type.MethodTable);  // Sanity test

            Assert.Equal(type.Fields, type.Fields);
            Assert.Equal(type.StaticFields, type.StaticFields);

            ClrField field = type.GetInstanceFieldByName("o");
            ClrField field2 = type.Fields.Single(f => f.Name == "o");

            Assert.Same(field, field2);
            Assert.Same(field.Name, field.Name);
        }

        [Fact]
        public void NoFieldCachingTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            dt.CacheOptions.CacheFields = false;
            dt.CacheOptions.CacheFieldNames = StringCaching.None;

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");
            Assert.NotEqual(0ul, type.MethodTable);  // Sanity test

            Assert.NotEqual(type.Fields, type.Fields);

            ClrField field = type.GetInstanceFieldByName("o");
            ClrField field2 = type.Fields.Single(f => f.Name == "o");

            Assert.NotSame(field, field2);
            Assert.NotSame(field.Name, field.Name);

            dt.CacheOptions.CacheFieldNames = StringCaching.Intern;

            Assert.Same(field.Name, field.Name);
            Assert.Same(field.Name, string.Intern(field.Name));
        }
    }
}
