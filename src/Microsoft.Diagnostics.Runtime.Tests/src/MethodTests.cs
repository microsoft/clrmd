// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MethodTests
    {
        [FrameworkFact(Skip = "GetMethodByHandle does not reliably resolve MethodDescs across sessions with .NET 10 DAC.")]
        public void MethodHandleMultiDomainTests()
        {
            ulong[] methodDescs;
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrType[] types = runtime.EnumerateModules().Where(m => m.Name.EndsWith("sharedlibrary.dll", System.StringComparison.OrdinalIgnoreCase)).Select(m => m.GetTypeByName("Foo")).Where(t => t != null).ToArray();

                Assert.Equal(2, types.Length);
                methodDescs = types.Select(t => t.Methods.Single(m => m.Name == "Bar")).Select(m => m.MethodDesc).ToArray();

                Assert.Equal(2, methodDescs.Length);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[0]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[1]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }
        }

        [Fact]
        public void MethodHandleSingleDomainTests()
        {
            ulong methodDesc;
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("types.dll");
                ClrType type = module.GetTypeByName("Types");
                ClrMethod method = type.GetMethod("Inner");
                methodDesc = method.MethodDesc;

                Assert.NotEqual(0ul, methodDesc);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDesc);

                Assert.NotNull(method);
                Assert.Equal("Inner", method.Name);
                Assert.Equal("Types", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("types.dll");
                ClrType type = module.GetTypeByName("Types");
                ClrMethod method = type.GetMethod("Inner");
                Assert.Equal(methodDesc, method.MethodDesc);
                Assert.Equal(method, runtime.GetMethodByHandle(methodDesc));
            }
        }

        [FrameworkFact]
        public void CompleteSignatureIsRetrievedForMethodsWithGenericParameters()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");

            ClrMethod[] methods = type.Methods.ToArray();
            ClrMethod genericMethod = type.GetMethod("GenericBar");

            string methodName = genericMethod.Signature;

            Assert.Equal(')', methodName.Last());
        }

        [Fact]
        public void ExplicitInterfaceMethodNameTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("ExplicitImpl");
            Assert.NotNull(type);

            ClrMethod method = type.Methods.Single(m => m.Signature is not null && m.Signature.Contains("ExplicitMethod"));
            Assert.Equal("IExplicitTest.ExplicitMethod", method.Name);
        }

        [Fact]
        public void RegularMethodNameIsUnchanged()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("ExplicitImpl");
            Assert.NotNull(type);

            ClrMethod method = type.GetMethod("RegularMethod");
            Assert.Equal("RegularMethod", method.Name);
        }

        [Fact]
        public void ConstructorMethodNameTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("Types");
            Assert.NotNull(type);

            ClrMethod ctor = type.Methods.Single(m => m.Signature is not null && m.Signature.Contains(".ctor"));
            Assert.Equal(".ctor", ctor.Name);
        }

        [WindowsFact]
        public void AssemblySize()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");

            ClrMethod[] methods = type.Methods.ToArray();
            ClrMethod genericMethod = type.GetMethod("GenericBar");

            Assert.NotEqual<uint>(0, genericMethod.HotColdInfo.ColdSize + genericMethod.HotColdInfo.HotSize);
        }
    }
}
