using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class BinarySearchExtensions
    {
        public static int Search<Kind, Key>(this IReadOnlyList<Kind> list, Key key, Func<Kind, Key, int> compareTo)
        {
            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;

                Kind entry = list[mid];
                int comparison = compareTo(entry, key);
                if (comparison > 0)
                {
                    upper = mid - 1;
                }
                else if (comparison < 0)
                {
                    lower = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return -1;
        }

        public static int Search<Kind, Key>(this Kind[] list, Key key, Func<Kind, Key, int> compareTo) => Search((IReadOnlyList<Kind>)list, key, compareTo);
    }
}
