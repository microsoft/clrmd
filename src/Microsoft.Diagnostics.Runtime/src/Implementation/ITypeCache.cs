// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface ITypeCache
    {
        public long TotalBytes { get; }
        public long MaxSize { get; }

        ClrType GetStoredType(ulong key);
        bool Store(ulong key, ClrType type);

        string ReportOrInternString(ulong key, string str);
        void ReportMemory(ulong key, long bytes);
        void Clear();
    }
}