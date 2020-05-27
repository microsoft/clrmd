// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DumpAnalyzer.Library.Utility;
using Microsoft.Diagnostics.Runtime.Windows;

namespace DumpAnalyzer.Library.Native
{
    internal class ArrayPoolBasedCacheEntry : ISegmentCacheEntry, IDisposable
    {
        private readonly static uint PageSize = (uint)Environment.SystemPageSize;

        private Action<ulong, uint> updateOwningCacheForAddedChunk;
        private DisposerQueue disposerQueue;
        private MemoryMappedFile mappedFile;
        private MinidumpSegment segmentData;
        private ReaderWriterLockSlim[] dataChunkLocks;
        private CachePage[] dataChunks;
        private int accessCount;
        private uint entrySize;
        private long lastAccessTickCount;

        [DebuggerDisplay("Size: {DataExtent}")]
        internal class CachePage
        {
            internal CachePage(byte[] data, uint dataExtent)
            {
                this.Data = data;
                this.DataExtent = dataExtent;
            }

            internal readonly byte[] Data;
            internal readonly uint DataExtent;
        }

        internal ArrayPoolBasedCacheEntry(MemoryMappedFile mappedFile, MinidumpSegment segmentData, DisposerQueue disposerQueue, Action<ulong, uint> updateOwningCacheForAddedChunk)
        {
            this.mappedFile = mappedFile;
            this.segmentData = segmentData;

            this.disposerQueue = disposerQueue;

            int pageCount = (int)((segmentData.End - segmentData.VirtualAddress) / ArrayPoolBasedCacheEntry.PageSize);
            if (((int)(segmentData.End - segmentData.VirtualAddress) % ArrayPoolBasedCacheEntry.PageSize) != 0)
            {
                pageCount++;
            }

            this.dataChunks = new CachePage[pageCount];
            this.dataChunkLocks = new ReaderWriterLockSlim[pageCount];
            for (int i = 0; i < this.dataChunkLocks.Length; i++)
            {
                this.dataChunkLocks[i] = new ReaderWriterLockSlim();
            }

            this.MinSize = (uint)(6 * UIntPtr.Size) + /*our six fields that are refrence type fields (updateOwningCacheForAddedChunk, disposeQueue, mappedFile, segmentData, dataChunkLocks, dataChunks)*/
                           (uint)(this.dataChunks.Length * UIntPtr.Size) + /*The array of data chunks (each element being a pointer)*/
                           (uint)(this.dataChunkLocks.Length * UIntPtr.Size) + /*The array of locks for our data chunks*/
                           sizeof(int) + /*accessCount field*/
                           sizeof(uint) + /*entrySize field*/
                           sizeof(long) /*lastAccessTickCount field*/;

            this.entrySize = MinSize;

            this.updateOwningCacheForAddedChunk = updateOwningCacheForAddedChunk;

            IncrementAccessCount();
            UpdateLastAccessTickCount();
        }

        public uint CurrentSize => this.entrySize;

        public uint MinSize { get; }

        public int AccessCount => this.accessCount;

        public void IncrementAccessCount()
        {
            Interlocked.Increment(ref this.accessCount);
        }

        public long LastAccessTickCount => Interlocked.Read(ref this.lastAccessTickCount);

        public void GetDataForAddress(ulong address, uint byteCount, IntPtr buffer, out uint bytesRead)
        {
            uint offset = (uint)(address - this.segmentData.VirtualAddress);

            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);
            int dataIndex = (int)(pageAlignedOffset / ArrayPoolBasedCacheEntry.PageSize);

            ReaderWriterLockSlim targetLock = this.dataChunkLocks[dataIndex];

            // THREADING: Once we have acquired the read lock we need to hold it, in some fashion, through the entirity of this method, that prevents the cache eviction code from
            // evicting this entry while we are using it.
            targetLock.EnterReadLock();

            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = EnsurePageRangeAtOffset(offset, targetLock, byteCount);

