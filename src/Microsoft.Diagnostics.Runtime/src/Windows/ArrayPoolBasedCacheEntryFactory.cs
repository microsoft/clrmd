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
        private readonly FileStream _dumpStream;
        private readonly MemoryMappedFile _mappedFile;
        private readonly DisposerQueue _disposerQueue;

        internal ArrayPoolBasedCacheEntryFactory(string dumpPath, DisposerQueue disposerQueue)
        {
            _dumpStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _mappedFile = MemoryMappedFile.CreateFromFile(_dumpStream,
                                                          mapName: null,
                                                          capacity: 0,
                                                          MemoryMappedFileAccess.Read,
                                                          HandleInheritability.None,
                                                          leaveOpen: false);

            _disposerQueue = disposerQueue;
        }

        public override SegmentCacheEntry CreateEntryForSegment(MinidumpSegment segmentData, Action<ulong, uint> updateOwningCacheForSizeChangeCallback)
        {
            return new ArrayPoolBasedCacheEntry(_mappedFile, segmentData, _disposerQueue, updateOwningCacheForSizeChangeCallback);
        }

        public void Dispose() => _mappedFile.Dispose();
    }
}
