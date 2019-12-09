// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class AccessorHelpers
    {
        public static V GetOrDefault<K, V>(this Dictionary<K, V> dictionary, K key)
        {
            dictionary.TryGetValue(key, out V value);
            return value;
        }
    }
}
