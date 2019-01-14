using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (DataTarget dt = DataTarget.LoadCrashDump(@"D:\work\09_27_cloudtest\CloudTestAgent.DMP"))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                foreach (ClrThread thread in runtime.Threads)
                {
                    Console.WriteLine($"{thread.OSThreadId}");
                    foreach (ClrStackFrame frame in thread.StackTrace)
                    {
                        Console.WriteLine($"    {frame}");
                    }
                }
            }
        }
    }
}
