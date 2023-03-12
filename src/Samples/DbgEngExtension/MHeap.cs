// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;

namespace DbgEngExtension
{
    /// <summary>
    /// A simple version of !dumpheap to demonstrate creating a plugin.
    /// Note that
    /// </summary>
    public class MHeap : DbgEngCommand
    {
        /// <summary>
        /// Constructor.  It's assumed that this is the constructor used for a DbgEng plugin, since it
        /// will be handing us an IUnknown.  Therefore <paramref name="redirectConsoleOutput"/> defaults
        /// to true.
        /// </summary>
        /// <param name="pUnknown">The dbgeng instance we are interacting with.</param>
        /// <param name="redirectConsoleOutput">Whether to override Console.WriteLine and redirect it to DbgEng's output system.</param>
        public MHeap(nint pUnknown, bool redirectConsoleOutput = true)
            : base(pUnknown, redirectConsoleOutput)
        {
        }

        /// <summary>
        /// Constructor.  It's assumed that this is the constructor used for a standalone DbgEng app, since it
        /// will be handing us a dbgeng object.  Therefore <paramref name="redirectConsoleOutput"/> defaults
        /// to false (don't modify Console.WriteLine, just write to the standard console).
        /// </summary>
        /// <param name="pUnknown">The dbgeng instance we are interacting with.</param>
        /// <param name="redirectConsoleOutput">Whether to override Console.WriteLine and redirect it to DbgEng's output system.</param>
        public MHeap(IDisposable dbgeng, bool redirectConsoleOutput = false)
            : base(dbgeng, redirectConsoleOutput)
        {
        }

        // We don't want to expose the string parsing overload to other applications, they should call the actual
        // method with parameters.
        internal void Run(string args)
        {
            bool statOnly = args.Trim().Equals("-stat");
            Run(statOnly);
        }

        public void Run(bool statOnly)
        {
            Dictionary<string, (int Count, ulong TotalSize)> sizes = new();

            // DbgEngCommand has helper properties for DataTarget and all ClrRuntimes:
            foreach (ClrRuntime runtime in Runtimes)
            {
                foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
                {
                    string typeName = obj.Type?.Name ?? "<unknown type>";

                    if (!statOnly)
                        Console.WriteLine($"{obj.Address,12:x} {obj.Type?.MethodTable ?? 0,12:x} {typeName}");

                    sizes.TryGetValue(typeName, out (int Count, ulong TotalSize) item);
                    item.Count++;
                    item.TotalSize += obj.Size;

                    sizes[typeName] = item;
                }

                if (sizes.Count > 0)
                {
                    if (!statOnly)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Stats:");
                    }

                    long totalObjects = 0;
                    ulong totalBytes = 0;
                    foreach (KeyValuePair<string, (int Count, ulong TotalSize)> item in sizes.OrderBy(r => r.Value.TotalSize))
                    {
                        Console.WriteLine($"{item.Value.Count,12:n0}\t{item.Value.TotalSize,12:n0}\t{item.Key}");

                        totalObjects += item.Value.Count;
                        totalBytes += item.Value.TotalSize;
                    }

                    Console.WriteLine();
                    Console.WriteLine($"{totalObjects:n0} total objects consisting of {totalBytes:n0} total bytes ({totalBytes.ConvertToHumanReadable()}).");
                }
                else
                {
                    Console.WriteLine("0 total objects.");
                }
            }
        }
    }
}