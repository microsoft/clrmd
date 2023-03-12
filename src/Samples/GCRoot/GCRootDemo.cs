// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime;

class Program
{
    public static void Main(string[] args)
    {
        ulong target = 0;

        if (args.Length != 2)
        {
            PrintUsage();
            Environment.Exit(1);
        }
        else if (!File.Exists(args[0]))
        {
            PrintUsage();
            Console.WriteLine($"File not found: '{args[0]}'.");
            Environment.Exit(1);
        }
        else if (!ulong.TryParse(args[1], System.Globalization.NumberStyles.HexNumber, null, out target))
        {
            PrintUsage();
            Console.WriteLine($"Could not parse object address '{args[1]}'.");
            Environment.Exit(1);
        }

        using DataTarget dt = DataTarget.LoadDump(args[0]);
        using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
        ClrHeap heap = runtime.Heap;

       /* At the most basic level, to implement !gcroot, simply create a GCRoot object.  Then call
        * EnumerateGCRoots to find unique paths from the root to the target object.  This runs similarly
        * to !gcroot.  Please note you will receive all paths each time this code is run, but the order
        * of roots from run to run are NOT guaranteed.  Note that all operations take a cancellation token
        * which you can use to cancel GCRoot at any time (it will throw an OperationCancelledException).
        */
        GCRoot gcroot = new GCRoot(heap);
        foreach (GCRootPath rootPath in gcroot.EnumerateGCRoots(target))
        {
            Console.Write($"{rootPath.Root} -> ");
            Console.WriteLine(string.Join(" -> ", rootPath.Path.Select(obj => obj.Address)));
        }

        Console.WriteLine();
        // ==========================================

        /* Since GCRoot can take a long time to run, ClrMD provides an event to get progress updates on how far
         * along GCRoot is.
         */

        gcroot.ProgressUpdated += (GCRoot source, int current) =>
        {
            Console.WriteLine($"Current root {source} objects inspected={current:n0}");
        };

        // ==========================================

       /* GCRoot can use parallel processing to use more of the CPU and work in parallel.  This is done
        * automatically (and is on by default)...but only if you use .BuildCache and build the *complete* GC heap
        * in local memory.  Since limitations in ClrMD and the APIs it's built on do not allow multithreading,
        * we only do multithreaded processing if we never need to call into ClrMD while walking objects.
        *
        * One downside is that multithreaded processing means the order of roots enumerated is not stable from
        * run to run.  You can turn off multithreaded processing so that the results of GCRoot are stable between
        * runs (though slower to run), but GCRoot makes NO guarantees about the order of roots you receive, and
        * they may change from build to build of ClrMD or change depending on the other flags set on the GCRoot
        * object.
        */

        // This can be used to turn off multithreaded processing, this property is true by default:
        // gcroot.AllowParallelSearch = false;

        // ==========================================

        /* Finally, we will run through the enumeration one more time, but this time using parallel processing
         * and printing out a bunch of status messages for demonstration.
         */

        Console.WriteLine("Get ready for a lot of output, since we are raw printing status updates!");
        Console.WriteLine();
        foreach (GCRootPath rootPath in gcroot.EnumerateGCRoots(target, false, Environment.ProcessorCount))
        {
            Console.Write($"{rootPath.Root} -> ");
            Console.WriteLine(string.Join(" -> ", rootPath.Path.Select(obj => obj.Address)));
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage:   gcrootdemo.exe [dumpfile] [target_object]");
        Console.WriteLine(@"Example: gcrootdemo.exe c:\path\to\crash.dmp 8dfea0");
    }
}
