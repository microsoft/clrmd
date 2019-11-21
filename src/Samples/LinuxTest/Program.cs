using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace LinuxTest
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Must specify a coredump to inspect.");
                return;
            }

            if (args.Length == 1)
            {
                Console.WriteLine("Must specify a dac to use.");
                return;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (DataTarget dt = DataTarget.LoadCoreDump(args[0]))
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime(args[1], true);
                ClrHeap heap = runtime.Heap;

                foreach (ClrObject obj in heap.EnumerateObjects())
                    Console.WriteLine($"{obj.Address:x12} {obj.Type?.Name ?? "error"}");
            }

            Console.WriteLine(sw.Elapsed);
        }
    }
}
