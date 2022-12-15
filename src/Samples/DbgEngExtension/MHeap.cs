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

        public void Run(string args)
        {
            bool statOnly = args.Trim().Equals("-stat");
            Dictionary<string, (int Count, ulong TotalSize)> sizes = new();

            // DbgEngCommand has helper properties for DataTarget and all ClrRuntimes:
            foreach (ClrRuntime runtime in Runtimes)
            {
                foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
                {
                    string typeName = obj.Type?.Name ?? "<unknown type>";

                    if (!statOnly)
                        Console.WriteLine($"{obj.Address,12:x} {obj.Type?.MethodTable??0,12:x} {typeName}");

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

                    foreach (var item in sizes.OrderBy(r => r.Value.TotalSize))
                        Console.WriteLine($"{item.Value.Count,12:n0}\t{item.Value.TotalSize,12:n0}\t{item.Key}");
                }
                else
                {
                    Console.WriteLine("0 total objects.");
                }
            }
        }
    }
}
