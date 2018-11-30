// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MethodTests
    {
        [Fact]
        public void MethodHandleMultiDomainTests()
        {
            ulong[] methodDescs;
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var module = runtime.GetModule("sharedlibrary.dll");
                var type = module.GetTypeByName("Foo");
                var method = type.GetMethod("Bar");
                methodDescs = method.EnumerateMethodDescs().ToArray();

                Assert.Equal(2, methodDescs.Length);
            }

            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var method = runtime.GetMethodByHandle(methodDescs[0]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }

            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var method = runtime.GetMethodByHandle(methodDescs[1]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }
        }

        [Fact]
        public void MethodHandleSingleDomainTests()
        {
            ulong methodDesc;
            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var module = runtime.GetModule("sharedlibrary.dll");
                var type = module.GetTypeByName("Foo");
                var method = type.GetMethod("Bar");
                methodDesc = method.EnumerateMethodDescs().Single();

                Assert.NotEqual(0ul, methodDesc);
            }

            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var method = runtime.GetMethodByHandle(methodDesc);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }

            using (var dt = TestTargets.Types.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var module = runtime.GetModule("sharedlibrary.dll");
                var type = module.GetTypeByName("Foo");
                var method = type.GetMethod("Bar");
                Assert.Equal(methodDesc, method.EnumerateMethodDescs().Single());
            }
        }

        /// <summary>
        /// This test tests a patch in v45runtime.GetNameForMD(ulong md) that
        /// corrects an error from sos
        /// </summary>
        [Fact]
        public void CompleteSignatureIsRetrievedForMethodsWithGenericParameters()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var module = runtime.GetModule("sharedlibrary.dll");
                var type = module.GetTypeByName("Foo");

                var genericMethod = type.GetMethod("GenericBar");

                var methodName = genericMethod.GetFullSignature();

                Assert.Equal(')', methodName.Last());
            }
        }
    }
}