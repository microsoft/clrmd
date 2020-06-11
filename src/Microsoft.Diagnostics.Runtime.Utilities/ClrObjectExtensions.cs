using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public static class ClrObjectExtensions
    {
        public static string GetStringValue(this ClrObject clrObject, int maxLength = int.MaxValue)
        {
            return clrObject.Type.IsString ? clrObject.AsString(maxLength) : null;
        }

        public static UInt64 GetGraphSize(this ClrObject clrObject)
        {
            ulong size = 0;
            var visited = new HashSet<UInt64>();
            Accumulate(clrObject, visited, ref size);

            static void Accumulate(ClrObject clrObject, HashSet<UInt64> visited, ref ulong size)
            {
                if (clrObject.IsNull) return;
                if (visited.Contains(clrObject.Address)) return;

                size += clrObject.Size;
                visited.Add(clrObject.Address);
                foreach (var childReference in clrObject.EnumerateReferencesWithFields())
                {
                    Accumulate(childReference.Object, visited, ref size);
                }
            }

            return size;
        }

    }
}
