// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.MemoryMappedFiles;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class ArrayPoolBasedCacheEntryFactory : SegmentCacheEntryFactory
    {
        private readonly MemoryMappedFile _mappedFile;

        private string _dumpPath;

        internal ArrayPoolBasedCacheEntryFactory(string dumpPath)
        {
            _dumpPath = dumpPath;
        }

        public override SegmentCacheEntry CreateEntryForSegment(MinidumpSegment segmentData, Action<uint> updateOwningCacheForSizeChangeCallback)
        {
            return new ArrayPoolBasedCacheEntry(_dumpPath, segmentData, updateOwningCacheForSizeChangeCallback);
        }
    }
}