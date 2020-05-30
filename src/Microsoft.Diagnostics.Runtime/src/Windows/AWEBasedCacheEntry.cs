// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;


// TODO:  This code wasn't written to consider nullable.
#nullable disable

#pragma warning disable CA1810 // Initialize reference type static fields inline
namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class AWEBasedCacheEntry : SegmentCacheEntry, IDisposable
    {
        private readonly static uint VirtualAllocPageSize; // set in static ctor
        private readonly static int SystemPageSize = Environment.SystemPageSize;

        private readonly Action<ulong, uint> _updateOwningCacheForSizeChangeCallback;
        private readonly MinidumpSegment _segmentData;
        private UIntPtr _pageFrameArray;
        private readonly int _pageFrameArrayItemCount;
        private readonly ReaderWriterLockSlim[] _pageLocks;
        private CachePage[] _pages;
        private long _lastAccessTickCount;
        private int _accessCount;

        static AWEBasedCacheEntry()
        {
            CacheNativeMethods.Util.SYSTEM_INFO sysInfo = new CacheNativeMethods.Util.SYSTEM_INFO();
            CacheNativeMethods.Util.GetSystemInfo(ref sysInfo);

            VirtualAllocPageSize = sysInfo.dwAllocationGranularity;
        }

        internal AWEBasedCacheEntry(MinidumpSegment segmentData, Action<ulong, uint> updateOwningCacheForSizeChangeCallback, UIntPtr pageFrameArray, int pageFrameArrayItemCount)
        {
            int pagesSize = (int)(segmentData.Size / VirtualAllocPageSize);
            if ((segmentData.Size % VirtualAllocPageSize) != 0)
                pagesSize++;

            _pages = new CachePage[pagesSize];
            _pageLocks = new ReaderWriterLockSlim[pagesSize];
            for (int i = 0; i < _pageLocks.Length; i++)
            {
                _pageLocks[i] = new ReaderWriterLockSlim();
            }

            MinSize = (uint)(_pages.Length * UIntPtr.Size) + /*size of pages array*/
                      (uint)(_pageLocks.Length * UIntPtr.Size) + /*size of pageLocks array*/
                      (uint)(_pageFrameArrayItemCount * UIntPtr.Size) +  /*size of pageFrameArray*/
                      (uint)(5 * IntPtr.Size) + /*size of reference type fields (updateOwningCacheForSizeChangeCallback, segmentData, pageFrameArray, pageLocks, pages)*/
                      (2 * sizeof(int)) + /*size of int fields (pageFrameArrayItemCount, accessSize)*/
                      sizeof(uint) +  /*size of uint field (accessCount)*/
                      sizeof(long); /*size of long field (lasAccessTickCount)*/

            _segmentData = segmentData;
            _updateOwningCacheForSizeChangeCallback = updateOwningCacheForSizeChangeCallback;
            _pageFrameArray = pageFrameArray;
            _pageFrameArrayItemCount = pageFrameArrayItemCount;

            CurrentSize = MinSize;

            IncrementAccessCount();
            UpdateLastAccessTickCount();
        }

        public override long LastAccessTickCount => Interlocked.Read(ref _lastAccessTickCount);

        public override int AccessCount => _accessCount;

        public override void IncrementAccessCount()
        {
            Interlocked.Increment(ref _accessCount);
        }

        public override void GetDataForAddress(ulong address, uint byteCount, IntPtr buffer, out uint bytesRead)
        {
            uint offset = (uint)(address - _segmentData.VirtualAddress);
            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);

            int dataIndex = (int)(pageAlignedOffset / VirtualAllocPageSize);

            ReaderWriterLockSlim targetLock = _pageLocks[dataIndex];

            // THREADING: Once we have acquired the read lock we need to hold it, in some fashion, through the entirity of this method, that prevents the PageOut code from
            // evicting this page data while we are using it.
            targetLock.EnterReadLock();

            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = EnsurePageRangeAtOffset(offset, targetLock, byteCount);

            try
            {
                if (IsSinglePageRead(offset, byteCount))
                {
                    uint inPageOffset = MapOffsetToPageOffset(offset);

                    CacheNativeMethods.Memory.memcpy(buffer, UIntPtr.Add(_pages[dataIndex].Data, (int)inPageOffset), new UIntPtr(byteCount));

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
                        if (dataIndex == _pages.Length)
                        {
                            // Out of data in this segment, report how many bytes we read
                            bytesRead = byteCount - (uint)remainingBytesToRead;
                            return;
                        }

                        uint bytesInCurrentPage = Math.Min((_pages[dataIndex].DataExtent - inPageOffset), (uint)remainingBytesToRead);

                        UIntPtr targetData = _pages[dataIndex++].Data;

                        CacheNativeMethods.Memory.memcpy(pInsertionPoint, UIntPtr.Add(targetData, (int)inPageOffset), new UIntPtr(bytesInCurrentPage));

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
                foreach (var (Lock, IsHeldAsUpgradeableReadLock) in acquiredLocks)
                {
                    if (Lock == targetLock)
                        sawOriginalLockInLockCollection = true;

                    if (IsHeldAsUpgradeableReadLock)
                        Lock.ExitUpgradeableReadLock();
                    else
                        Lock.ExitReadLock();
                }

                // Exit our originally acquire read lock if, in the process of mapping in cache pages, we didn't have to upgrade it to an upgradeable read lock (in which
                // case it would have been released by the loop above).
                if (!sawOriginalLockInLockCollection)
                    targetLock.ExitReadLock();
            }
        }

        public override bool GetDataFromAddressUntil(ulong address, byte[] terminatingSequence, out byte[] result)
        {
            uint offset = (uint)(address - _segmentData.VirtualAddress);

            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);
            int dataIndex = (int)(pageAlignedOffset / VirtualAllocPageSize);

            List<ReaderWriterLockSlim> locallyAcquiredLocks = new List<ReaderWriterLockSlim>
            {
                _pageLocks[dataIndex]
            };

            locallyAcquiredLocks[0].EnterReadLock();

            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = EnsurePageAtOffset(offset, locallyAcquiredLocks[0]);

            uint pageAdjustedOffset = MapOffsetToPageOffset(offset);

            List<byte> res = new List<byte>();

            try
            {
                CachePage curPage = _pages[dataIndex];
                UIntPtr curPageData = _pages[dataIndex].Data;
                while (true)
                {
                    for (uint i = pageAdjustedOffset; i < curPage.DataExtent;)
                    {
                        bool wasTerminatorMatch = true;
                        for (int j = 0; j < terminatingSequence.Length; j++)
                        {
                            unsafe
                            {
                                if (*(((byte*)(curPageData.ToPointer()) + (i + j))) != terminatingSequence[j])
                                {
                                    wasTerminatorMatch = false;
                                    break;
                                }
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
                            unsafe
                            {
                                res.Add(*((byte*)(curPageData.ToPointer()) + (i + j)));
                            }
                        }

                        i += (uint)terminatingSequence.Length;
                    }

                    // Ran out of data in this segment before we found the end of the sequence
                    if ((dataIndex + 1) == _pages.Length)
                    {
                        result = res.ToArray();
                        return false;
                    }

                    // no offsets when we jump to the next page of data
                    pageAdjustedOffset = 0;

                    offset += curPage.DataExtent;

                    locallyAcquiredLocks.Add(_pageLocks[dataIndex + 1]);
                    locallyAcquiredLocks[locallyAcquiredLocks.Count - 1].EnterReadLock();

                    acquiredLocks.AddRange(EnsurePageAtOffset(offset, locallyAcquiredLocks[locallyAcquiredLocks.Count - 1]));

                    curPage = _pages[++dataIndex];
                    if (curPage == null)
                    {
                        throw new InvalidOperationException($"CachePage at index {dataIndex} was null. EnsurePageAtOffset didn't work.");
                    }

                    curPageData = curPage.Data;
                }
            }
            finally
            {
                foreach (var (Lock, IsHeldAsUpgradeableReadLock) in acquiredLocks)
                {
                    locallyAcquiredLocks.Remove(Lock);

                    if (IsHeldAsUpgradeableReadLock)
                        Lock.ExitUpgradeableReadLock();
                    else
                        Lock.ExitReadLock();
                }

                foreach (ReaderWriterLockSlim remainingLock in locallyAcquiredLocks)
                    remainingLock.ExitReadLock();
            }
        }

        public override bool PageOutData()
        {
            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageOutDataStart();

            long sizeRemoved = 0;

            int maxLoopCount = 10;
            int pass = 0;
            for (; pass < maxLoopCount; pass++)
            {
                // Assume we will be able to evict all non-null pages
                bool encounteredBusyPage = false;

                for (int i = 0; i < _pages.Length; i++)
                {
                    ReaderWriterLockSlim pageLock = _pageLocks[i];
                    if (!pageLock.TryEnterWriteLock(timeout: TimeSpan.Zero))
                    {
                        // Someone holds the writelock on this page, skip it and try to get it in another pass, this prevent us from blocking page out
                        // on someone currently reading a page, they will likely be done by our next pass
                        encounteredBusyPage = true;
                        continue;
                    }

                    try
                    {
                        CachePage page = _pages[i];
                        if (page != null)
                        {
                            sizeRemoved += page.DataExtent;

                            // We need to unmap the physical memory from this VM range and then free the VM range
                            bool unmapPhysicalPagesResult = CacheNativeMethods.AWE.MapUserPhysicalPages(page.Data, numberOfPages: (uint)(VirtualAllocPageSize / SystemPageSize), pageArray: UIntPtr.Zero);
                            if (!unmapPhysicalPagesResult)
                            {
                                Debug.Fail("MapUserPhysicalPage failed to unmap a phsyical page");

                                // this is an error but we don't want to remove the ptr entry since we apparently didn't unmap the physical memory
                                continue;
                            }

                            bool virtualFreeRes = CacheNativeMethods.Memory.VirtualFree(page.Data, sizeToFree: UIntPtr.Zero, CacheNativeMethods.Memory.VirtualFreeType.Release);
                            if (!virtualFreeRes)
                            {
                                Debug.Fail("MapUserPhysicalPage failed to unmap a phsyical page");

                                // this is an error but we already unmapped the physical memory so also throw away our VM pointer
                                _pages[i] = null;

                                continue;
                            }

                            // Done, throw away our VM pointer
                            _pages[i] = null;
                        }
                    }
                    finally
                    {
                        pageLock.ExitWriteLock();
                    }
                }

                // We are done if we didn't encounter any busy pages during our attempt
                if (!encounteredBusyPage)
                    break;
            }

            // Revert to our minimum size
            CurrentSize = MinSize;

            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageOutDataEnd(sizeRemoved);

            return true;
        }

        public override void UpdateLastAccessTickCount()
        {
            long originalTickCountValue = Interlocked.Read(ref _lastAccessTickCount);

            while (true)
            {
                CacheNativeMethods.Util.QueryPerformanceCounter(out long currentTickCount);
                if (Interlocked.CompareExchange(ref _lastAccessTickCount, currentTickCount, originalTickCountValue) == originalTickCountValue)
                {
                    break;
                }

                originalTickCountValue = Interlocked.Read(ref _lastAccessTickCount);
            }
        }

        private static uint AlignOffsetToPageBoundary(uint offset)
        {
            if ((offset % VirtualAllocPageSize) != 0)
                return offset - offset % VirtualAllocPageSize;

            return offset;
        }

        private static uint MapOffsetToPageOffset(uint offset)
        {
            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);
            int pageIndex = (int)(pageAlignedOffset / VirtualAllocPageSize);
            return offset - ((uint)pageIndex * VirtualAllocPageSize);
        }

        private List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> EnsurePageAtOffset(uint offset, ReaderWriterLockSlim acquiredReadLock)
        {
            return EnsurePageRangeAtOffset(offset, acquiredReadLock, VirtualAllocPageSize);
        }

        private List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> EnsurePageRangeAtOffset(uint offset, ReaderWriterLockSlim originalReadLock, uint bytesNeeded)
        {
            List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)> acquiredLocks = new List<(ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock)>();

            uint pageAlignedOffset = AlignOffsetToPageBoundary(offset);

            int dataIndex = (int)(pageAlignedOffset / VirtualAllocPageSize);

            if (_pages[dataIndex] == null)
            {
                // THREADING: Our contract is the caller must have acquired the read lock at least for this first page, this is because the caller needs
                // to ensure the page cannot be evicted even after we return (presumably they want to read data from it). However, before we page in a
                // currently null page, we have to acquire the write lock. So we acquire an upgradeable read lock on this index, and then double check if
                // the page entry hasn't been set by someone who beat us to the write lock. We will also return this upgraded lock in the collection of
                // upgraded locks we have acquired so the caller can release it when they are done reading the page(s)
                originalReadLock.ExitReadLock();
                originalReadLock.EnterUpgradeableReadLock();
                originalReadLock.EnterWriteLock();

                try
                {
                    if (_pages[dataIndex] == null)
                    {
                        UIntPtr pData = GetPageAtOffset(pageAlignedOffset, out uint dataRange);

                        _pages[dataIndex] = new CachePage(pData, dataRange);
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
            uint bytesAvailableOnPage = _pages[dataIndex].DataExtent - (offset - pageAlignedOffset);
            if (bytesAvailableOnPage < bytesNeeded)
            {
                // if bytesAvailableOnPage < bytesNeeded it means the read will cover multiple cache pages, so we need to also fault in any following cache 
                // pages
                int bytesRemaining = (int)bytesNeeded - (int)bytesAvailableOnPage;
                do
                {
                    // Out of data for this memory segment, it may be the case that the read crosses between memory segments, so return any info on any
                    // upgraded locks we have acquired thus far
                    if ((dataIndex + 1) == _pages.Length)
                    {
                        return acquiredLocks;
                    }

                    pageAlignedOffset += VirtualAllocPageSize;

                    // Take a read lock on the next page entry
                    originalReadLock = _pageLocks[dataIndex + 1];
                    originalReadLock.EnterReadLock();

                    if (_pages[dataIndex + 1] != null)
                    {
                        bytesRemaining -= (int)_pages[++dataIndex].DataExtent;

                        acquiredLocks.Add((originalReadLock, IsHeldAsUpgradeableReadLock: false));
                        continue;
                    }

                    // THREADING: We know the entry must have been null or we would have continued above, so go ahead and enter as an upgradeable lock and
                    // acquire the write lock
                    originalReadLock.ExitReadLock();
                    originalReadLock.EnterUpgradeableReadLock();
                    originalReadLock.EnterWriteLock();

                    if (_pages[dataIndex + 1] == null)
                    {
                        // Still not set, so we will set it now

                        try
                        {
                            UIntPtr pData = GetPageAtOffset(pageAlignedOffset, out uint dataRange);

                            _pages[++dataIndex] = new CachePage(pData, dataRange);

                            bytesRemaining -= (int)dataRange;
                        }
                        catch (Exception)
                        {
                            // THREADING: If we see an exception here we are going to rethrow, which means or caller won't be able to release the upgraded read lock, so do it here
                            // as to not leave this page permanently locked out
                            originalReadLock.ExitWriteLock();
                            originalReadLock.ExitUpgradeableReadLock();

                            // Drop any read locks we have taken up to this point as our caller won't be able to do that since we are re-throwing
                            foreach ((ReaderWriterLockSlim Lock, bool IsHeldAsUpgradeableReadLock) in acquiredLocks)
                            {
                                if (IsHeldAsUpgradeableReadLock)
                                    Lock.ExitUpgradeableReadLock();
                                else
                                    Lock.ExitReadLock();
                            }

                            throw;
                        }
                    }
                    else // someone else beat us to filling this page in, extract the data we need
                    {
                        bytesRemaining -= (int)_pages[++dataIndex].DataExtent;
                    }

                    // THREADING: Exit our write lock as we either wrote the entry or someone else did, but keep our read lock so the page can't be
                    // evicted before the caller can read it
                    originalReadLock.ExitWriteLock();
                    acquiredLocks.Add((originalReadLock, IsHeldAsUpgradeableReadLock: true));
                } while (bytesRemaining > 0);
            }

            return acquiredLocks;
        }

        private UIntPtr GetPageAtOffset(uint offset, out uint dataExtent)
        {
            uint readSize;
            if ((offset + VirtualAllocPageSize) <= (int)_segmentData.Size)
            {
                readSize = VirtualAllocPageSize;
            }
            else
            {
                readSize = (uint)_segmentData.Size - offset;
            }

            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageInDataStart((long)(_segmentData.VirtualAddress + offset), readSize);

            dataExtent = readSize;

            int memoryPageNumber = (int)(offset / SystemPageSize);

            try
            {
                // Allocate a VM range to map the physical memory into
                UIntPtr vmPtr = CacheNativeMethods.Memory.VirtualAlloc(VirtualAllocPageSize, CacheNativeMethods.Memory.VirtualAllocType.Reserve | CacheNativeMethods.Memory.VirtualAllocType.Physical, CacheNativeMethods.Memory.MemoryProtection.ReadWrite);
                if (vmPtr == UIntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                // Map pages of our physical memory into the VM space, we have to adjust the pageFrameArray pointer as MapUserPhysicalPages only takes a page count and a page frame array starting point
                uint numberOfPages = readSize / (uint)Environment.SystemPageSize + (((readSize % Environment.SystemPageSize) == 0) ? 0u : 1u);
                bool mapPhysicalPagesResult = CacheNativeMethods.AWE.MapUserPhysicalPages(vmPtr, numberOfPages: numberOfPages, _pageFrameArray + (memoryPageNumber * UIntPtr.Size));
                if (!mapPhysicalPagesResult)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                UpdateLastAccessTickCount();
                CurrentSize += readSize;

                // NOTE: We call back under lock, non-ideal but the callback should NOT be modifying this entry in any way
                _updateOwningCacheForSizeChangeCallback(_segmentData.VirtualAddress, readSize);

                if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataEnd((int)readSize);

                return vmPtr;
            }
            catch (Exception ex)
            {
                if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataFailed(ex.Message);

                throw;
            }
        }

        private bool IsSinglePageRead(uint offset, uint byteCount)
        {
            uint alignedOffset = AlignOffsetToPageBoundary(offset);

            int dataIndex = (int)(alignedOffset / VirtualAllocPageSize);

            CachePage startingPage = _pages[dataIndex];
            if (startingPage == null)
            {
                throw new InvalidOperationException($"Inside IsSinglePageRead but the page at index {dataIndex} is null. You need to call EnsurePageAtOffset or EnsurePageRangeAtOffset before calling this method.");
            }

            uint inPageOffset = MapOffsetToPageOffset(offset);

            return (inPageOffset + byteCount) < startingPage.DataExtent;
        }

        public void Dispose()
        {
            if (_pages != null)
            {
                for (int i = 0; i < _pages.Length; i++)
                {
                    ReaderWriterLockSlim pageLock = _pageLocks[i];
                    pageLock.EnterWriteLock();

                    try
                    {
                        CachePage page = _pages[i];
                        if (page != null)
                        {
                            // We need to unmap the physical memory from this VM range and then free the VM range
                            bool unmapPhysicalPagesResult = CacheNativeMethods.AWE.MapUserPhysicalPages(page.Data, numberOfPages: (uint)(VirtualAllocPageSize / Environment.SystemPageSize), pageArray: UIntPtr.Zero);
                            if (!unmapPhysicalPagesResult)
                            {
                                Debug.Fail("MapUserPhysicalPage failed to unmap a phsyical page");

                                // this is an error but we don't want to remove the ptr entry since we apparently didn't unmap the physical memory
                                continue;
                            }

                            bool virtualFreeRes = CacheNativeMethods.Memory.VirtualFree(page.Data, sizeToFree: UIntPtr.Zero, CacheNativeMethods.Memory.VirtualFreeType.Release);
                            if (!virtualFreeRes)
                            {
                                Debug.Fail("MapUserPhysicalPage failed to unmap a phsyical page");

                                // this is an error but we already unmapped the physical memory so also throw away our VM pointer
                                _pages[i] = null;

                                continue;
                            }

                            // Done, throw away our VM pointer
                            _pages[i] = null;
                        }
                    }
                    finally
                    {
                        pageLock.ExitWriteLock();
                    }
                }


                uint numberOfPagesToFree = (uint)_pageFrameArrayItemCount;
                bool freeUserPhyiscalPagesRes = CacheNativeMethods.AWE.FreeUserPhysicalPages(ref numberOfPagesToFree, _pageFrameArray);
                if (!freeUserPhyiscalPagesRes)
                {
                    Debug.Fail("Failed tp free our physical pages");
                }

                if (numberOfPagesToFree != _pageFrameArrayItemCount)
                {
                    Debug.Fail("Failed to free ALL of our physical pages");
                }

                // Free our page frame array
                CacheNativeMethods.Memory.HeapFree(_pageFrameArray);
                _pageFrameArray = UIntPtr.Zero;

                _pages = null;
            }
        }

        [DebuggerDisplay("Size: {DataExtent}")]
        internal sealed class CachePage
        {
            internal CachePage(UIntPtr data, uint dataExtent)
            {
                Data = data;
                DataExtent = dataExtent;
            }

            public UIntPtr Data { get; }
            public uint DataExtent { get; }
        }
    }
}
