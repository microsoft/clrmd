// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;

if (args.Length != 1)
{
    Console.WriteLine("Usage: DumpHeap [crashdump]");
    return 1;
}

Dictionary<ulong, (int Count, ulong Size, string Name)> stats = new();

using DataTarget dataTarget = DataTarget.LoadDump(args[0]);
foreach (ClrInfo clr in dataTarget.ClrVersions)
{
    using ClrRuntime runtime = clr.CreateRuntime();
    ClrHeap heap = runtime.Heap;

    Console.WriteLine("{0,16} {1,16} {2,8} {3}", "Object", "MethodTable", "Size", "Type");
    foreach (ClrObject obj in heap.EnumerateObjects())
    {
        Console.WriteLine($"{obj.Address:x16} {obj.Type?.MethodTable ?? 0:x16} {obj.Size,8:D} {obj.Type?.Name}");

        ulong mt = obj.Type?.MethodTable ?? 0;
        if (!stats.TryGetValue(mt, out (int Count, ulong Size, string Name) item))
            item = (0, 0, obj.Type?.Name ?? "<unknown>");

        stats[mt] = (item.Count + 1, item.Size + obj.Size, item.Name);
    }

    Console.WriteLine("\nStatistics:");
    var sorted = from i in stats
                 orderby i.Value.Size ascending
                 select new
                 {
                     i.Key,
                     i.Value.Name,
                     i.Value.Size,
                     i.Value.Count
                 };

    Console.WriteLine("{0,16} {1,12} {2,12}\t{3}", "MethodTable", "Count", "Size", "Type");
    foreach (var item in sorted)
        Console.WriteLine($"{item.Key:x16} {item.Count,12:D} {item.Size,12:D}\t{item.Name}");

    Console.WriteLine($"Total {sorted.Sum(x => x.Count):0} objects");
}

return 0;
