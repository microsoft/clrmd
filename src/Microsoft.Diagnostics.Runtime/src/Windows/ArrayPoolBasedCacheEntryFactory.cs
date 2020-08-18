// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class ArrayPoolBasedCacheEntryFactory : SegmentCacheEntryFactory, IDisposable
    {
        private readonly MemoryMappedFile _mappedFile;

        internal ArrayPoolBasedCacheEntryFactory(FileStream stream, bool leaveOpen)
        {
            _mappedFile = MemoryMappedFile.CreateFromFile(stream,
                                                          mapName: null,
                                                          capacity: 0,
                                                          MemoryMappedFileAccess.Read,
                                                          HandleInheritability.None,
                                                          leaveOpen);
        }

        public override SegmentCacheEntry CreateEntryForSegment(MinidumpSegment segmentData, Action<uint> updateOwningCacheForSizeChangeCallback)
        {
            return new ArrayPoolBasedCacheEntry(_mappedFile, segmentData, updateOwningCacheForSizeChangeCallback);
        }

        public void Dispose() => _mappedFile.Dispose();
    }
}
