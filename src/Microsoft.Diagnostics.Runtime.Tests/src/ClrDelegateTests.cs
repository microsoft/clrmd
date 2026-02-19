// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ClrDelegateTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsDelegateTest(bool singleFile)
        {

            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType type = typesModule.GetTypeByName("Types");

            ClrObject TestDelegate = type.GetStaticFieldByName("TestDelegate").ReadObject(runtime.AppDomains.Single());
            Assert.True(TestDelegate.IsValid);
            Assert.True(TestDelegate.IsDelegate);
            Assert.False(TestDelegate.AsDelegate().HasMultipleTargets);

            ClrObject TestEvent = type.GetStaticFieldByName("TestEvent").ReadObject(runtime.AppDomains.Single());
            Assert.True(TestEvent.IsValid);
            Assert.True(TestEvent.IsDelegate);
            Assert.True(TestEvent.AsDelegate().HasMultipleTargets);
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetDelegateTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType Types = typesModule.GetTypeByName("Types");

            ClrDelegate TestDelegate = Types.GetStaticFieldByName("TestDelegate").ReadObject(runtime.AppDomains.Single()).AsDelegate();

            ClrDelegateTarget delegateTarget = TestDelegate.GetDelegateTarget();
            Assert.NotNull(delegateTarget);
            CompareToInner(Types, TestDelegate, delegateTarget);

            ClrDelegate TestEvent = Types.GetStaticFieldByName("TestEvent").ReadObject(runtime.AppDomains.Single()).AsDelegate();
            ClrDelegateTarget eventTarget = TestEvent.GetDelegateTarget();

            // In newer runtimes, GetDelegateTarget may return a target for multicast delegates
            // (resolving to the dispatch stub). Use EnumerateDelegateTargets instead.
            if (eventTarget is not null)
            {
                // The resolved method may not have meaningful metadata
                Assert.True(eventTarget.TargetObject.IsValid);
            }
        }

        private static void CompareToInner(ClrType Types, ClrDelegate del, ClrDelegateTarget delegateTarget)
        {
            Assert.True(del.Object.IsDelegate); // This delegate targets a static method

            Assert.NotNull(delegateTarget.Method);
            Assert.Equal(Types, delegateTarget.Method.Type);
            Assert.Equal("Inner", delegateTarget.Method.Name);
        }

        private static void CompareToInstanceMethod(ClrType Types, ClrDelegate del, ClrDelegateTarget delegateTarget)
        {
            Assert.NotEqual(del.Object.Address, delegateTarget.TargetObject.Address); // This delegate is an instance method of "Types"
            Assert.Equal(Types, delegateTarget.TargetObject.Type);

            Assert.NotNull(delegateTarget.Method);
            Assert.Equal(Types, delegateTarget.Method.Type);
            Assert.Equal("InstanceMethod", delegateTarget.Method.Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateDelegateTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrModule typesModule = runtime.GetModule(TypeTests.ModuleName);
            ClrType Types = typesModule.GetTypeByName("Types");

            ClrDelegate TestDelegate = Types.GetStaticFieldByName("TestDelegate").ReadObject(runtime.AppDomains.Single()).AsDelegate();
            ClrDelegateTarget[] methods = TestDelegate.EnumerateDelegateTargets().ToArray();

            Assert.Single(methods);
            CompareToInner(Types, TestDelegate, methods[0]);


            ClrDelegate TestEvent = Types.GetStaticFieldByName("TestEvent").ReadObject(runtime.AppDomains.Single()).AsDelegate();
            methods = TestEvent.EnumerateDelegateTargets().ToArray();

            Assert.Equal(2, methods.Length);
            CompareToInner(Types, TestEvent, methods[0]);
            CompareToInstanceMethod(Types, TestEvent, methods[1]);
        }
    }
}
