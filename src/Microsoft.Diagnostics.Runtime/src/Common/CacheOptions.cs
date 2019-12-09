// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class CacheOptions
    {
        public bool CacheTypes { get; set; } = true;
        public bool CacheFields { get; set; } = true;
        public bool CacheMethods { get; set; } = true;

        public StringCaching CacheTypeNames { get; set; } = StringCaching.Cache;
        public StringCaching CacheFieldNames { get; set; } = StringCaching.Cache;
        public StringCaching CacheMethodNames { get; set; } = StringCaching.Cache;
    }
}
