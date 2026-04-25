// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    /// <summary>
    /// A lock-free, single-threaded <see cref="IDataReader"/> that satisfies memory reads
    /// directly from a memory-mapped view of the dump file. Eliminates per-read locks and
    /// stream seeks compared to the default cached/uncached minidump readers.
    ///
    /// <para>
    /// Construction strategy: the inner <see cref="IDataReader"/> (a normal
    /// <c>MinidumpReader</c>, <c>CoredumpReader</c>, or <c>MachOCoreReader</c>) is created
    /// first so the existing parsers do all format-specific work. The lock-free reader then
    /// pulls the resulting (VA, FileOffset, Size) segment table via
    /// <see cref="IDumpFileMemorySource"/> — no parsing logic is duplicated.
    /// </para>
    ///
    /// <para>
    /// Hot path: a sorted segment array + a single-slot last-used-segment cache provide
    /// O(1) access for the dominant sequential-read pattern (heap walks, GC root traversal),
    /// falling back to binary search for non-local addresses.
    /// </para>
    ///
    /// <para>
    /// All non-memory operations (modules, thread contexts, OS thread enumeration) delegate
    /// to the inner reader, which retains its own access to the dump.
    /// </para>
    /// </summary>
    internal sealed unsafe class LockFreeMmfDataReader : IDataReader, IThreadReader, IDumpInfoProvider, ISegmentedDirectMemoryAccess, IDisposable
    {
        private readonly IDataReader _inner;
        private readonly IThreadReader? _innerThreads;
        private readonly IDumpInfoProvider? _innerInfo;
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly byte* _basePtr;
        private readonly long _viewLength;
        private readonly Segment[] _segments;
        private int _lastSegmentIndex;
        private bool _disposed;

        private readonly struct Segment
        {
            public readonly ulong VirtualAddress;
            public readonly ulong Size;
            public readonly ulong FileOffset;

            public Segment(ulong virtualAddress, ulong size, ulong fileOffset)
            {
                VirtualAddress = virtualAddress;
                Size = size;
                FileOffset = fileOffset;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(ulong address) => address >= VirtualAddress && address - VirtualAddress < Size;
        }

        /// <summary>
        /// Wraps <paramref name="inner"/> with a memory-mapped, lock-free read path over
        /// the same dump file.
        /// </summary>
        /// <param name="dumpPath">Path to the dump file on disk.</param>
        /// <param name="inner">An inner reader that also implements <see cref="IDumpFileMemorySource"/>.</param>
        public LockFreeMmfDataReader(string dumpPath, IDataReader inner)
        {
            if (dumpPath is null)
                throw new ArgumentNullException(nameof(dumpPath));
            if (inner is null)
                throw new ArgumentNullException(nameof(inner));

            if (inner is not IDumpFileMemorySource segmentSource)
                throw new ArgumentException(
                    $"Inner reader of type {inner.GetType().Name} does not implement {nameof(IDumpFileMemorySource)}.",
                    nameof(inner));

            _inner = inner;
            _innerThreads = inner as IThreadReader;
            _innerInfo = inner as IDumpInfoProvider;

            MemoryMappedFile? mmf = null;
            MemoryMappedViewAccessor? accessor = null;
            try
            {
                mmf = MemoryMappedFile.CreateFromFile(dumpPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                _viewLength = (long)accessor.SafeMemoryMappedViewHandle.ByteLength;

                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                _basePtr = ptr + accessor.PointerOffset;

                _mmf = mmf;
                _accessor = accessor;
            }
            catch (Exception ex) when (IsAddressSpaceExhaustion(ex))
            {
                accessor?.Dispose();
                mmf?.Dispose();
                throw new OutOfMemoryException(BuildOomMessage(dumpPath), ex);
            }

            _segments = BuildSegments(segmentSource.EnumerateMemorySegments());
        }

        private static bool IsAddressSpaceExhaustion(Exception ex)
            => ex is OutOfMemoryException or IOException;

        private static string BuildOomMessage(string dumpPath)
        {
            long fileSize = -1;
            try { fileSize = new FileInfo(dumpPath).Length; } catch { }

            string sizeNote = fileSize >= 0
                ? $" (dump file is {fileSize / (1024.0 * 1024.0 * 1024.0):F2} GB)"
                : string.Empty;

            if (!Environment.Is64BitProcess)
            {
                return $"Failed to memory-map dump file{sizeNote}. " +
                    $"This is most likely caused by 32-bit address space exhaustion: " +
                    $"{nameof(DataTargetOptions)}.{nameof(DataTargetOptions.UseLockFreeMemoryMapReader)} " +
                    $"maps the entire dump file into the current process. " +
                    $"Disable {nameof(DataTargetOptions.UseLockFreeMemoryMapReader)} (the default) " +
                    $"or run as a 64-bit process to load large dumps.";
            }

            return $"Failed to memory-map dump file{sizeNote} while initializing the lock-free " +
                $"memory-mapped data reader ({nameof(DataTargetOptions.UseLockFreeMemoryMapReader)}). " +
                $"Disable {nameof(DataTargetOptions.UseLockFreeMemoryMapReader)} (the default) to fall back " +
                $"to the streaming reader.";
        }

        private Segment[] BuildSegments(IReadOnlyList<DumpMemorySegment> source)
        {
            int count = source.Count;
            Segment[] result = new Segment[count];
            for (int i = 0; i < count; i++)
            {
                DumpMemorySegment s = source[i];
                if (s.FileOffset > (ulong)_viewLength || s.FileOffset + s.Size > (ulong)_viewLength)
                {
                    // Truncate / skip out-of-range entries rather than fail outright; the
                    // inner reader can fall back if needed for cold paths (we never invoke it
                    // for memory reads, so an empty slot just produces a 0-byte read).
                    result[i] = new Segment(s.VirtualAddress, 0, s.FileOffset);
                    continue;
                }
                result[i] = new Segment(s.VirtualAddress, s.Size, s.FileOffset);
            }

            // The contract of IDumpFileMemorySource requires sorted-by-VA, but be defensive.
            Array.Sort(result, static (a, b) => a.VirtualAddress.CompareTo(b.VirtualAddress));
            return result;
        }

        // ============================================================
        // IMemoryReader — hot path (lock-free pointer reads)
        // ============================================================

        public int PointerSize => _inner.PointerSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(ulong address, Span<byte> buffer)
        {
            if (buffer.Length == 0)
                return 0;

            int idx;
            int lastIdx = _lastSegmentIndex;
            if ((uint)lastIdx < (uint)_segments.Length && _segments[lastIdx].Contains(address))
            {
                idx = lastIdx;
            }
            else
            {
                idx = FindSegment(address);
                if (idx < 0)
                    return 0;
                _lastSegmentIndex = idx;
            }

            int bytesRead = ReadFromSegment(ref _segments[idx], address, buffer);

            // Span adjacent segments transparently — matches the existing minidump readers.
            while (bytesRead < buffer.Length)
            {
                int nextIdx = idx + 1;
                if ((uint)nextIdx >= (uint)_segments.Length)
                    break;

                ulong nextAddress = address + (ulong)bytesRead;
                ref Segment nextSeg = ref _segments[nextIdx];
                if (!nextSeg.Contains(nextAddress))
                    break;

                int additional = ReadFromSegment(ref nextSeg, nextAddress, buffer.Slice(bytesRead));
                if (additional == 0)
                    break;

                bytesRead += additional;
                idx = nextIdx;
                _lastSegmentIndex = idx;
            }

            return bytesRead;
        }

        public bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            value = default;
            Span<byte> buffer = new(Unsafe.AsPointer(ref value), Unsafe.SizeOf<T>());
            return Read(address, buffer) == buffer.Length;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            value = 0;
            Span<byte> buffer = new(Unsafe.AsPointer(ref value), PointerSize);
            return Read(address, buffer) == PointerSize;
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadFromSegment(ref Segment seg, ulong address, Span<byte> buffer)
        {
            ulong offsetInSeg = address - seg.VirtualAddress;
            ulong availableU = seg.Size - offsetInSeg;
            if (availableU == 0)
                return 0;

            int toRead = buffer.Length;
            if (availableU < (ulong)toRead)
                toRead = (int)availableU;

            ulong fileOffset = seg.FileOffset + offsetInSeg;
            new ReadOnlySpan<byte>(_basePtr + (long)fileOffset, toRead).CopyTo(buffer);
            return toRead;
        }

        // ============================================================
        // ISegmentedDirectMemoryAccess — zero-copy hot path
        // ============================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetDirectSpan(ulong address, int length, out ReadOnlySpan<byte> span)
        {
            if (length == 0)
            {
                span = default;
                return true;
            }

            if (length < 0)
            {
                span = default;
                return false;
            }

            int idx;
            int lastIdx = _lastSegmentIndex;
            if ((uint)lastIdx < (uint)_segments.Length && _segments[lastIdx].Contains(address))
            {
                idx = lastIdx;
            }
            else
            {
                idx = FindSegment(address);
                if (idx < 0)
                {
                    span = default;
                    return false;
                }
                _lastSegmentIndex = idx;
            }

            ref Segment seg = ref _segments[idx];
            ulong offsetInSeg = address - seg.VirtualAddress;
            ulong availableU = seg.Size - offsetInSeg;
            if (availableU < (ulong)length)
            {
                // Range crosses a segment boundary (or extends past the segment end);
                // caller falls back to Read which transparently spans adjacent segments.
                span = default;
                return false;
            }

            ulong fileOffset = seg.FileOffset + offsetInSeg;
            if (fileOffset > (ulong)_viewLength || fileOffset + (ulong)length > (ulong)_viewLength)
            {
                span = default;
                return false;
            }

            span = new ReadOnlySpan<byte>(_basePtr + (long)fileOffset, length);
            return true;
        }

        private int FindSegment(ulong address)
        {
            int lo = 0;
            int hi = _segments.Length - 1;

            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                ref Segment seg = ref _segments[mid];

                if (address < seg.VirtualAddress)
                    hi = mid - 1;
                else if (!seg.Contains(address))
                    lo = mid + 1;
                else
                    return mid;
            }

            return -1;
        }

        // ============================================================
        // IDataReader — cold path (delegated to inner)
        // ============================================================

        public string DisplayName => _inner.DisplayName;
        public bool IsThreadSafe => false;
        public OSPlatform TargetPlatform => _inner.TargetPlatform;
        public Architecture Architecture => _inner.Architecture;
        public int ProcessId => _inner.ProcessId;

        public IEnumerable<ModuleInfo> EnumerateModules() => _inner.EnumerateModules();

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
            => _inner.GetThreadContext(threadID, contextFlags, context);

        public void FlushCachedData() => _inner.FlushCachedData();

        // IThreadReader (passthrough so DAC OS thread enumeration keeps working)
        public IEnumerable<uint> EnumerateOSThreadIds()
            => _innerThreads?.EnumerateOSThreadIds() ?? Array.Empty<uint>();

        public ulong GetThreadTeb(uint osThreadId)
            => _innerThreads?.GetThreadTeb(osThreadId) ?? 0;

        // IDumpInfoProvider passthrough
        public bool IsMiniOrTriage => _innerInfo?.IsMiniOrTriage ?? false;
        public bool IsCreatedByDotNetRuntime => _innerInfo?.IsCreatedByDotNetRuntime ?? false;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_inner is IDisposable d)
                d.Dispose();
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _mmf.Dispose();
        }
    }
}
