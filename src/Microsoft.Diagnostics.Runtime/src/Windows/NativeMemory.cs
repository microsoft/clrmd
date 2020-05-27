using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using DumpAnalyzer.Definitions.Interfaces.Native;
using DumpAnalyzer.Library.Utility;
using Microsoft.Diagnostics.Runtime.Windows;

namespace DumpAnalyzer.Library.Native.Objects
{
    internal class NativeMemory : IDisposable
    {
        private long maxCacheSize;
        private CacheTechnology cacheTechnology;
        private int pointerSize;
        private string dumpPath;
        private ImmutableArray<MinidumpSegment> memorySegments;

        private HeapSegmentDataCache cachedMemorySegments;


        // Set the default cap for the cache at 800 megs (semi-arbitrary value) if we are in a 32 bit process, once the max is reached the cache will trim itself by removing
        // the largest items from the least recently accessed 10% of entries (until it brings itsef back under its max cap size).
        internal NativeMemory(int pointerSize, string dumpPath, ImmutableArray<MinidumpSegment> segments) : this(pointerSize, dumpPath, maxCacheSize: Environment.Is64BitProcess ? long.MaxValue : 800 * (1 << 20), segments)
        {
        }


        internal NativeMemory(int pointerSize, string dumpPath, long maxCacheSize, ImmutableArray<MinidumpSegment> segments) : this(pointerSize, dumpPath, maxCacheSize, CacheTechnology.AWE, segments)
        {
        }

        internal NativeMemory(int pointerSize, string dumpPath, long maxCacheSize, CacheTechnology cacheTechnology, ImmutableArray<MinidumpSegment> segments)
        {
            this.pointerSize = pointerSize;
            this.dumpPath = dumpPath;
            this.maxCacheSize = maxCacheSize;
            this.cacheTechnology = cacheTechnology;
            memorySegments = segments;


            if ((this.cacheTechnology == CacheTechnology.AWE) &&
                CacheNativeMethods.Util.EnableDisablePrivilege("SeLockMemoryPrivilege", enable: true))
            {
                // If we have the ability to lock physical memory in memory and the user has requested we use AWE, then do so, for best performance
                uint largestSegment = this.memorySegments.Max((hs) => (uint)hs.Size);

                // Create a a single large page, the size of the largest heap segment, we will read each in turn into this one large segment before
                // splitting them into (potentially) multiple VirtualAlloc pages.
                IntPtr dumpHandle = CacheNativeMethods.File.CreateFile(this.dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    AWEBasedCacheEntryFactory cacheEntryFactory = new AWEBasedCacheEntryFactory(dumpHandle);
                    cacheEntryFactory.CreateSharedSegment(largestSegment);

                    this.cachedMemorySegments = new HeapSegmentDataCache(cacheEntryFactory, this.maxCacheSize);

                    // Force the cache entry creation, this is because the AWE factory will read the heap segment data from the file into physical memory, it is FAR
                    // better for perf if we read it all in one contiunous go instead of piece-meal as needed.
                    foreach (MinidumpSegment segment in this.memorySegments)
                    {
                        this.cachedMemorySegments.CreateAndAddEntry(segment);
                    }

                    // We are done using the shared segment so we can release it now
                    cacheEntryFactory.DeleteSharedSegment();
                }
                finally
                {
                    CacheNativeMethods.File.CloseHandle(dumpHandle);
                }
            }
            else
            {
                // We can't add the lock memory privilege, so just fall back on our ArrayPool/MemoryMappedFile based cache 
                this.cachedMemorySegments = new HeapSegmentDataCache(new ArrayPoolBasedCacheEntryFactory(this.dumpPath, DisposerQueue.Shared), this.maxCacheSize);
            }

        }

        public bool TryReadMemory(ulong address, uint byteCount, Span<byte> buffer)
        {
            unsafe
            {
                fixed (void* pBuffer = buffer)
                {
                    if(TryReadMemory(address, byteCount, new IntPtr(pBuffer)))
                    {
                        return true;
                    }

                    return false;
                }
            }
        }

        public bool TryReadMemoryUntil(ulong address, byte[] terminatingByteSequence, out byte[] data)
        {
            data = null;

            ImmutableArray<MinidumpSegment> segments = SegmentData;
            MinidumpSegment lastKnownSegment = segments[segments.Length - 1];

            // quick check if the address is before our first segment or after our last, CLRMD seems to ask for numerous addresses that are very small (like < 0x1000), so no need
            // to do an exhaustive search for that as we would never have anything that low
            if ((address < segments[0].VirtualAddress) || (address > (lastKnownSegment.VirtualAddress + lastKnownSegment.Size)))
                return false;

            int curSegmentIndex = -1;
            MinidumpSegment targetSegment;

            // NOTE: size doesn't matter in our faux MemorySegmentData, we never check it we ony compare start addresses
            int memorySegmentStartIndex = this.memorySegments.BinarySearch(new MinidumpSegment(0, address, 0), new HeapSegmentComparer());
            if (memorySegmentStartIndex >= 0)
            {
                curSegmentIndex = memorySegmentStartIndex;
            }
            else
            {
                if (memorySegmentStartIndex == ~segments.Length)
                {
                    // It would be beyond the end of the memory segments we have
                    return false;
                }

                int insertionIndex = ~memorySegmentStartIndex;

                // This is the index of the first item GREATER than the given address. This means that the given address MUST fall at insertionIndex - 1 (potentially overlapping with the next
                // segment if the read value > SegmentSize)
                if (insertionIndex == 0)
                {
                    // Its before our first memory address
                    return false;
                }

                curSegmentIndex = insertionIndex - 1;
            }

            targetSegment = segments[curSegmentIndex];

            // This can only be true if we went into the else block above, located a segment BEYOND the given address, backed up one segment and the address
            // isn't inside that segment. This means we don't have the requested memory in the dump.
            if (address > targetSegment.End)
                return false;

            List<byte> res = new List<byte>();

            while(true)
            {
                byte[] readBytes;
                if (ReadBytesFromSegmentUntil(targetSegment, address, terminatingByteSequence, out readBytes))
                {
                    res.AddRange(readBytes);
                    data = res.ToArray();
                    return true;
                }
                else
                {
                    res.AddRange(readBytes);
                    if((curSegmentIndex + 1) == segments.Length)
                    {
                        return false;
                    }

                    address += (ulong)readBytes.Length;
                    targetSegment = segments[++curSegmentIndex];
                    if(address != targetSegment.VirtualAddress)
                    {
                        return false;
                    }
                }
            }
        }

