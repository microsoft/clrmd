// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class StackTest
    {
        [Fact]
        public void StackTraceTests()
        {
            string[] frames = new string[] { "Inner", "Inner", "Middle", "Outer", "Main" };

            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrStackFrame[] items = runtime.GetMainThread().EnumerateStackTrace().Where(f => f.Kind == ClrStackFrameKind.ManagedMethod).ToArray();

            Assert.Equal(frames.Length, items.Length);
            for (int i = 0; i < frames.Length; i++)
                Assert.StartsWith(frames[i], items[i].Method.Name);
        }
    }
}
