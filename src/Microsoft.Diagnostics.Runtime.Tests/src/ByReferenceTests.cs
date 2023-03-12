// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ByReferenceTests : IDisposable
    {
        private readonly DataTarget dataTarget;
        private readonly ClrRuntime runtime;

        public ByReferenceTests()
        {
            dataTarget = TestTargets.ByReference.LoadFullDump();
            runtime = dataTarget.ClrVersions.Single().CreateRuntime();
        }

        public void Dispose()
        {
            runtime.Dispose();
            dataTarget.Dispose();
        }

        private IEnumerable<ClrRoot> GetStackRoots(string methodName)
        {
            return runtime.Threads.Single(thread => thread.EnumerateStackTrace().Any(frame => frame.Method?.Name == methodName))
                .EnumerateStackRoots().Where(root => root.StackFrame.Method?.Name == methodName);
        }

        private void AssertReferenceType(IEnumerable<ClrRoot> stackRoots)
        {
            ClrRoot stackRoot = Assert.Single(stackRoots);
            Assert.True(stackRoot.IsInterior);
            Assert.True(stackRoot.Object.IsValid);
        }

        private void AssertValueType(IEnumerable<ClrRoot> stackRoots)
        {
            Assert.Empty(stackRoots);
        }

        [Fact]
        public void HeapReferenceType() => AssertReferenceType(GetStackRoots(nameof(HeapReferenceType)));

        [Fact]
        public void HeapValueType() => AssertValueType(GetStackRoots(nameof(HeapValueType)));

        [Fact]
        public void StackReferenceType() => AssertReferenceType(GetStackRoots(nameof(StackReferenceType)));

        [Fact]
        public void StackValueType() => AssertValueType(GetStackRoots(nameof(StackValueType)));
    }
}
