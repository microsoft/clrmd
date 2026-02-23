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

            string[] frames = IntPtr.Size == 8 ? new[] { "Inner", "Inner", "Middle", "Outer", "Main" } : new[] { "Inner", "Middle", "Outer", "Main" };

            int i = 0;

            foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
            {
                if (frame.Kind == ClrStackFrameKind.ManagedMethod)
                {
                    Assert.NotEqual(0ul, frame.InstructionPointer);
                    Assert.NotEqual(0ul, frame.StackPointer);
                    Assert.NotNull(frame.Method);
                    Assert.NotNull(frame.Method.Type);
                    Assert.NotNull(frame.Method.Type.Module);
                    Assert.Equal(frames[i++], frame.Method.Name);
                }
            }
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
