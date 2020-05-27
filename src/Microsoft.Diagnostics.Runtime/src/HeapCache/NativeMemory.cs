using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using DumpAnalyzer.Definitions.Interfaces.Native;
using DumpAnalyzer.Library.Utility;

#nullable disable

namespace DumpAnalyzer.Library.Native.Objects
{
    internal class NativeMemory : IDisposable
    {
        #region Private Fields

        private long maxCacheSize;
        private CacheTechnology cacheTechnology;
        private uint pointerSize;
        private string dumpPath;
        private List<HeapSegment> memorySegments;

        private HeapSegmentDataCache cachedMemorySegments;


        private static readonly IDictionary<string, object> EmptyPreCreatedFieldDictionary = new Dictionary<string, object>();

        #endregion

        // Set the default cap for the cache at 800 megs (semi-arbitrary value) if we are in a 32 bit process, once the max is reached the cache will trim itself by removing
        // the largest items from the least recently accessed 10% of entries (until it brings itsef back under its max cap size).
        internal NativeMemory(uint pointerSize, string dumpPath) : this(pointerSize, dumpPath, maxCacheSize: Environment.Is64BitProcess ? long.MaxValue : 800 * (1 << 20))
        {
        }


        internal NativeMemory(uint pointerSize, string dumpPath, long maxCacheSize) : this(pointerSize, dumpPath, maxCacheSize, CacheTechnology.AWE)
        {
        }

        internal NativeMemory(uint pointerSize, string dumpPath, long maxCacheSize, CacheTechnology cacheTechnology)
        {
            this.pointerSize = pointerSize;
            this.dumpPath = dumpPath;
            this.maxCacheSize = maxCacheSize;
            this.cacheTechnology = cacheTechnology;
        }


        public uint PointerSize => this.pointerSize;

        public IReadOnlyList<INativeHeapSegment> Segments
        {
            get
            {
                return SegmentData;
            }
        }

        public bool TryReadMemory(ulong address, uint byteCount, out byte[] data)
        {
            data = null;

            unsafe
            {
                byte[] buffer = new byte[byteCount];
                fixed (void* pBuffer = buffer)
                {
                    if(TryReadMemory(address, byteCount, new IntPtr(pBuffer)))
                    {
                        data = buffer;
                        return true;
                    }

                    return false;
                }
            }
        }

        public bool TryReadMemoryUntil(ulong address, byte[] terminatingByteSequence, out byte[] data)
        {
            data = null;

            List<HeapSegment> segments = SegmentData;
            HeapSegment lastKnownSegment = segments[segments.Count - 1];

            // quick check if the address is before our first segment or after our last, CLRMD seems to ask for numerous addresses that are very small (like < 0x1000), so no need
            // to do an exhaustive search for that as we would never have anything that low
            if ((address < segments[0].Start) || (address > (lastKnownSegment.Start + lastKnownSegment.Size)))
                return false;

            int curSegmentIndex = -1;
            HeapSegment targetSegment;

            // NOTE: size doesn't matter in our faux MemorySegmentData, we never check it we ony compare start addresses
            int memorySegmentStartIndex = this.memorySegments.BinarySearch(new HeapSegment(address, size: 0), new HeapSegmentComparer());
            if (memorySegmentStartIndex >= 0)
            {
                curSegmentIndex = memorySegmentStartIndex;
            }
            else
            {
                if (memorySegmentStartIndex == ~segments.Count)
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
                    if((curSegmentIndex + 1) == segments.Count)
                    {
                        return false;
                    }

                    address += (ulong)readBytes.Length;
                    targetSegment = segments[++curSegmentIndex];
                    if(address != targetSegment.Start)
                    {
                        return false;
                    }
                }
            }
        }

        // NOTE: Used by unit tests
        internal bool TryReadMemory(ulong address, uint byteCount, IntPtr buffer)
        {
            List<HeapSegment> segments = SegmentData;
            HeapSegment lastKnownSegment = segments[segments.Count - 1];

            // quick check if the address is before our first segment or after our last, CLRMD seems to ask for numerous addresses that are very small (like < 0x1000), so no need
            // to do an exhaustive search for that as we would never have anything that low
            if ((address < segments[0].Start) || (address > (lastKnownSegment.Start + lastKnownSegment.Size)))
                return false;

            int curSegmentIndex = -1;
            HeapSegment targetSegment = null;

            int memorySegmentStartIndex = segments.BinarySearch(new HeapSegment(address, (ulong)byteCount), new HeapSegmentComparer());

            if (memorySegmentStartIndex >= 0)
            {
                curSegmentIndex = memorySegmentStartIndex;
            }
            else
            {
                if (memorySegmentStartIndex == ~segments.Count)
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

                if ((curSegmentIndex + 1) == segments.Count)
                {
                    return false;
                }

                targetSegment = segments[++curSegmentIndex];

                if (address != targetSegment.Start)
                {
                    return false;
                }
            }
        }

