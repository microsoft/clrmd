using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine($"Usage: dumpheap.exe [crashdump]");
            Environment.Exit(1);
        }

        Dictionary<ulong, (int Count, ulong Size, string Name)> stats = new Dictionary<ulong, (int Count, ulong Size, string Name)>();

        using DataTarget dataTarget = DataTarget.LoadDump(args[0]);
        foreach (ClrInfo clr in dataTarget.ClrVersions)
        {
            using ClrRuntime runtime = clr.CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Console.WriteLine("{0,12} {0,12} {1,8} {2}", "Object", "MethodTable", "Size", "Type");
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                Console.WriteLine($"{obj.Address:x12} {obj.Type.MethodTable:x12} {obj.Size:x8} {obj.Type.Name}");

                if (stats.TryGetValue(obj.Type.MethodTable, out (int Count, ulong Size, string Name) item))
                    item = (0, 0, obj.Type.Name);

                stats[obj.Type.MethodTable] = (item.Count + 1, item.Size + obj.Size, item.Name);
            }

            Console.WriteLine("Stats:");

            var sorted = from i in stats
                         orderby i.Value.Size ascending
                         select new
                         {
                             i.Value.Name,
                             i.Value.Size,
                             i.Value.Count
                         };

            foreach (var item in sorted)
                Console.WriteLine($"{item.Size:x12} {item.Count:n0}\t{item.Name}");
        }
    }
}
