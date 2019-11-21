using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace DesktopTest
{
    class Program
    {
        static void Main()
        {
            using DataTarget dt = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, uint.MaxValue, AttachFlag.Passive);

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
