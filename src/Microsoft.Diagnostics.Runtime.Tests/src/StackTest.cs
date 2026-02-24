// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class StackTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StackTraceTests(bool singleFile)
        {
            string[] expectedFrames = new string[] { "Inner", "Inner", "Middle", "Outer", "Main" };

            using DataTarget dt = TestTargets.NestedException.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            string[] actualNames = runtime.GetMainThread().EnumerateStackTrace()
                .Where(f => f.Kind == ClrStackFrameKind.ManagedMethod)
                .Select(f => f.Method.Name)
                .ToArray();

            // Verify the expected frames appear in order (runtime may add helper frames like DispatchEx)
            int expectedIdx = 0;
            for (int i = 0; i < actualNames.Length && expectedIdx < expectedFrames.Length; i++)
            {
                if (actualNames[i].StartsWith(expectedFrames[expectedIdx]))
                    expectedIdx++;
            }

            Assert.True(expectedIdx == expectedFrames.Length,
                $"Expected to find frames [{string.Join(", ", expectedFrames)}] in order within stack [{string.Join(", ", actualNames)}]");
        }
    }
}
