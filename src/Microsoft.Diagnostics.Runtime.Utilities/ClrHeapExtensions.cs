using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public static class ClrHeapExtensions
    {
        public static IEnumerable<(ClrType type, IEnumerable<ClrObject> objects, long size)> DumpHeapStat(
            this ClrHeap clrHeap, long minTotalSize = 1024)
        {
            if (clrHeap == null) throw new ArgumentNullException(nameof(clrHeap));
            if (minTotalSize < 0) throw new ArgumentException($"The argument {nameof(minTotalSize)} must be greater or equal to zero");

            var objects = clrHeap.EnumerateObjects();

            return objects
                .GroupBy(o => o.Type, o => o)
                .Select(o => (type: o.Key, objects: (IEnumerable<ClrObject>)o, totalSize: o.Sum(s => (long)s.Size)))
                .Where(t => t.totalSize > minTotalSize)
                .OrderBy(t => t.totalSize);
        }
    }
}
