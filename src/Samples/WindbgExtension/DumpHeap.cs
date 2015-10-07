using Microsoft.Diagnostics.Runtime;
using RGiesecke.DllExport;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace WindbgExtension
{
    public partial class DebuggerExtensions
    {
        [DllExport("heapstat")]
        public static void HeapStat(IntPtr client, [MarshalAs(UnmanagedType.LPStr)] string args)
        {
            // Must be the first thing in our extension.
            if (!InitApi(client))
                return;


            // Use ClrMD as normal, but ONLY cache the copy of ClrRuntime (this.Runtime).  All other
            // types you get out of ClrMD (such as ClrHeap, ClrTypes, etc) should be discarded and
            // reobtained every run.
            ClrHeap heap = Runtime.GetHeap();

            var stats = from obj in heap.EnumerateObjectAddresses()
                        let t = heap.GetObjectType(obj)
                        group obj by t into g
                        let size = g.Sum(p => (uint)g.Key.GetSize(p))
                        orderby size
                        select new
                        {
                            Size = size,
                            Count = g.Count(),
                            Name = g.Key.Name
                        };

            // Console.WriteLine now writes to the debugger.
            foreach (var entry in stats)
                Console.WriteLine("{0,12:n0} {1,12:n0} {2}", entry.Count, entry.Size, entry.Name);
        }
    }
}