            try
            {
                if (IsSinglePageRead(offset, byteCount))
                {
                    uint inPageOffset = MapOffsetToPageOffset(offset);

                    byte[] targetData = this.dataChunks[dataIndex].Data;
                    unsafe
                    {
                        fixed (byte* pSource = &targetData[inPageOffset])
                        {
                            CacheNativeMethods.Memory.memcpy(buffer, new UIntPtr(pSource), new UIntPtr((uint)byteCount));
                        }
                    }

                    bytesRead = byteCount;
                    return;
                }
                else // This is a read that spans at least one page boundary.
                {
                    IntPtr pInsertionPoint = buffer;

                    uint inPageOffset = MapOffsetToPageOffset(offset);

                    int remainingBytesToRead = (int)byteCount;
                    do
                    {
                        if (dataIndex == this.dataChunks.Length)
                        {
                            // Out of data in this segment, report how many bytes we read
                            bytesRead = byteCount - (uint)remainingBytesToRead;
                            return;
                        }

                        uint bytesInCurrentPage = Math.Min((this.dataChunks[dataIndex].DataExtent - inPageOffset), (uint)remainingBytesToRead);

                        byte[] targetData = this.dataChunks[dataIndex++].Data;

                        unsafe
                        {
                            fixed (byte* pSource = &targetData[inPageOffset])
                            {
                                CacheNativeMethods.Memory.memcpy(pInsertionPoint, new UIntPtr(pSource), new UIntPtr((uint)bytesInCurrentPage));
                            }
                        }

                        pInsertionPoint += (int)bytesInCurrentPage;
                        inPageOffset = 0;

                        remainingBytesToRead -= (int)bytesInCurrentPage;
                    } while (remainingBytesToRead > 0);

                    // If we get here we completed the read across multiple pages, so report we read all that was required
                    bytesRead = byteCount;
                }
            }
            finally
            {
                bool sawOriginalLockInLockCollection = false;
                foreach (var entry in acquiredLocks)
                {
                    if (entry.Lock == targetLock)
                        sawOriginalLockInLockCollection = true;

                    if (entry.IsHeldAsUpgradeableReadLock)
                        entry.Lock.ExitUpgradeableReadLock();
                    else
                        entry.Lock.ExitReadLock();
                }

                // Exit our originally acquire read lock if, in the process of mapping in cache pages, we didn't have to upgrade it to an upgradeable read lock (in which
                // case it would have been released by the loop above).
                if (!sawOriginalLockInLockCollection)
                    targetLock.ExitReadLock();
            }
        }

        public bool GetDataFromAddressUntil(ulong address, byte[] terminatingSequence, out byte[] result)
        {
            uint offset = (uint)(address - this.segmentData.VirtualAddress);

            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);
            int dataIndex = (int)(pageAlignedOffset / ArrayPoolBasedCacheEntry.PageSize);

            List<ReaderWriterLockSlim> locallyAcquiredLocks = new List<ReaderWriterLockSlim>();
            locallyAcquiredLocks.Add(this.dataChunkLocks[dataIndex]);
            locallyAcquiredLocks[0].EnterReadLock();

            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = EnsurePageAtOffset(offset, locallyAcquiredLocks[0]);

            uint pageAdjustedOffset = MapOffsetToPageOffset(offset);

            List<byte> res = new List<byte>();

