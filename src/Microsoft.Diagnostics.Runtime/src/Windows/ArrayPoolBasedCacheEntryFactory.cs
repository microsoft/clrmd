// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal class ArrayPoolBasedCacheEntryFactory : ISegmentCacheEntryFactory, IDisposable
    {
        private FileStream dumpStream;
        private MemoryMappedFile mappedFile;
        private DisposerQueue disposerQueue;

        internal ArrayPoolBasedCacheEntryFactory(string dumpPath, DisposerQueue disposerQueue)
        {
            this.dumpStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            this.mappedFile = MemoryMappedFile.CreateFromFile(this.dumpStream,
                                                              mapName: null,
                                                              capacity: 0,
                                                              MemoryMappedFileAccess.Read,
                                                              HandleInheritability.None,
                                                              leaveOpen: false);

            this.disposerQueue = disposerQueue;
        }

        public SegmentCacheEntry CreateEntryForSegment(MinidumpSegment segmentData, Action<ulong, uint> updateOwningCacheForSizeChangeCallback)
        {
            return new ArrayPoolBasedCacheEntry(this.mappedFile, segmentData, this.disposerQueue, updateOwningCacheForSizeChangeCallback);
        }

        public void Dispose()
        {
            using (this.mappedFile)
            { }
        }
    }
}
