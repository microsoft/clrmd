// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal class HeapSegmentDataCache : IDisposable
    {
        private ReaderWriterLockSlim cacheLock;
        private readonly IDictionary<ulong, ISegmentCacheEntry> cache;
        private ISegmentCacheEntryFactory entryFactory;

        private long cacheSize;
        private readonly long maxSize;

        internal HeapSegmentDataCache(ISegmentCacheEntryFactory entryFactory, long maxSize)
        {
            this.cacheLock = new ReaderWriterLockSlim();
            this.cache = new Dictionary<ulong, ISegmentCacheEntry>();

            this.entryFactory = entryFactory;
            this.maxSize = maxSize;
        }

        internal ISegmentCacheEntry CreateAndAddEntry(MinidumpSegment segment)
        {
            ISegmentCacheEntry entry = this.entryFactory.CreateEntryForSegment(segment, this.UpdateOverallCacheSizeForAddedChunk);
            this.cacheLock.EnterWriteLock();
            try
            {
                // Check the cache again now that we have acquired the write lock
                ISegmentCacheEntry existingEntry;
                if (this.cache.TryGetValue(segment.VirtualAddress, out existingEntry))
                {
                    // Someone else beat us to adding this entry, clean up the entry we created and return the existing one
                    using (entry as IDisposable)
                    {
                        return existingEntry;
                    }
                }

                this.cache.Add(segment.VirtualAddress, entry);
            }
            finally
            {
                this.cacheLock.ExitWriteLock();
            }

            Interlocked.Add(ref this.cacheSize, entry.CurrentSize);
            this.TrimCacheIfOverLimit(segment.VirtualAddress);

            return entry;
        }

        internal bool TryGetCacheEntry(ulong baseAddress, out ISegmentCacheEntry entry)
        {
            this.cacheLock.EnterReadLock();
            bool res = false;

            try
            {
                res = this.cache.TryGetValue(baseAddress, out entry);
            }
            finally
            {
                this.cacheLock.ExitReadLock();
            }

            if (res)
            {
                entry.UpdateLastAccessTickCount();
            }

            return res;
        }

        private void UpdateOverallCacheSizeForAddedChunk(ulong modifiedSegmentAddress, uint chunkSize)
        {
            Interlocked.Add(ref this.cacheSize, chunkSize);

            TrimCacheIfOverLimit(modifiedSegmentAddress);
        }

        private void TrimCacheIfOverLimit(ulong modifiedSegmentAddress)
        {
            if (Interlocked.Read(ref this.cacheSize) < this.maxSize)
                return;

            // Select all cache entries which aren't at their min-size
            //
            // NOTE: We snapshot the LastAccessTickCount values here because there is the case where the Sort function will throw an exception if it tests two entries and the 
            // lhs rhs comparison is inconsistent when reveresed (i.e. something like lhs < rhs is true but then rhs < lhs is also true). This sound illogical BUT it can happen
            // if between the two comparisons the LastAccessTickCount changes (because other threads are concurrently accessing these same entries), in that case we would trigger 
            // this exception, which is bad :)
            IEnumerable<(KeyValuePair<ulong, ISegmentCacheEntry> CacheEntry, long LastAccessTickCount)> items = null;
            List<(KeyValuePair<ulong, ISegmentCacheEntry> CacheEntry, long LastAccessTickCount)> entries = null;

            this.cacheLock.EnterReadLock();
            try
            {
                items = this.cache.Where((kvp) => kvp.Value.CurrentSize != kvp.Value.MinSize).Select((kvp) => (CacheEntry: kvp, kvp.Value.LastAccessTickCount));
                entries = new List<(KeyValuePair<ulong, ISegmentCacheEntry> CacheEntry, long LastAccessTickCount)>(items);
            }
            finally
            {
                this.cacheLock.ExitReadLock();
            }

            // Flip the sort order to the LEAST recently accessed items (i.e. the ones whose LastAccessTickCount are furthest in history) end up at the END of the array,
            //
            // NOTE: Using tickcounts is succeptible to roll-over, but worst case scenario we remove a slightly more recently used one thinking it is older, not a huge deal
            // and using DateTime.Now to get a non-roll-over succeptible timestamp showed up as 5% of scenario time in PerfView :(
            entries.Sort((lhs, rhs) => rhs.LastAccessTickCount.CompareTo(lhs.LastAccessTickCount));

            // Try to cut ourselves down to about 85% of our max capaity, otherwise just hang out right at that boundary and the next entry we add we end up having to
            // scavenge again, and again, and again...
            uint requiredCutAmount = (uint)(this.maxSize * 0.15);

            long desiredSize = (long)(this.maxSize * 0.85);

            uint cutAmount = 0;
            while (cutAmount < requiredCutAmount)
            {
                // We could also be trimming on other threads, so if collectively we have brought ourselves below 85% of our max capacity then we are done
                if (Interlocked.Read(ref this.cacheSize) <= desiredSize)
                    break;

                // find the largest item of the 10% of least recently accessed (remaining) items
                uint largestSizeSeen = 0;
                int curItemIndex = (entries.Count - 1) - (int)(entries.Count * 0.10);

                int removalTargetIndex = -1;
                while (curItemIndex < entries.Count)
                {
                    KeyValuePair<ulong, ISegmentCacheEntry> curItem = entries[curItemIndex].CacheEntry;

                    // >= so we prefer the largest item that is least recently accessed, ensuring we don't remove a segment that is being actively modified now (should
                    // never happen since we also update that segments accessed timestamp, but, defense in depth).
                    //
                    // NOTE: We subtract MinSize from CurrentSize assuming the cache will be able to page out its data and thus we won't
                    // actually remove its entry below. If not we will correct this value later in the 'we remove the entire cache entry'
                    // block (the !PageOutData block below).
                    if ((curItem.Value.CurrentSize - curItem.Value.MinSize) >= largestSizeSeen && (curItem.Key != modifiedSegmentAddress))
                    {
                        largestSizeSeen = (curItem.Value.CurrentSize - curItem.Value.MinSize);
                        removalTargetIndex = curItemIndex;
                    }

                    curItemIndex++;
                }

                ISegmentCacheEntry targetItem = entries[removalTargetIndex].CacheEntry.Value;

                bool removedMemUsedByItem = true;

                // Prefer paging out the data if we can, this is less drastic than throwing it away (ala Dispose), if we can't page it out 
                // though remove and dispose of it
                if (!targetItem.PageOutData())
                {
                    long sizeRemoved = targetItem.CurrentSize;

                    // NOTE: We reset largestSizeSeen here because the entire entry is being removed so there is no remaining 'min' size like there 
                    // would be in entries that paged out their data.
                    largestSizeSeen = targetItem.CurrentSize;

                    if(HeapSegmentCacheEventSource.Instance.IsEnabled())
                        HeapSegmentCacheEventSource.Instance.PageOutDataStart();

                    using (targetItem as IDisposable)
                    {
                        this.cacheLock.EnterWriteLock();
                        try
                        {
                            removedMemUsedByItem = this.cache.Remove(entries[removalTargetIndex].CacheEntry.Key);
                        }
                        finally
                        {
                            this.cacheLock.ExitWriteLock();

                            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                                HeapSegmentCacheEventSource.Instance.PageOutDataEnd(sizeRemoved);
                        }
                    }
                }

                // Remove the entry from our local collection of entries as either way (data paged out or item removed from cache or item already 
                // removed from cache on another thread) the size of the entry is now essentially 0 for further trimming purposes
                entries.RemoveAt(removalTargetIndex);
                if (removedMemUsedByItem)
                {
                    Interlocked.Add(ref this.cacheSize, -largestSizeSeen);
                    cutAmount += largestSizeSeen;
                }
            }
        }

        public void Dispose()
        {
            using (this.entryFactory as IDisposable)
            {
                this.cacheLock.EnterWriteLock();
                try
                {
                    foreach (KeyValuePair<ulong, ISegmentCacheEntry> kvp in this.cache)
                    {
                        (kvp.Value as IDisposable)?.Dispose();
                    }

                    this.cache.Clear();
                }
                finally
                {
                    this.cacheLock.ExitWriteLock();
                }
            }
        }
    }
}