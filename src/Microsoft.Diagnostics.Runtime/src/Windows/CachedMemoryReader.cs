// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

#pragma warning disable CA2000 // Dispose objects before losing scope
namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class CachedMemoryReader : MinidumpMemoryReader
    {
        private readonly ImmutableArray<MinidumpSegment> _segments;
        private readonly HeapSegmentDataCache _cachedMemorySegments;

        private readonly object _rvaLock = new object();
        private Stream? _rvaStream;

        public string DumpPath { get; }
        public override int PointerSize { get; }
        public CacheTechnology CacheTechnology { get; }
        public long MaxCacheSize { get; }

        public const int MinimumCacheSize = 0x200_0000;

        public CachedMemoryReader(ImmutableArray<MinidumpSegment> segments, string dumpPath, FileStream stream, long maxCacheSize, CacheTechnology cacheTechnology, int pointerSize)
        {
            PointerSize = pointerSize;
            DumpPath = dumpPath;
            MaxCacheSize = maxCacheSize;
            CacheTechnology = cacheTechnology;
            _segments = segments;

            if (CacheTechnology == CacheTechnology.AWE)
            {
                CacheNativeMethods.Util.SYSTEM_INFO sysInfo = new CacheNativeMethods.Util.SYSTEM_INFO();
                CacheNativeMethods.Util.GetSystemInfo(ref sysInfo);

                // The AWE cache allocates on VirtualAlloc sized pages, which are 64k, if the majority of heap segments in the dump are < 64k this can be wasteful
                // of memory (in the extreme we can end using 64k of VM to store < 100 bytes), in this case we will force the cache technology to be the array pool
                // one which allocates on system page size (4k), which is still wasteful but FAR less so.
                int segmentsBelow64K = _segments.Sum((hs) => hs.Size < sysInfo.dwAllocationGranularity ? 1 : 0);
                if (segmentsBelow64K > (int)(_segments.Length * 0.80))
                    CacheTechnology = CacheTechnology.ArrayPool;
            }


            if ((CacheTechnology == CacheTechnology.AWE) &&
                CacheNativeMethods.Util.EnableDisablePrivilege("SeLockMemoryPrivilege", enable: true))
            {
                _rvaStream = stream;

                // If we have the ability to lock physical memory in memory and the user has requested we use AWE, then do so, for best performance
                uint largestSegment = _segments.Max((hs) => (uint)hs.Size);

                // Create a a single large page, the size of the largest heap segment, we will read each in turn into this one large segment before
                // splitting them into (potentially) multiple VirtualAlloc pages.
                AWEBasedCacheEntryFactory cacheEntryFactory = new AWEBasedCacheEntryFactory(stream.SafeFileHandle.DangerousGetHandle());
                cacheEntryFactory.CreateSharedSegment(largestSegment);

                _cachedMemorySegments = new HeapSegmentDataCache(cacheEntryFactory, MaxCacheSize);

                // Force the cache entry creation, this is because the AWE factory will read the heap segment data from the file into physical memory, it is FAR
                // better for perf if we read it all in one contiunous go instead of piece-meal as needed.
                foreach (MinidumpSegment segment in _segments)
                    _cachedMemorySegments.CreateAndAddEntry(segment);

                // We are done using the shared segment so we can release it now
                cacheEntryFactory.DeleteSharedSegment();
            }
            else
            {
                // We can't add the lock memory privilege, so just fall back on our ArrayPool/MemoryMappedFile based cache 
                _cachedMemorySegments = new HeapSegmentDataCache(new ArrayPoolBasedCacheEntryFactory(stream), MaxCacheSize);
            }
        }

        public override int ReadFromRva(ulong rva, Span<byte> buffer)
        {
            lock (_rvaLock)
            {
                if (_rvaStream is null)
                    _rvaStream = File.OpenRead(DumpPath);

                _rvaStream.Position = (long)rva;
                return _rvaStream.Read(buffer);
            }
        }

        public override unsafe int Read(ulong address, Span<byte> buffer)
        {
            fixed (void* pBuffer = buffer)
                return TryReadMemory(address, buffer.Length, new IntPtr(pBuffer));
        }

        internal int TryReadMemory(ulong address, int byteCount, IntPtr buffer)
        {
            ImmutableArray<MinidumpSegment> segments = _segments;
            MinidumpSegment lastKnownSegment = segments[segments.Length - 1];

            // quick check if the address is before our first segment or after our last, CLRMD seems to ask for numerous addresses that are very small (like < 0x1000), so no need
            // to do an exhaustive search for that as we would never have anything that low
            if ((address < segments[0].VirtualAddress) || (address > (lastKnownSegment.VirtualAddress + lastKnownSegment.Size)))
                return 0;

            int curSegmentIndex = -1;
            MinidumpSegment targetSegment;

            int memorySegmentStartIndex = segments.Search(address, (x, addr) => (x.VirtualAddress <= addr && addr < x.VirtualAddress + x.Size) ? 0 : x.VirtualAddress.CompareTo(addr));

            if (memorySegmentStartIndex >= 0)
            {
                curSegmentIndex = memorySegmentStartIndex;
            }
            else
            {
                // It would be beyond the end of the memory segments we have
                if (memorySegmentStartIndex == ~segments.Length)
                    return 0;

                // This is the index of the first segment of memory whose start address is GREATER than the given address.
                int insertionIndex = ~memorySegmentStartIndex;
                if (insertionIndex == 0)
                    return 0;

                // Grab the segment before this one, as it must be the one that contains this address
                curSegmentIndex = insertionIndex - 1;
            }

            targetSegment = segments[curSegmentIndex];

            // This can only be true if we went into the else block above, located a segment BEYOND the given address, backed up one segment and the address
            // isn't inside that segment. This means we don't have the requested memory in the dump.
            if (address > targetSegment.End)
                return 0;

            IntPtr insertionPtr = buffer;
            int totalBytes = 0;

            int remainingBytes = byteCount;
            while (true)
            {
                ReadBytesFromSegment(targetSegment, address, remainingBytes, insertionPtr, out int bytesRead);

                totalBytes += bytesRead;
                remainingBytes -= bytesRead;

                if (remainingBytes == 0 || bytesRead == 0)
                    return totalBytes;

                insertionPtr += bytesRead;
                address += (uint)bytesRead;

                if ((curSegmentIndex + 1) == segments.Length)
                    return totalBytes;

                targetSegment = segments[++curSegmentIndex];

                if (address != targetSegment.VirtualAddress)
                {
                    curSegmentIndex = segments.Search(address, (x, addr) => (x.VirtualAddress <= addr && addr < x.VirtualAddress + x.Size) ? 0 : x.VirtualAddress.CompareTo(addr));
                    if (curSegmentIndex == -1)
                        return totalBytes;

                    targetSegment = segments[curSegmentIndex];
                }
            }
        }

        private SegmentCacheEntry GetCacheEntryForMemorySegment(MinidumpSegment memorySegment)
        {
            // NOTE: We assume the caller has triggered cachedMemorySegments initialization in the fetching of the MemorySegmentData they have given us
            if (_cachedMemorySegments.TryGetCacheEntry(memorySegment.VirtualAddress, out SegmentCacheEntry? entry))
                return entry!;

            return _cachedMemorySegments.CreateAndAddEntry(memorySegment);
        }

        private void ReadBytesFromSegment(MinidumpSegment segment, ulong startAddress, int byteCount, IntPtr buffer, out int bytesRead)
        {
            SegmentCacheEntry cacheEntry = GetCacheEntryForMemorySegment(segment);
            cacheEntry.GetDataForAddress(startAddress, (uint)byteCount, buffer, out uint read);
            bytesRead = (int)read;
        }

        public override void Dispose()
        {
            _cachedMemorySegments.Dispose();
            _rvaStream?.Dispose();
        }
    }
}
