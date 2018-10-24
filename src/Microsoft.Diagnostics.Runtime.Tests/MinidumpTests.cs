using Xunit;
using System;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    class StackTraceEntry
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

                string[] frames = IntPtr.Size == 8 ?
                    new string[] { "Inner", "Inner", "Middle", "Outer", "Main" } : 
                    new string[] { "Inner", "Middle", "Outer", "Main" };

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
