// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// TODO:  This code wasn't written to consider nullable.
#nullable disable

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// Represents heap segment cache entries backed by arrays from ArrayPool{byte}.Shared. This technology is less efficient than the <see cref="AWEBasedCacheEntry"/> but it has upsides
    /// around not requiring special privileges and mapping in a more granular fashion (4k pages vs 64k pages).
    /// </summary>

    internal sealed class ArrayPoolBasedCacheEntry : CacheEntryBase<byte[]>
    {
        private static readonly uint SystemPageSize = (uint)Environment.SystemPageSize;

        private SafeFileHandle _fileHandle;
        private string _dumpFileLocation;

        internal ArrayPoolBasedCacheEntry(string dumpFileLocation, MinidumpSegment segmentData, Action<uint> updateOwningCacheForAddedChunk) : base(segmentData, derivedMinSize: 2 * IntPtr.Size, updateOwningCacheForAddedChunk)
        {
            _dumpFileLocation = dumpFileLocation;
            _fileHandle = new SafeFileHandle(CacheNativeMethods.File.CreateFile(_dumpFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read), ownsHandle: true);
            if(_fileHandle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override long PageOutData()
        {
            ThrowIfDisposed();

            var data = TryRemoveAllPagesFromCache(disposeLocks: false);

            int oldCurrent;
            int newCurrent;
            do
            {
                oldCurrent = (int)_entrySize;
                newCurrent = Math.Max((int)MinSize, oldCurrent - (int)data.DataRemoved);
            }
            while (Interlocked.CompareExchange(ref _entrySize, newCurrent, oldCurrent) != oldCurrent);

            return data.DataRemoved;
        }

        protected override uint EntryPageSize => SystemPageSize;

        protected override (byte[] Data, uint DataExtent) GetPageDataAtOffset(uint pageAlignedOffset)
        {
            // NOTE: The caller ensures this method is not called concurrently

            if (HeapSegmentCacheEventSource.Instance.IsEnabled())
                HeapSegmentCacheEventSource.Instance.PageInDataStart((long)(_segmentData.VirtualAddress + pageAlignedOffset), EntryPageSize);

            uint readSize;
            if ((pageAlignedOffset + EntryPageSize) <= (int)_segmentData.Size)
            {
                readSize = EntryPageSize;
            }
            else
            {
                readSize = (uint)_segmentData.Size - pageAlignedOffset;
            }

            bool pageInFailed = false;
            try
            {
                if(!CacheNativeMethods.File.SetFilePointerEx(_fileHandle, (long)(_segmentData.FileOffset + pageAlignedOffset), SeekOrigin.Begin))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                unsafe
                {
                    // Grab a shared buffer to use if there is one, or create one for the pool
                    byte[] data = ArrayPool<byte>.Shared.Rent((int)readSize);

                    fixed (byte* pData = data)
                    {
                        uint bytesRead;
                        if(!CacheNativeMethods.File.ReadFile(_fileHandle, new IntPtr(pData), readSize, out bytesRead))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    return (data, readSize);
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

        protected unsafe override void InvokeCallbackWithDataPtr(CachePage<byte[]> page, Action<UIntPtr, uint> callback)
        {
            fixed (byte* pBuffer = page.Data)
            {
                callback(new UIntPtr(pBuffer), page.DataExtent);
            }
        }

        protected override void Dispose(bool disposing)
        {
            for (int i = 0; i < 3; i++)
            {
                if (TryRemoveAllPagesFromCache(disposeLocks: true).ItemsSkipped == 0)
                {
                    break;
                }
            }

            if (_fileHandle != null)
            {
                _fileHandle.Dispose();
                _fileHandle = null;
            }
        }

        private (uint DataRemoved, uint ItemsSkipped) TryRemoveAllPagesFromCache(bool disposeLocks)
        {
            // Assume we will be able to evict all non-null pages
            uint dataRemoved = 0;
            uint itemsSkipped = 0;

            for (int i = 0; i < _pages.Length; i++)
            {
                CachePage<byte[]> page = _pages[i];
                if (page != null)
                {
                    ReaderWriterLockSlim dataChunkLock = _pageLocks[i];
                    if (!dataChunkLock.TryEnterWriteLock(timeout: TimeSpan.Zero))
                    {
                        // Someone holds a read or write lock on this page, skip it
                        itemsSkipped++;
                        continue;
                    }

                    try
                    {
                        // double check that no other thread already scavenged this entry
                        page = _pages[i];
                        if (page != null)
                        {
                            ArrayPool<byte>.Shared.Return(page.Data);
                            dataRemoved += page.DataExtent;
                            _pages[i] = null;
                        }
                    }
                    finally
                    {
                        dataChunkLock.ExitWriteLock();
                        if (disposeLocks)
                        {
                            dataChunkLock.Dispose();
                            _pageLocks[i] = null;
                        }
                    }
                }
            }

            return (dataRemoved, itemsSkipped);
        }
    }
}
