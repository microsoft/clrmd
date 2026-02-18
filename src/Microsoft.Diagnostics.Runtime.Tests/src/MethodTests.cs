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

        /// <summary>
        /// Regression test for issue #1306: GetMethodByHandle should resolve all methods
        /// on a struct implementing an interface, including unboxing stubs and inherited
        /// methods that haven't been JIT-compiled.
        /// </summary>
        [Fact]
        public void GetMethodByHandle_StructImplementingInterface_FindsAllMethods()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("StructWithInterface");

            Assert.NotNull(type);
            Assert.True(type.IsValueType);
            Assert.NotEmpty(type.Methods);

            foreach (ClrMethod method in type.Methods)
            {
                Assert.NotEqual(0ul, method.MethodDesc);
                ClrMethod found = runtime.GetMethodByHandle(method.MethodDesc);
                Assert.NotNull(found);
                Assert.Equal(method.Name, found.Name);
            }
        }

        /// <summary>
        /// Regression test for issue #1306: Struct methods without native code (unboxing stubs,
        /// un-jitted inherited methods) should return CompilationType.None, while JIT-compiled
        /// methods should return a non-None compilation type.
        /// </summary>
        [Fact]
        public void StructMethodCompilationType_MatchesJitStatus()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("StructWithInterface");

            Assert.NotNull(type);

            bool hasJittedMethod = false;
            bool hasUnjittedMethod = false;

            foreach (ClrMethod method in type.Methods)
            {
                ClrMethod found = runtime.GetMethodByHandle(method.MethodDesc);
                Assert.NotNull(found);

                if (found.CompilationType != MethodCompilationType.None)
                {
                    hasJittedMethod = true;
                }
                else
                {
                    hasUnjittedMethod = true;
                }
            }

            Assert.True(hasJittedMethod, "Expected at least one JIT-compiled method on StructWithInterface");
            Assert.True(hasUnjittedMethod, "Expected at least one un-jitted method on StructWithInterface");
        }

        /// <summary>
        /// Regression test for issue #935 sub-issue 3: GetMethodByHandle must return correct
        /// CompilationType and HotSize for methods on reference-type generic type instantiations.
        /// These share JIT'd code via canonical (__Canon) method descs. On .NET Framework the
        /// per-instantiation MethodDesc may have HasNativeCode=0 even though the method has been
        /// JIT'd (shared code), which requires the slot-based fallback in DacMethodLocator.
        /// On .NET Core 10+, the runtime shares the canonical MethodDesc itself across
        /// ref-type instantiations, so HasNativeCode is already set. This test validates the
        /// overall behavior on both platforms.
        /// </summary>
        [Fact]
        public void GetMethodByHandle_GenericMethodWithRefType_ReturnsJittedInfo()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            // The test target stored the per-instantiation MethodDesc for
            // GenericClass<bool,int,float,string,object>.Invoke in a static field.
            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType gsmType = module.GetTypeByName("GenericStaticMethod");
            Assert.NotNull(gsmType);

            ClrStaticField handleField = gsmType.GetStaticFieldByName("GenericClassInvokeMethodHandle");
            Assert.NotNull(handleField);

            ulong methodDesc = (ulong)handleField.Read<nint>(runtime.AppDomains[0]);
            Assert.NotEqual(0ul, methodDesc);

            ClrMethod found = runtime.GetMethodByHandle(methodDesc);
            Assert.NotNull(found);
            Assert.Contains("Invoke", found.Signature);
            Assert.NotEqual(MethodCompilationType.None, found.CompilationType);
            Assert.NotEqual(0u, found.HotColdInfo.HotSize);
        }

        /// <summary>
        /// Validates that GetMethodByHandle correctly resolves all methods on reference-type
        /// generic instantiations found via ClrType.Methods enumeration. On .NET Core, the
        /// canonical MethodDesc is shared across all reference-type instantiations, and all
        /// JIT'd methods should resolve with correct CompilationType and HotSize.
        /// </summary>
        [Fact]
        public void GetMethodByHandle_RefTypeGenericInstantiation_ResolvesAllMethods()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            // Find RefGenericClass<string> or RefGenericClass<object> from the heap
            ClrType refGenericType = null;
            foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
            {
                if (obj.Type?.Name?.Contains("RefGenericClass") == true)
                {
                    refGenericType = obj.Type;
                    break;
                }
            }

            Assert.NotNull(refGenericType);
            Assert.NotEmpty(refGenericType.Methods);

            bool hasGetValue = false;
            foreach (ClrMethod method in refGenericType.Methods)
            {
                Assert.NotEqual(0ul, method.MethodDesc);
                ClrMethod found = runtime.GetMethodByHandle(method.MethodDesc);
                Assert.NotNull(found);
                Assert.Equal(method.Name, found.Name);

                if (method.Name == "GetValue")
                {
                    hasGetValue = true;
                    Assert.NotEqual(MethodCompilationType.None, found.CompilationType);
                    Assert.NotEqual(0u, found.HotColdInfo.HotSize);
                }
            }

            Assert.True(hasGetValue, "Expected to find GetValue method on RefGenericClass");
        }

        /// <summary>
        /// Regression test for the exact scenario in issue #935: a generic method on a
        /// non-generic class (e.g., C.M&lt;int&gt;()) should report correct CompilationType
        /// and HotSize when called via GetMethodByHandle with a value-type instantiation.
        /// The old code used slot-based lookup which returned the generic method definition
        /// (CompilationType=None, HotSize=0) instead of the JIT'd instantiation.
        /// </summary>
        [Fact]
        public void GetMethodByHandle_ValueTypeGenericMethod_ReturnsJittedInfo()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType gsmType = module.GetTypeByName("GenericStaticMethod");
            Assert.NotNull(gsmType);

            // Echo<int> — value-type generic method instantiation (issue #935 exact scenario)
            ClrStaticField handleField = gsmType.GetStaticFieldByName("EchoIntMethodHandle");
            Assert.NotNull(handleField);

            ulong methodDesc = (ulong)handleField.Read<nint>(runtime.AppDomains[0]);
            Assert.NotEqual(0ul, methodDesc);

            ClrMethod found = runtime.GetMethodByHandle(methodDesc);
            Assert.NotNull(found);
            Assert.NotEqual(MethodCompilationType.None, found.CompilationType);
            Assert.NotEqual(0u, found.HotColdInfo.HotSize);
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
