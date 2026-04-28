// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class CacheOptions
    {
        public bool CacheTypes { get; set; } = true;
        public bool CacheFields { get; set; } = true;
        public bool CacheMethods { get; set; } = true;

        /// <summary>
        /// Whether to cache stack traces or not.  This can take up significant memory in
        /// larger dump files, but will vastly improve performance if the application needs to
        /// walk stacks multiple times.
        /// </summary>
        public bool CacheStackTraces { get; set; } = true;
        public bool CacheStackRoots { get; set; } = true;

        public StringCaching CacheTypeNames { get; set; } = StringCaching.Cache;
        public StringCaching CacheFieldNames { get; set; } = StringCaching.Cache;
        public StringCaching CacheMethodNames { get; set; } = StringCaching.Cache;

        /// <summary>
        /// The maximum amount of memory (virtual address space) used by data readers to cache
        /// memory from the dumpfile.
        /// </summary>
        public long MaxDumpCacheSize { get; set; } = IntPtr.Size == 8 ? 0x1_0000_0000 : 0x800_0000;

        /// <summary>
        /// No longer has any effect.  See the v4 migration guide.
        /// </summary>
        [Obsolete("This no longer has any effect, see the v4 migration guide.")]
        public bool UseOSMemoryFeatures { get; set; }
    }
}