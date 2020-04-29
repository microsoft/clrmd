// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ExceptionTests
    {
        [Fact]
        public void ExceptionPropertyTest()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            TestProperties(runtime);
        }

        internal static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.GetMainThread();
            ClrException? exNullable = thread.CurrentException;
            Assert.NotNull(exNullable);

            ClrException ex = exNullable.GetValueOrDefault();

            ExceptionTestData testData = TestTargets.NestedExceptionData;
            Assert.Equal(testData.OuterExceptionMessage, ex.Message);
            if (ex.Type.Name != null)
                Assert.Equal(testData.OuterExceptionType, ex.Type.Name);
            Assert.NotNull(ex.Inner);
        }

        [Fact]
        public void TestStackTrace()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrThread thread = runtime.GetMainThread();
            ClrException? exceptionNullable = thread.CurrentException;
            Assert.NotNull(exceptionNullable);

            ClrException exception = exceptionNullable.GetValueOrDefault();
            ImmutableArray<ClrStackFrame> stackTrace = exception.StackTrace;
            foreach (ClrStackFrame stackFrame in stackTrace)
            {
                Assert.Equal(stackFrame.Thread, thread);
            }
        }
    }
}
