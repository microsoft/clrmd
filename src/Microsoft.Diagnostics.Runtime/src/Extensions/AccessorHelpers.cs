using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class AccessorHelpers
    {
        public static V GetOrDefault<K,V>(this Dictionary<K,V> dictionary, K key)
        {
            dictionary.TryGetValue(key, out V value);
            return value;
        }
    }
}