            try
            {
                CachePage curPage = this.dataChunks[dataIndex];
                while (true)
                {
                    for (uint i = pageAdjustedOffset; i < curPage.DataExtent;)
                    {
                        bool wasTerminatorMatch = true;
                        for (int j = 0; j < terminatingSequence.Length; j++)
                        {
                            if (curPage.Data[i + j] != terminatingSequence[j])
                            {
                                wasTerminatorMatch = false;
                                break;
                            }
                        }

                        // We found our terminating sequence, so don't copy it over to the output array
                        if (wasTerminatorMatch)
                        {
                            result = res.ToArray();
                            return true;
                        }

                        // copy over the non-matching bytes
                        for (int j = 0; j < terminatingSequence.Length; j++)
                        {
                            res.Add(curPage.Data[i + j]);
                        }

                        i += (uint)terminatingSequence.Length;
                    }

                    // Ran out of data in this segment before we found the end of the sequence
                    if ((dataIndex + 1) == this.dataChunks.Length)
                    {
                        result = res.ToArray();
                        return false;
                    }

                    // no offsets when we jump to the next page of data
                    pageAdjustedOffset = 0;

                    offset += (uint)curPage.DataExtent;

                    locallyAcquiredLocks.Add(this.dataChunkLocks[dataIndex + 1]);
                    locallyAcquiredLocks[locallyAcquiredLocks.Count - 1].EnterReadLock();

                    acquiredLocks.AddRange(EnsurePageAtOffset(offset, locallyAcquiredLocks[locallyAcquiredLocks.Count - 1]));

                    curPage = this.dataChunks[++dataIndex];
                    if (curPage == null)
                    {
                        throw new InvalidOperationException($"Expected a CachePage to exist at {dataIndex} but it was null! EnsurePageAtOffset didn't work.");
                    }
                }
            }
            finally
            {
                foreach (var entry in acquiredLocks)
                {
                    locallyAcquiredLocks.Remove(entry.Lock);

                    if (entry.IsHeldAsUpgradeableReadLock)
                        entry.Lock.ExitUpgradeableReadLock();
                    else
                        entry.Lock.ExitReadLock();
                }

                foreach (ReaderWriterLockSlim remainingLock in locallyAcquiredLocks)
                    remainingLock.ExitReadLock();
            }
        }

        public bool PageOutData()
        {
            // We can't page our data out
            return false;
        }

        public void UpdateLastAccessTickCount()
        {
            long originalTickCountValue = Interlocked.Read(ref this.lastAccessTickCount);

            long currentTickCount;
            while (true)
            {
                CacheNativeMethods.Util.QueryPerformanceCounter(out currentTickCount);
                if (Interlocked.CompareExchange(ref this.lastAccessTickCount, currentTickCount, originalTickCountValue) == originalTickCountValue)
                {
                    break;
                }

                originalTickCountValue = Interlocked.Read(ref this.lastAccessTickCount);
            }
        }

        public void Dispose()
        {
            // NOTE: Technically this could cause a hang, we don't want to leak an ArrayPool because someone is reading from it while another thread is trying to remove
            // this cache entry, and we don't want to return it to the pool while they are using it. So the idea is that eventually we will spin around and find all pages
            // not in use, until then, keep spinning :)
            while (true)
            {
                // Assume we will be able to evict all non-null pages
                bool encounteredBusyPage = false;

                for (int i = 0; i < this.dataChunks.Length; i++)
                {
                    CachePage page = this.dataChunks[i];
                    if (page != null)
                    {
                        ReaderWriterLockSlim dataChunkLock = this.dataChunkLocks[i];
                        if (!dataChunkLock.TryEnterWriteLock(timeout: TimeSpan.Zero))
                        {
                            // Someone holds the writelock on this page, skip it and try to get it in another pass, this prevent us from blocking at the moment
                            // on someone currently reading a page, they will likely be done by our next pass
                            encounteredBusyPage = true;
                            continue;
                        }

                        try
                        {
                            // double check that no other thread already scavenged this entry
                            page = this.dataChunks[i];
                            if (page != null)
                            {
                                ArrayPool<byte>.Shared.Return(page.Data);
                                this.dataChunks[i] = null;
                            }
                        }
                        finally
                        {
                            dataChunkLock.ExitWriteLock();
                        }
                    }
                }

                if (!encounteredBusyPage)
                    break;
            }
        }

        private uint MapOffsetToPageOffset(uint offset)
        {
            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);

            int pageIndex = (int)(pageAlignedOffset / ArrayPoolBasedCacheEntry.PageSize);

            return offset - ((uint)pageIndex * (uint)ArrayPoolBasedCacheEntry.PageSize);
        }

        private uint AlignOffsetToPageBoundary(uint offset)
        {
            if ((offset % ArrayPoolBasedCacheEntry.PageSize) != 0)
            {
                return offset - (uint)(offset % ArrayPoolBasedCacheEntry.PageSize);
            }

            return offset;
        }

        private List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> EnsurePageAtOffset(uint offset, ReaderWriterLockSlim originalReadLock)
        {
            return EnsurePageRangeAtOffset(offset, originalReadLock, ArrayPoolBasedCacheEntry.PageSize);
        }

        private List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> EnsurePageRangeAtOffset(uint offset, ReaderWriterLockSlim originalReadLock, uint bytesNeeded)
        {
            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = new List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)>();

            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);

            int dataIndex = (int)(pageAlignedOffset / ArrayPoolBasedCacheEntry.PageSize);

            if (this.dataChunks[dataIndex] == null)
            {
                // THREADING: Our contract is the caller must have acquired the read lock at least for this first page, this is because the caller needs
                // to ensure the page cannot be evicted even after we return (presumably they want to read data from it). However, before we page in a
                // currently null page, we have to acquire the write lock. So we acquire an upgradeable read lock on this index, and then double check if
                // the page entry hasn't been set by someone who beat us to the write lock. We will also return this upgraded lock in the collection of
                // upgraded locks we have acquired so the caller can release it when they are done reading the page(s)
                originalReadLock.ExitReadLock();
                originalReadLock.EnterUpgradeableReadLock();
                originalReadLock.EnterWriteLock();

                uint dataRange = 0;
                try
                {
                    if (this.dataChunks[dataIndex] == null)
                    {
                        byte[] data = GetPageAtOffset(pageAlignedOffset, out dataRange);
                        this.dataChunks[dataIndex] = new CachePage(data, dataRange);
                    }
                }
                catch (Exception)
                {
                    // THREADING: If we see an exception here we are going to rethrow, which means or caller won't be able to release the upgraded read lock, so do it here
                    // as to not leave this page permanently locked out
                    originalReadLock.ExitWriteLock();
                    originalReadLock.ExitUpgradeableReadLock();
                    throw;
                }

                // THREADING: Note the read lock held by our call to EnterUpgradeableReadLock is still in effect, so we need to return this lock as one that the
                // caller must release when they are done
                acquiredLocks.Add((originalReadLock, IsHeldAsUpgradeableReadLock: true));

                // THREADING: Exit our write lock as we are done writing the entry
                originalReadLock.ExitWriteLock();
            }

            // THREADING: We still either hold the original read lock or our upgraded readlock (if we set the cache page entry above), either way we know
            // that the entry at dataIndex is non-null
            uint bytesAvailableOnPage = this.dataChunks[dataIndex].DataExtent - (offset - pageAlignedOffset);
            if (bytesAvailableOnPage < bytesNeeded)
            {
                int bytesRemaining = (int)bytesNeeded - (int)bytesAvailableOnPage;
                do
                {
                    // Out of data for this memory segment, it may be the case that the read crosses between memory segments
                    if ((dataIndex + 1) == this.dataChunks.Length)
                    {
                        return acquiredLocks;
                    }

                    pageAlignedOffset += (uint)ArrayPoolBasedCacheEntry.PageSize;

                    // Take a read lock on the next page entry
                    originalReadLock = this.dataChunkLocks[dataIndex + 1];
                    originalReadLock.EnterReadLock();

                    if (this.dataChunks[dataIndex + 1] != null)
                    {
                        bytesRemaining -= (int)this.dataChunks[++dataIndex].DataExtent;

                        acquiredLocks.Add((originalReadLock, IsHeldAsUpgradeableReadLock: false));
                        continue;
                    }

                    // THREADING: We know the entry must have been null or we would have continued above, so go ahead and enter as an upgradeable lock and
                    // acquire the write lock
                    originalReadLock.ExitReadLock();
                    originalReadLock.EnterUpgradeableReadLock();
                    originalReadLock.EnterWriteLock();

                    if (this.dataChunks[dataIndex + 1] == null)
                    {
                        // Still not set, so we will set it now
                        try
                        {
                            uint dataRange;
                            byte[] data = GetPageAtOffset(pageAlignedOffset, out dataRange);

                            this.dataChunks[++dataIndex] = new CachePage(data, dataRange);

                            bytesRemaining -= (int)dataRange;
                        }
                        catch(Exception)
                        {
                            // THREADING: If we see an exception here we are going to rethrow, which means or caller won't be able to release the upgraded read lock, so do it here
                            // as to not leave this page permanently locked out
                            originalReadLock.ExitWriteLock();
                            originalReadLock.ExitUpgradeableReadLock();

                            // Drop any read locks we have taken up to this point as our caller won't be able to do that since we are re-throwing
                            foreach(var item in acquiredLocks)
                            {
                                if (item.IsHeldAsUpgradeableReadLock)
                                    item.Lock.ExitUpgradeableReadLock();
                                else
                                    item.Lock.ExitReadLock();
                            }

                            throw;
                        }
                    }
                    else // someone else beat us to filling this page in, extract the data we need
                    {
                        bytesRemaining -= (int)this.dataChunks[++dataIndex].DataExtent;
                    }

                    // THREADING: Exit our write lock as we either wrote the entry or someone else did, but keep our read lock so the page can't be
                    // evicted before the caller can read it
                    originalReadLock.ExitWriteLock();
                    acquiredLocks.Add((originalReadLock, IsHeldAsUpgradeableReadLock: true));
                } while (bytesRemaining > 0);
            }

            return acquiredLocks;
        }

        private byte[] GetPageAtOffset(uint offset, out uint dataExtent)
        {
            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageInDataStart((long)(this.segmentData.VirtualAddress + offset), ArrayPoolBasedCacheEntry.PageSize);

            uint readSize;
            if ((offset + ArrayPoolBasedCacheEntry.PageSize) <= (int)this.segmentData.Size)
            {
                readSize = ArrayPoolBasedCacheEntry.PageSize;
            }
            else
            {
                readSize = (uint)this.segmentData.Size - offset;
            }

            dataExtent = readSize;

            bool pageInFailed = false;
            MemoryMappedViewAccessor view = this.mappedFile.CreateViewAccessor((long)this.segmentData.FileOffset + offset, size: (long)readSize, MemoryMappedFileAccess.Read);
            try
            {
                FieldInfo field = typeof(UnmanagedMemoryAccessor).GetField("_offset", BindingFlags.NonPublic | BindingFlags.Instance);
                ulong viewOffset = (ulong)(long)field.GetValue(view);

                unsafe
                {
                    byte* pViewLoc = null;
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pViewLoc);
                        if (pViewLoc == null)
                            throw new InvalidOperationException("Failed to acquire the underlying memory mapped view pointer. This is unexpected");

                        pViewLoc += viewOffset;

                        // Grab a shared buffer to use if there is one, or create one for the pool
                        byte[] data = ArrayPool<byte>.Shared.Rent((int)readSize);

                        // NOTE: This looks sightly ridiculous but view.ReadArray<T> is TERRIBLE for primitive types like byte, it calls Marshal.PtrToStructure for EVERY item in the 
                        // array, the overhead of that call SWAMPS all access costs to the memory, and it is called N times (where N here is 4k), whereas memcpy just blasts the bits
                        // from one location to the other, it is literally a couple of orders of magnitude faster.
                        fixed (byte* pData = data)
                        {
                            CacheNativeMethods.Memory.memcpy(new UIntPtr(pData), new UIntPtr(pViewLoc), new UIntPtr((uint)readSize));
                        }

                        UpdateLastAccessTickCount();
                        this.entrySize += (uint)readSize;
                        this.updateOwningCacheForAddedChunk(this.segmentData.VirtualAddress, (uint)readSize);

                        return data;
                    }
                    finally
                    {
                        if (pViewLoc != null)
                            view.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }
            catch(Exception ex)
            {
                if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataFailed(ex.Message);

                pageInFailed = true;
                throw;
            }
            finally
            {
                if (view != null)
                {
                    this.disposerQueue.Enqueue(view);
                }

                if (!pageInFailed && HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataEnd((int)readSize);
            }
        }

        private bool IsSinglePageRead(uint offset, uint byteCount)
        {
            // It is a simple read if the data lies entirely within a single page
            uint alignedOffset = AlignOffsetToPageBoundary(offset);

            int dataIndex = (int)(alignedOffset / ArrayPoolBasedCacheEntry.PageSize);

            CachePage startingPage = this.dataChunks[dataIndex];
            if (startingPage == null)
            {
                throw new InvalidOperationException($"Inside IsSinglePageRead but the page at index {dataIndex} is null. You need to call EnsurePageAtOffset or EnsurePageRangeAtOffset before calling this method.");
            }

            uint inPageOffset = MapOffsetToPageOffset(offset);

            return (inPageOffset + byteCount) < startingPage.DataExtent;
        }
    }
}