        private List<HeapSegment> SegmentData
        {
            get
            {
                if (this.memorySegments == null)
                {
                    using (FileStream dumpStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(dumpStream,
                                                                                  mapName: null,
                                                                                  capacity: 0,
                                                                                  MemoryMappedFileAccess.Read,
                                                                                  HandleInheritability.None,
                                                                                  leaveOpen: false))
                    {
                        PopulateMemorySegmentList(mmf, dumpStream.Length);
                    }
                }

                return this.memorySegments;
            }
        }

        private ISegmentCacheEntry GetcacheEntryForMemorySegment(HeapSegment memorySegment)
        {
            // NOTE: We assume the caller has triggered cachedMemorySegments initialization in the fetching of the MemorySegmentData they have given us

            ISegmentCacheEntry entry;
            if (this.cachedMemorySegments.TryGetCacheEntry(memorySegment.Start, out entry))
            {
                return entry;
            }

            entry = this.cachedMemorySegments.CreateAndAddEntry(memorySegment);

            return entry;
        }

        private void ReadBytesFromSegment(HeapSegment segment, ulong startAddress, uint byteCount, IntPtr buffer, out uint bytesRead)
        {
            ISegmentCacheEntry cacheEntry = GetcacheEntryForMemorySegment(segment);

            cacheEntry.GetDataForAddress(startAddress, byteCount, buffer, out bytesRead);
        }

        private bool ReadBytesFromSegmentUntil(HeapSegment segment, ulong startAddress, byte[] terminatingByteSequence, out byte[] result)
        {
            ISegmentCacheEntry cacheEntry = GetcacheEntryForMemorySegment(segment);

            return cacheEntry.GetDataFromAddressUntil(startAddress, terminatingByteSequence, out result);
        }

        private bool TIsBasicTypeOrArrayOfBasicType<T>()
        {
            Type typeOfT = typeof(T);

            if (typeOfT.IsArray)
            {
                typeOfT = typeOfT.GetElementType();
            }

            TypeCode typeCode = Type.GetTypeCode(typeOfT);
            switch (typeCode)
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
            }

