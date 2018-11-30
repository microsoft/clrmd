// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal class StackTraceEntry
    {
        public ClrStackFrameType Kind { get; set; }
        public string ModuleString { get; set; }
        public string MethodName { get; set; }
    }

    public class MinidumpTests
    {
        [Fact]
        public void MinidumpCallstackTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadMiniDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrThread thread = runtime.GetMainThread();

                string[] frames = IntPtr.Size == 8 ? new[] {"Inner", "Inner", "Middle", "Outer", "Main"} : new[] {"Inner", "Middle", "Outer", "Main"};

                int i = 0;

                foreach (ClrStackFrame frame in thread.StackTrace)
                {
                    if (frame.Kind == ClrStackFrameType.Runtime)
                    {
                        Assert.Equal(0ul, frame.InstructionPointer);
                        Assert.NotEqual(0ul, frame.StackPointer);
                    }
                    else
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
        }

        [Fact]
        public void MinidumpExceptionPropertiesTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadMiniDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ExceptionTests.TestProperties(runtime);
            }
        }
    }
}