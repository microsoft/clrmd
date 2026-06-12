// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Threading;

// TODO:  This code wasn't written to consider nullable.
#nullable disable

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// Represents heap segment cache entries backed by arrays from ArrayPool{byte}.Shared.
    /// </summary>

    internal sealed class ArrayPoolBasedCacheEntry : CacheEntryBase<byte[]>
    {
        private static readonly uint SystemPageSize = (uint)Environment.SystemPageSize;

        private readonly MemoryMappedFile _mappedFile;

        internal ArrayPoolBasedCacheEntry(MemoryMappedFile mappedFile, MinidumpSegment segmentData, Action<uint> updateOwningCacheForAddedChunk) : base(segmentData, derivedMinSize: 2 * IntPtr.Size, updateOwningCacheForAddedChunk)
        {
            _mappedFile = mappedFile;
        }

        public override long PageOutData()
        {
            ThrowIfDisposed();

            ulong dataRemoved = TryRemoveAllPagesFromCache();

            int oldCurrent;
            int newCurrent;
            do
            {
                oldCurrent = _entrySize;
                newCurrent = Math.Max(MinSize, oldCurrent - (int)dataRemoved);
            }
            while (Interlocked.CompareExchange(ref _entrySize, newCurrent, oldCurrent) != oldCurrent);

            return (long)dataRemoved;
        }

        protected override uint EntryPageSize => SystemPageSize;

        protected override (byte[] Data, ulong DataExtent) GetPageDataAtOffset(ulong pageAlignedOffset)
        {
            // NOTE: With the lock-free CompareExchange publish path, this method MAY
            // execute concurrently on the same offset from multiple threads. That is
            // safe here: each call acquires its own MemoryMappedViewAccessor and rents
            // its own buffer; only the CAS winner's buffer is retained, and the loser's
            // buffer is returned via DiscardUnusedPage.

            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageInDataStart((long)(_segmentData.VirtualAddress + pageAlignedOffset), EntryPageSize);

            uint readSize;
            if (pageAlignedOffset + EntryPageSize <= _segmentData.Size)
            {
                readSize = EntryPageSize;
            }
            else
            {
                readSize = (uint)(_segmentData.Size - pageAlignedOffset);
            }

            bool pageInFailed = false;
            using MemoryMappedViewAccessor view = _mappedFile.CreateViewAccessor((long)_segmentData.FileOffset + (long)pageAlignedOffset, size: readSize, MemoryMappedFileAccess.Read);
            try
            {
                ulong viewOffset = (ulong)view.PointerOffset;

                unsafe
                {
                    byte* pViewLoc = null;
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
                            CacheNativeMethods.Memory.memcpy(new UIntPtr(pData), new UIntPtr(pViewLoc), new UIntPtr(readSize));
                        }

                        return (data, readSize);
                    }
                    finally
                    {
                        if (pViewLoc != null)
                            view.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }
            catch (Exception ex)
            {
                if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataFailed(ex.Message);

                pageInFailed = true;
                throw;
            }
            finally
            {
                if (!pageInFailed && HeapSegmentCacheEventSource.Instance.IsEnabled())
                    HeapSegmentCacheEventSource.Instance.PageInDataEnd((int)readSize);
            }
        }

        protected override uint InvokeCallbackWithDataPtr(CachePage<byte[]> page, Func<UIntPtr, ulong, uint> callback)
        {
            unsafe
            {
                fixed (byte* pBuffer = page.Data)
                {
                    return callback(new UIntPtr(pBuffer), page.DataExtent);
                }
            }
        }

        protected override uint CopyDataFromPage(CachePage<byte[]> page, IntPtr buffer, ulong inPageOffset, uint byteCount)
        {
            // Calculate how much of the requested read can be satisfied by the page
            uint sizeRead = (uint)Math.Min(page.DataExtent - inPageOffset, byteCount);

            unsafe
            {
                fixed (byte* pData = page.Data)
                {
                    CacheNativeMethods.Memory.memcpy(buffer, new UIntPtr(pData + inPageOffset), new UIntPtr(sizeRead));
                }
            }

            return sizeRead;
        }

        protected override void DiscardUnusedPage(CachePage<byte[]> page)
        {
            // Loser of a publish-CAS: return the rented buffer to the pool.
            if (page.Data != null)
                ArrayPool<byte>.Shared.Return(page.Data);
        }

        protected override void Dispose(bool disposing)
        {
            TryRemoveAllPagesFromCache();
        }

        private ulong TryRemoveAllPagesFromCache()
        {
            ulong dataRemoved = 0;

            for (int i = 0; i < _pages.Length; i++)
            {
                // Atomically claim the slot. In-flight readers that already captured
                // the page reference (via Volatile.Read on the read path) may still be
                // inside CopyDataFromPage / InvokeCallbackWithDataPtr mid-memcpy on
                // page.Data. We intentionally do NOT return page.Data to
                // ArrayPool<byte>.Shared here: if we did, another thread could rent the
                // same buffer and overwrite it under that reader, producing corrupted
                // reads (e.g. zeroed MethodTable pointers leading to spurious null
                // ClrType lookups). Letting GC reclaim the array preserves the data
                // for any in-flight reader's lifetime. Eviction is rare relative to
                // the hot read path, so the lost pool reuse is acceptable.
                CachePage<byte[]> page = Interlocked.Exchange(ref _pages[i], null);
                if (page != null)
                    dataRemoved += page.DataExtent;
            }

            return dataRemoved;
        }
    }
}