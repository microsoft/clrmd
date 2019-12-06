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

        public bool CacheTypeNames { get; set; } = true;
        public bool CacheFieldNames { get; set; } = true;
        public bool CacheMethodNames { get; set; } = true;
    }
}