        internal bool TryReadMemory(ulong address, uint byteCount, IntPtr buffer)
        {
            ImmutableArray<MinidumpSegment> segments = SegmentData;
            MinidumpSegment lastKnownSegment = segments[segments.Length - 1];

            // quick check if the address is before our first segment or after our last, CLRMD seems to ask for numerous addresses that are very small (like < 0x1000), so no need
            // to do an exhaustive search for that as we would never have anything that low
            if ((address < segments[0].VirtualAddress) || (address > (lastKnownSegment.VirtualAddress + lastKnownSegment.Size)))
                return false;

            int curSegmentIndex = -1;
            MinidumpSegment targetSegment;

            int memorySegmentStartIndex = segments.BinarySearch(new MinidumpSegment(0, address, (ulong)byteCount), new HeapSegmentComparer());

            if (memorySegmentStartIndex >= 0)
            {
                curSegmentIndex = memorySegmentStartIndex;
            }
            else
            {
                if (memorySegmentStartIndex == ~segments.Length)
                {
                    // It would be beyond the end of the memory segments we have
                    return false;
                }

                // This is the index of the first segment of memory whose start address is GREATER than the given address.
                int insertionIndex = ~memorySegmentStartIndex;

                if (insertionIndex == 0)
                {
                    // Its before our first memory address
                    return false;
                }

                // Grab the segment before this one, as it must be the one that contains this address
                curSegmentIndex = insertionIndex - 1;
            }

            targetSegment = segments[curSegmentIndex];

            // This can only be true if we went into the else block above, located a segment BEYOND the given address, backed up one segment and the address
            // isn't inside that segment. This means we don't have the requested memory in the dump.
            if (address > targetSegment.End)
                return false;

            IntPtr insertionPtr = buffer;

            uint remainingBytes = byteCount;
            while (true)
            {
                uint bytesRead = 0;
                ReadBytesFromSegment(targetSegment, address, remainingBytes, insertionPtr, out bytesRead);

                remainingBytes -= bytesRead;
                if (remainingBytes == 0)
                {
                    return true;
                }

                insertionPtr += (int)bytesRead;
                address += (ulong)bytesRead;

                if ((curSegmentIndex + 1) == segments.Length)
                {
                    return false;
                }

                targetSegment = segments[++curSegmentIndex];

                if (address != targetSegment.VirtualAddress)
                {
                    return false;
                }
            }
        }

        private ImmutableArray<MinidumpSegment> SegmentData => memorySegments;

        private ISegmentCacheEntry GetcacheEntryForMemorySegment(MinidumpSegment memorySegment)
        {
            // NOTE: We assume the caller has triggered cachedMemorySegments initialization in the fetching of the MemorySegmentData they have given us

            ISegmentCacheEntry entry;
            if (this.cachedMemorySegments.TryGetCacheEntry(memorySegment.VirtualAddress, out entry))
            {
                return entry;
            }

            entry = this.cachedMemorySegments.CreateAndAddEntry(memorySegment);

            return entry;
        }

        private void ReadBytesFromSegment(MinidumpSegment segment, ulong startAddress, uint byteCount, IntPtr buffer, out uint bytesRead)
        {
            ISegmentCacheEntry cacheEntry = GetcacheEntryForMemorySegment(segment);

            cacheEntry.GetDataForAddress(startAddress, byteCount, buffer, out bytesRead);
        }

        private bool ReadBytesFromSegmentUntil(MinidumpSegment segment, ulong startAddress, byte[] terminatingByteSequence, out byte[] result)
        {
            ISegmentCacheEntry cacheEntry = GetcacheEntryForMemorySegment(segment);

            return cacheEntry.GetDataFromAddressUntil(startAddress, terminatingByteSequence, out result);
        }

        private class HeapSegmentComparer : IComparer<MinidumpSegment>
        {
            public int Compare(MinidumpSegment x, MinidumpSegment y)
            {
                return x.VirtualAddress.CompareTo(y.VirtualAddress);
            }
        }

        public void Dispose()
        {
            this.cachedMemorySegments?.Dispose();
            this.cachedMemorySegments = null;
        }
    }
}
