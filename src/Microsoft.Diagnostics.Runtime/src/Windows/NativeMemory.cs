// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class NativeMemory : IDisposable, IMemoryReader
    {
        private readonly ImmutableArray<MinidumpSegment> _segments;
        private readonly HeapSegmentDataCache _cachedMemorySegments;

        public string DumpPath { get; }
        public int PointerSize { get; }
        public CacheTechnology CacheTechnology { get; }
        public long MaxCacheSize { get; }

        public NativeMemory(int pointerSize, string dumpPath, long maxCacheSize, CacheTechnology cacheTechnology, ImmutableArray<MinidumpSegment> segments)
        {
            PointerSize = pointerSize;
            DumpPath = dumpPath;
            MaxCacheSize = maxCacheSize;
            CacheTechnology = cacheTechnology;
            _segments = segments;

            if ((CacheTechnology == CacheTechnology.AWE) &&
                CacheNativeMethods.Util.EnableDisablePrivilege("SeLockMemoryPrivilege", enable: true))
            {
                // If we have the ability to lock physical memory in memory and the user has requested we use AWE, then do so, for best performance
                uint largestSegment = _segments.Max((hs) => (uint)hs.Size);

                // Create a a single large page, the size of the largest heap segment, we will read each in turn into this one large segment before
                // splitting them into (potentially) multiple VirtualAlloc pages.
                IntPtr dumpHandle = CacheNativeMethods.File.CreateFile(DumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    AWEBasedCacheEntryFactory cacheEntryFactory = new AWEBasedCacheEntryFactory(dumpHandle);
                    cacheEntryFactory.CreateSharedSegment(largestSegment);

                    _cachedMemorySegments = new HeapSegmentDataCache(cacheEntryFactory, MaxCacheSize);

                    // Force the cache entry creation, this is because the AWE factory will read the heap segment data from the file into physical memory, it is FAR
                    // better for perf if we read it all in one contiunous go instead of piece-meal as needed.
                    foreach (MinidumpSegment segment in _segments)
                        _cachedMemorySegments.CreateAndAddEntry(segment);

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

#pragma warning disable CA2000 // Dispose objects before losing scope
                _cachedMemorySegments = new HeapSegmentDataCache(new ArrayPoolBasedCacheEntryFactory(DumpPath), MaxCacheSize);
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }

        public unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer) == buffer.Length)
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T t);
            return t;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[PointerSize];
            if (Read(address, buffer) == PointerSize)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public ulong ReadPointer(ulong address)
        {
            if (ReadPointer(address, out ulong value))
                return value;

            return 0;
        }

        public unsafe int Read(ulong address, Span<byte> buffer)
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

        private SegmentCacheEntry GetcacheEntryForMemorySegment(MinidumpSegment memorySegment)
        {
            // NOTE: We assume the caller has triggered cachedMemorySegments initialization in the fetching of the MemorySegmentData they have given us
            if (_cachedMemorySegments.TryGetCacheEntry(memorySegment.VirtualAddress, out SegmentCacheEntry entry))
                return entry;

            return _cachedMemorySegments.CreateAndAddEntry(memorySegment);
        }

        private void ReadBytesFromSegment(MinidumpSegment segment, ulong startAddress, int byteCount, IntPtr buffer, out int bytesRead)
        {
            SegmentCacheEntry cacheEntry = GetcacheEntryForMemorySegment(segment);
            cacheEntry.GetDataForAddress(startAddress, (uint)byteCount, buffer, out uint read);
            bytesRead = (int)read;
        }

        public void Dispose()
        {
            _cachedMemorySegments.Dispose();
        }
    }
}
