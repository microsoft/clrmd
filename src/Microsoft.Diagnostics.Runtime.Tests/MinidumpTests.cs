using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    class StackTraceEntry
    {
        public ClrStackFrameType Kind { get; set; }
        public string ModuleString { get; set; }
        public string MethodName { get; set; }
    }

    [TestClass]
    public class MinidumpTests
    {
        [TestMethod]
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
                        Assert.AreEqual(0ul, frame.InstructionPointer);
                        Assert.AreNotEqual(0ul, frame.StackPointer);
                    }
                    else
                    {
                        Assert.AreNotEqual(0ul, frame.InstructionPointer);
                        Assert.AreNotEqual(0ul, frame.StackPointer);
                        Assert.IsNotNull(frame.Method);
                        Assert.IsNotNull(frame.Method.Type);
                        Assert.IsNotNull(frame.Method.Type.Module);
                        Assert.AreEqual(frames[i++], frame.Method.Name);
                    }
                }
            }
        }


        [TestMethod]
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
