// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;

if (args.Length != 2)
{
    Console.WriteLine("Usage:   GCRoot [dumpfile] [target_object]");
    Console.WriteLine(@"Example: GCRoot c:\path\to\crash.dmp 8dfea0");
    return 1;
}

if (!File.Exists(args[0]))
{
    Console.WriteLine($"File not found: '{args[0]}'.");
    return 1;
}

if (!ulong.TryParse(args[1], System.Globalization.NumberStyles.HexNumber, null, out ulong target))
{
    Console.WriteLine($"Could not parse object address '{args[1]}'.");
    return 1;
}

using DataTarget dt = DataTarget.LoadDump(args[0]);
using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
ClrHeap heap = runtime.Heap;

// Create a GCRoot object with the target address we want to find roots for.
GCRoot gcroot = new(heap, [target]);

// EnumerateRootPaths finds unique paths from GC roots to the target object.
// Note that the order of roots is NOT guaranteed between runs.
foreach ((ClrRoot root, GCRoot.ChainLink path) in gcroot.EnumerateRootPaths())
{
    Console.Write($"{root} -> ");

    // Walk the chain of objects from the root to the target.
    GCRoot.ChainLink? link = path;
    while (link is not null)
    {
        Console.Write($"{link.Object:x}");
        link = link.Next;
        if (link is not null)
            Console.Write(" -> ");
    }

    Console.WriteLine();
}

return 0;