            return typeOfT == typeof(IntPtr) || typeOfT == typeof(UIntPtr);
        }

        private T AddressAsT<T>(ulong address)
        {
            if (typeof(T) == typeof(ulong))
                return (T)(object)address;
            else if (typeof(T) == typeof(long))
                return (T)(object)unchecked((long)address);
            else if (typeof(T) == typeof(int))
            {
                if (this.pointerSize != 4)
                    throw new InvalidOperationException("Attempting to read a pointer as int, but the pointer size of the dump is 64 bits.");

                return (T)(object)unchecked((int)address);
            }
            else if (typeof(T) == typeof(uint))
            {
                if (this.pointerSize != 4)
                    throw new InvalidOperationException("Attempting to read a pointer as uint, but the pointer size of the dump is 64 bits.");

                return (T)(object)(uint)address;
            }
            else if (typeof(T) == typeof(IntPtr))
            {
                // Convert the native data into the appropriate size for this app
                if (IntPtr.Size == 4)
                {
                    return (T)(object)new IntPtr(unchecked((int)address));
                }
                else
                {
                    return (T)(object)new IntPtr(unchecked((long)address));
                }
            }
            else if (typeof(T) == typeof(UIntPtr))
            {
                if (UIntPtr.Size == 4)
                {
                    return (T)(object)new IntPtr(unchecked((uint)address));
                }
                else
                {
                    return (T)(object)new UIntPtr(address);
                }
            }

            throw new InvalidOperationException($"Can't convert an address to type {typeof(T).FullName}");
        }

        private void PopulateMemorySegmentList(MemoryMappedFile mappedFile, long fileLength)
        {
            using(MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor(offset: 0, size: Math.Min((1<<20)*100, fileLength), MemoryMappedFileAccess.Read))
            {
                IntPtr dumpBase = view.SafeMemoryMappedViewHandle.DangerousGetHandle();

                NativeMethods.MinidumpDirectory directory = new NativeMethods.MinidumpDirectory();

                IntPtr memoryListStreamPtr = IntPtr.Zero;

                uint streamSize = 0;
                if (NativeMethods.MiniDumpReadDumpStream(dumpBase,
                                                        NativeMethods.MinidumpStreamType.Memory64ListStream,
                                                        ref directory,
                                                        ref memoryListStreamPtr,
                                                        ref streamSize))
                {
                    ulong segmentCount = 0;
                    unsafe
                    {
                        segmentCount = *((ulong*)memoryListStreamPtr);
                    }

                    this.memorySegments = new List<HeapSegment>((int)segmentCount);

                    unsafe
                    {
                        ulong baseRVA = *((ulong*)(memoryListStreamPtr + sizeof(ulong)));

                        ulong curBlockOffset = 0;

                        NativeMethods.MinidumpMemoryDescriptor64* pMemoryList = (NativeMethods.MinidumpMemoryDescriptor64*)(memoryListStreamPtr + (2 * sizeof(ulong)));
                        for (ulong i = 0; i < segmentCount; i++)
                        {
                            // NOTE: Addresses in minidumps that come from 32 bit systems are sign-extended, this means that if the sign bit is used in the memory address
                            // (since addresses are unsigned) it will sign-extend the bit making something like 0xff7c0000 into 0xffffffffff7c0000 which is a VERY different 
                            // number. So on 32 bit pointer sized systems we simply mask down to 32 bits to avoid misintepreting the actual memory the segment represents
                            ulong startOfMemoryRange = (this.pointerSize == 4 ? (pMemoryList->StartOfMemoryRange & 0x00000000FFFFFFFF) : pMemoryList->StartOfMemoryRange);

                            this.memorySegments.Add(new HeapSegment(startOfMemoryRange, pMemoryList->DataSize, baseRVA + curBlockOffset));
                            curBlockOffset += pMemoryList->DataSize;

                            pMemoryList += 1;
                        }
                    }
                }
                else if(NativeMethods.MiniDumpReadDumpStream(dumpBase,
                                                             NativeMethods.MinidumpStreamType.MemoryListStream,
                                                             ref directory,
                                                             ref memoryListStreamPtr,
                                                             ref streamSize))
                {
                    uint segmentCount = 0;
                    unsafe
                    {
                        segmentCount = *((uint*)memoryListStreamPtr);
                    }

                    this.memorySegments = new List<HeapSegment>((int)segmentCount);

                    unsafe
                    {
                        ulong curBlockOffset = 0;

                        NativeMethods.MinidumpMemoryDescriptor* pMemoryList = (NativeMethods.MinidumpMemoryDescriptor*)(memoryListStreamPtr + sizeof(uint));
                        for (ulong i = 0; i < segmentCount; i++)
                        {
                            // NOTE: Addresses in minidumps that come from 32 bit systems are sign-extended, this means that if the sign bit is used in the memory address
                            // (since addresses are unsigned) it will sign-extend the bit making something like 0xff7c0000 into 0xffffffffff7c0000 which is a VERY different 
                            // number. So on 32 bit pointer sized systems we simply mask down to 32 bits to avoid misintepreting the actual memory the segment represents
                            ulong startOfMemoryRange = (this.pointerSize == 4 ? (pMemoryList->StartOfMemoryRange & 0x00000000FFFFFFFF) : pMemoryList->StartOfMemoryRange);

                            this.memorySegments.Add(new HeapSegment(startOfMemoryRange, pMemoryList->Memory.DataSize, pMemoryList->Memory.Rva));
                            curBlockOffset += pMemoryList->Memory.DataSize;

                            pMemoryList += 1;
                        }
                    }
                }
            }

            // In some dumps the heap memory segments do not appear ordered, strangely enough, but we rely on them being so so we can quickly locate the proper
            // memory segment for an address read request
            this.memorySegments.Sort(new HeapSegmentComparer());

            if ((this.cacheTechnology == CacheTechnology.AWE) &&
                CacheNativeMethods.Util.EnableDisablePrivilege("SeLockMemoryPrivilege", enable: true))
            {
                // If we have the ability to lock physical memory in memory and the user has requested we use AWE, then do so, for best performance
                uint largestSegment = this.memorySegments.Max((hs) => (uint)hs.Size);

                // TODO: Need to close this handle :P
                //
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
                    foreach (HeapSegment segment in this.memorySegments)
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

            return;
        }

        private class MemorySegmentSizeComparer : IComparer<HeapSegment>
        {
            private bool invertOrder;

            internal MemorySegmentSizeComparer(bool invertOrder)
            {
                this.invertOrder = invertOrder;
            }

            public int Compare(HeapSegment x, HeapSegment y)
            {
                if(invertOrder)
                {
                    return (y.End - y.Start).CompareTo(x.End - x.Start);

                }
                else
                {
                    return (x.End - x.Start).CompareTo(y.End - y.Start);
                }
            }
        }

        private class HeapSegmentComparer : IComparer<HeapSegment>
        {
            public int Compare(HeapSegment x, HeapSegment y)
            {
                return x.Start.CompareTo(y.Start);
            }
        }

        public void Dispose()
        {
            this.cachedMemorySegments?.Dispose();
            this.cachedMemorySegments = null;
        }
    }
}
