// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal sealed class StackTraceEntry
    {
        public ClrStackFrameKind Kind { get; set; }
        public string ModuleString { get; set; }
        public string MethodName { get; set; }
    }

    public class MinidumpTests
    {

        [Theory]
        [InlineData(false)]
        public void MinidumpProcessIdTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadMinidump(singleFile);
            Assert.True(dt.DataReader.ProcessId > 0);
        }

        [Theory]
        [InlineData(false)]
        // Single-file apps only support full dumps (runtime limitation).
        public void MinidumpCallstackTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadMinidump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrThread thread = runtime.GetMainThread();

            string[] expectedFrames = IntPtr.Size == 8 ? new[] { "Inner", "Inner", "Middle", "Outer", "Main" } : new[] { "Inner", "Middle", "Outer", "Main" };

            var managedFrames = thread.EnumerateStackTrace()
                .Where(f => f.Kind == ClrStackFrameKind.ManagedMethod)
                .ToArray();

            foreach (ClrStackFrame frame in managedFrames)
            {
                Assert.NotEqual(0ul, frame.InstructionPointer);
                Assert.NotEqual(0ul, frame.StackPointer);
                Assert.NotNull(frame.Method);
                Assert.NotNull(frame.Method.Type);
                Assert.NotNull(frame.Method.Type.Module);
            }

            // Verify expected frames appear in order (runtime may add helper frames like DispatchEx)
            int expectedIdx = 0;
            for (int i = 0; i < managedFrames.Length && expectedIdx < expectedFrames.Length; i++)
            {
                if (managedFrames[i].Method.Name == expectedFrames[expectedIdx])
                    expectedIdx++;
            }

            Assert.True(expectedIdx == expectedFrames.Length,
                $"Expected to find frames [{string.Join(", ", expectedFrames)}] in order within stack [{string.Join(", ", managedFrames.Select(f => f.Method.Name))}]");
        }

        [Theory]
        [InlineData(false)]
        // Single-file apps only support full dumps (runtime limitation).
        public void MinidumpExceptionPropertiesTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadMinidump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ExceptionTests.TestProperties(runtime);
        }



        [Theory]
        [InlineData(false)]
        // Single-file apps only support full dumps (runtime limitation).
        public void MinidumpExceptionPropertiesNoSymbolsTest(bool singleFile)
        {
            var options = new DataTargetOptions()
            {
                FileLocator = new NullBinaryLocator()
            };

            using DataTarget dt = TestTargets.NestedException.LoadMinidump(singleFile, options);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ExceptionTests.TestProperties(runtime);
        }
    }
}
