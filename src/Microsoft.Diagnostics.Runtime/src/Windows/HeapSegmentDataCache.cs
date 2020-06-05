// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class HeapSegmentDataCache : IDisposable
    {
        private readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();
        private readonly Dictionary<ulong, SegmentCacheEntry> _cache = new Dictionary<ulong, SegmentCacheEntry>();
        private readonly SegmentCacheEntryFactory _entryFactory;

        private long _cacheSize;
        private readonly long _maxSize;

        public HeapSegmentDataCache(SegmentCacheEntryFactory entryFactory, long maxSize)
        {
            _entryFactory = entryFactory;
            _maxSize = maxSize;
        }

        public SegmentCacheEntry CreateAndAddEntry(MinidumpSegment segment)
        {
            SegmentCacheEntry entry = _entryFactory.CreateEntryForSegment(segment, UpdateOverallCacheSizeForAddedChunk);
            _cacheLock.EnterWriteLock();
            try
            {
                // Check the cache again now that we have acquired the write lock
                if (_cache.TryGetValue(segment.VirtualAddress, out SegmentCacheEntry? existingEntry))
                {
                    // Someone else beat us to adding this entry, clean up the entry we created and return the existing one
                    using (entry as IDisposable)
                        return existingEntry;
                }

                _cache.Add(segment.VirtualAddress, entry);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            Interlocked.Add(ref _cacheSize, entry.CurrentSize);
            TrimCacheIfOverLimit(segment.VirtualAddress);

            return entry;
        }

        public bool TryGetCacheEntry(ulong baseAddress, out SegmentCacheEntry? entry)
        {
            _cacheLock.EnterReadLock();
            bool res = false;

            try
            {
                res = _cache.TryGetValue(baseAddress, out entry);
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }

            entry?.UpdateLastAccessTickCount();
            return res;
        }

        private void UpdateOverallCacheSizeForAddedChunk(ulong modifiedSegmentAddress, uint chunkSize)
        {
            Interlocked.Add(ref _cacheSize, chunkSize);

            TrimCacheIfOverLimit(modifiedSegmentAddress);
        }

        private void TrimCacheIfOverLimit(ulong modifiedSegmentAddress)
        {
            if (Interlocked.Read(ref _cacheSize) < _maxSize)
                return;

            // Select all cache entries which aren't at their min-size
            //
            // NOTE: We snapshot the LastAccessTickCount values here because there is the case where the Sort function will throw an exception if it tests two entries and the 
            // lhs rhs comparison is inconsistent when reveresed (i.e. something like lhs < rhs is true but then rhs < lhs is also true). This sound illogical BUT it can happen
            // if between the two comparisons the LastAccessTickCount changes (because other threads are concurrently accessing these same entries), in that case we would trigger 
            // this exception, which is bad :)
            IEnumerable<(KeyValuePair<ulong, SegmentCacheEntry> CacheEntry, long LastAccessTickCount)>? items = null;
            List<(KeyValuePair<ulong, SegmentCacheEntry> CacheEntry, long LastAccessTickCount)>? entries = null;

            _cacheLock.EnterReadLock();
            try
            {
                items = _cache.Where((kvp) => kvp.Value.CurrentSize != kvp.Value.MinSize).Select((kvp) => (CacheEntry: kvp, kvp.Value.LastAccessTickCount));
                entries = new List<(KeyValuePair<ulong, SegmentCacheEntry> CacheEntry, long LastAccessTickCount)>(items);
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }

            // Flip the sort order to the LEAST recently accessed items (i.e. the ones whose LastAccessTickCount are furthest in history) end up at the END of the array,
            //
            // NOTE: Using tickcounts is succeptible to roll-over, but worst case scenario we remove a slightly more recently used one thinking it is older, not a huge deal
            // and using DateTime.Now to get a non-roll-over succeptible timestamp showed up as 5% of scenario time in PerfView :(
            entries.Sort((lhs, rhs) => rhs.LastAccessTickCount.CompareTo(lhs.LastAccessTickCount));

            // Try to cut ourselves down to about 85% of our max capaity, otherwise just hang out right at that boundary and the next entry we add we end up having to
            // scavenge again, and again, and again...
            uint requiredCutAmount = (uint)(_maxSize * 0.15);

            long desiredSize = (long)(_maxSize * 0.85);

            uint cutAmount = 0;
            while (cutAmount < requiredCutAmount)
            {
                // We could also be trimming on other threads, so if collectively we have brought ourselves below 85% of our max capacity then we are done
                if (Interlocked.Read(ref _cacheSize) <= desiredSize)
                    break;

                // find the largest item of the 10% of least recently accessed (remaining) items
                uint largestSizeSeen = 0;
                int curItemIndex = (entries.Count - 1) - (int)(entries.Count * 0.10);

                int removalTargetIndex = -1;
                while (curItemIndex < entries.Count)
                {
                    KeyValuePair<ulong, SegmentCacheEntry> curItem = entries[curItemIndex].CacheEntry;

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

                SegmentCacheEntry targetItem = entries[removalTargetIndex].CacheEntry.Value;

                bool removedMemUsedByItem = true;
                bool refreshEntries = false;

                // Prefer paging out the data if we can, this is less drastic than throwing it away (ala Dispose), if we can't page it out 
                // though remove and dispose of it
                long removedBytes = targetItem.PageOutData();
                if (removedBytes == 0)
                {
                    long sizeRemoved = targetItem.CurrentSize;

                    // NOTE: We reset largestSizeSeen here because the entire entry is being removed so there is no remaining 'min' size like there 
                    // would be in entries that paged out their data.
                    largestSizeSeen = targetItem.CurrentSize;

                    if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                        HeapSegmentCacheEventSource.Instance.PageOutDataStart();

                    _cacheLock.EnterWriteLock();
                    try
                    {
                        ulong targetKey = entries[removalTargetIndex].CacheEntry.Key;

                        SegmentCacheEntry? doubleCheckItem;
                        if (this._cache.TryGetValue(targetKey, out doubleCheckItem)) // make sure it hasn't been removed by another thread
                        {
                            // THREADING: If the entry in the cache is the SAME as when we snapshotted the item, then dispose of it. This closes
                            // a hole where between the time we snapshot and the time we get here another thread has removed the item and then,
                            // yet another thread, has added a NEW entry for that item back into the cache. If we simply blindly remove the cache 
                            // item we end up not disposing of it, which is bad.
                            if (targetItem == doubleCheckItem)
                            {
                                (targetItem as IDisposable)?.Dispose();
                                removedMemUsedByItem = this._cache.Remove(targetKey);
                            }
                            else
                            {
                                // The item in the cache is not the same as our snapshot, so we should re-snapshot
                                removedMemUsedByItem = false;
                                refreshEntries = true;
                            }
                        }
                        else
                        {
                            // The item we tried to remove doesn't exist any longer, so our snapshot data is clearly out of date
                            removedMemUsedByItem = false;
                            refreshEntries = true;
                        }

                        if(refreshEntries)
                        {
                            // THREADING: If the above if check fails we DO NOT want to remove the cache entry. The reason being we chose the target item
                            // because it was the best candidate amongst our snapshotted items, nothing says that the replaced item at the same key is
                            // the best item amongst the new collection of entries.

                            // Since there is at least ONE new entry in the cache since our snapshot was taken, retake the snapshot (since we already hold the
                            // write lock) so we aren't trimming against stale entries.
                            items = this._cache.Where((kvp) => kvp.Value.CurrentSize != kvp.Value.MinSize).Select((kvp) => (CacheEntry: kvp, kvp.Value.LastAccessTickCount));
                            entries = new List<(KeyValuePair<ulong, SegmentCacheEntry> CacheEntry, long LastAccessTickCount)>(items);
                        }
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();

                        if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                            HeapSegmentCacheEventSource.Instance.PageOutDataEnd(sizeRemoved);
                    }
                }

                if (!refreshEntries)
                {
                    // If we didn't refresh the entry collection then remove the entry from our local collection of entries as either way (data paged out or item removed from
                    // cache or item alreday removed from cache on another thread) the size of the entry is now essentially 0 for further trimming purposes.
                    entries.RemoveAt(removalTargetIndex);
                }

                if (removedMemUsedByItem)
                {
                    Interlocked.Add(ref _cacheSize, -removedBytes);
                    cutAmount += (uint)removedBytes;
                }
            }
        }

        public void Dispose()
        {
            using (_entryFactory as IDisposable)
            {
                _cacheLock.EnterWriteLock();
                try
                {
                    foreach (KeyValuePair<ulong, SegmentCacheEntry> kvp in _cache)
                    {
                        (kvp.Value as IDisposable)?.Dispose();
                    }

                    _cache.Clear();
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }

            _cacheLock.Dispose();
        }
    }
}