// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ExceptionTests
    {
        [Fact]
        public void ExceptionPropertyTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        internal static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Single(t => !t.IsFinalizer);
            ClrException ex = thread.CurrentException;
            Assert.NotNull(ex);

            ExceptionTestData testData = TestTargets.NestedExceptionData;
            Assert.Equal(testData.OuterExceptionMessage, ex.Message);
            Assert.Equal(testData.OuterExceptionType, ex.Type.Name);
            Assert.NotNull(ex.Inner);
        }
    }
}