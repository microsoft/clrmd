using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class BinarySearchExtensions
    {
        public static bool Search<Kind, Key>(this IReadOnlyList<Kind> list, Key key, Func<Kind, Key, int> compareTo, out Kind found)
        {
            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;

                int comparison = compareTo(list[mid], key);
                if (comparison < 0)
                {
                    upper = mid - 1;
                }
                else if (comparison > 0)
                {
                    lower = mid + 1;
                }
                else
                {
                    found = list[mid];
                    return true;
                }
            }

            found = default;
            return false;
        }
    }
}
