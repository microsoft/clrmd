// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class DefaultTypeCache : ITypeCache
    {
        private readonly Dictionary<ulong, ClrType> _types = new Dictionary<ulong, ClrType>(1024);
        public long TotalBytes { get; private set; }
        public long MaxSize { get; } = IntPtr.Size == 4 ? 500 * 1024 * 1024 : long.MaxValue;

        public void Clear()
        {
            TotalBytes = 0;
            _types.Clear();
        }

        public bool Store(ulong key, ClrType type)
        {
            if (TotalBytes >= MaxSize)
                return false;

            _types[key] = type;
            return true;
        }

        public ClrType GetStoredType(ulong key) => _types.GetOrDefault(key);

        public void ReportMemory(ulong key, long bytes)
        {
            TotalBytes += bytes;
        }

        public string ReportOrInternString(ulong key, string str)
        {
            if (str != null)
                ReportMemory(key, 2 * IntPtr.Size + 2 * str.Length);
            return str;
        }
    }
}