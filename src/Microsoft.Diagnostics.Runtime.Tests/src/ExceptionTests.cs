// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ExceptionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExceptionPropertyTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            TestProperties(runtime);
        }

        internal static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.GetMainThread();
            ClrException ex = thread.CurrentException;
            Assert.NotNull(ex);

            ExceptionTestData testData = TestTargets.NestedExceptionData;
            Assert.Equal(testData.OuterExceptionMessage, ex.Message);
            if (ex.Type?.Name != null)
                Assert.Equal(testData.OuterExceptionType, ex.Type?.Name);
            Assert.NotNull(ex.Inner);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestStackTrace(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrThread thread = runtime.GetMainThread();
            ClrException exception = thread.CurrentException;
            Assert.NotNull(exception);

            ImmutableArray<ClrStackFrame> stackTrace = exception.StackTrace;
            foreach (ClrStackFrame stackFrame in stackTrace)
            {
                Assert.Equal(stackFrame.Thread, thread);
            }
        }
    }
}
