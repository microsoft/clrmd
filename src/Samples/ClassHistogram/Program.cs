using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Tests;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassHistogram
{
    class Program
    {
        static void Main(string[] args)
        {
            // build a "class histogram" (i.e. !dumpheap -stat in WinDBG+sos)
            // in an optimized way
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions[0].CreateRuntime();
                ClrHeap heap = runtime.Heap;

                var statistics = new Dictionary<ClrType, TypeEntry>(32768);
                int objectCount = 0;

                // build the class histogram: count and cumulated size of instances per type
                foreach (var objDetails in heap.EnumerateObjectDetails())
                {
                    ulong address = objDetails.Item1;
                    ulong mt = objDetails.Item2;
                    ClrType type = objDetails.Item3;
                    ulong size = objDetails.Item4;

                    TypeEntry entry;
                    if (!statistics.TryGetValue(type, out entry))
                    {
                        entry = new TypeEntry()
                        {
                            TypeName = type.Name,
                            MethodTable = mt,
                            Size = 0
                        };
                        statistics[type] = entry;
                    }
                    entry.Count++;
                    entry.Size += size;
                    objectCount++;
                }

                // display a la !dumpheap -stat
                var sortedStatistics = 
                    from entry in statistics.Values
                    orderby entry.Size
                    select entry;
                Console.WriteLine("{0,12} {1,12} {2,12} {3}", "MT", "Count", "TotalSize", "Class Name");
                foreach (var entry in sortedStatistics)
                    Console.WriteLine($"{entry.MethodTable,12:X12} {entry.Size,12:D} {entry.Count,12:D} {entry.TypeName}");
                Console.WriteLine($"Total {objectCount} objects");
            }
        }
    }

    class TypeEntry
    {
        public string TypeName;
        public ulong MethodTable;
        public int Count;
        public ulong Size;
    }
}
