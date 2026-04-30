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
    internal sealed class LockFreeMmfDataReader : IDataReader, IThreadReader, IDumpInfoProvider, ISegmentedDirectMemoryAccess, IDisposable, IThreadInfoReader, IProcessInfoProvider, IMemoryRegionReader
    {
        private readonly IDataReader _inner;
        private readonly IThreadReader? _innerThreads;
        private readonly IDumpInfoProvider? _innerInfo;
        private readonly IThreadInfoReader? _innerThreadInfo;
        private readonly IProcessInfoProvider? _innerProcessInfo;
        private readonly IMemoryRegionReader? _innerRegions;
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        // Base address of the mapped view, stored as IntPtr so the field itself is not unsafe.
        // Acquired via SafeMemoryMappedViewHandle.AcquirePointer in the ctor and released in
        // Dispose. All raw-pointer use is confined to the ViewSpan helper below; the helper
        // exists because no safe API can materialize a Span<byte> over an MMF view, and zero-copy
        // spans over the dump file are the entire reason this reader exists.
        private readonly IntPtr _basePtr;
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
            _innerThreadInfo = inner as IThreadInfoReader;
            _innerProcessInfo = inner as IProcessInfoProvider;
            _innerRegions = inner as IMemoryRegionReader;

            MemoryMappedFile? mmf = null;
            MemoryMappedViewAccessor? accessor = null;
            FileStream? fs = null;
            try
            {
                // Open the file ourselves with FileShare.Read|Write|Delete so we don't conflict with
                // the inner reader (which already has the file open with FileShare.Read).
                // MemoryMappedFile.CreateFromFile(string, ...) opens with FileShare.None internally,
                // which would throw "file in use" against the inner reader's existing handle.
                fs = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                mmf = MemoryMappedFile.CreateFromFile(fs, mapName: null, capacity: 0, MemoryMappedFileAccess.Read,
                    HandleInheritability.None, leaveOpen: false);
                accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                _viewLength = (long)accessor.SafeMemoryMappedViewHandle.ByteLength;

                unsafe
                {
                    byte* ptr = null;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                    _basePtr = (IntPtr)(ptr + accessor.PointerOffset);
                }

                _mmf = mmf;
                _accessor = accessor;
            }
            catch (Exception ex) when (IsAddressSpaceExhaustion(ex))
            {
                accessor?.Dispose();
                mmf?.Dispose();
                fs?.Dispose();
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
            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
            if (Read(address, buffer) != buffer.Length)
            {
                value = default;
                return false;
            }
            value = MemoryMarshal.Read<T>(buffer);
            return true;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            int ps = PointerSize;
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            // stackalloc zero-inits, so the upper 4 bytes remain 0 on 32-bit reads.
            if (Read(address, buffer.Slice(0, ps)) != ps)
            {
                value = 0;
                return false;
            }
            value = ps == sizeof(ulong) ? MemoryMarshal.Read<ulong>(buffer) : MemoryMarshal.Read<uint>(buffer);
            return true;
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }

        // Single chokepoint for materializing a Span<byte> over the mapped view.
        // This is the only method that dereferences _basePtr.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ReadOnlySpan<byte> ViewSpan(long fileOffset, int length)
            => new((byte*)_basePtr + fileOffset, length);

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
            ViewSpan((long)fileOffset, toRead).CopyTo(buffer);
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

            span = ViewSpan((long)fileOffset, length);
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

        // IThreadInfoReader passthrough
        public bool TryGetThreadInfo(uint osThreadId, out ThreadInfo info)
        {
            if (_innerThreadInfo is null)
                throw CreateUnsupportedInterfaceException(nameof(IThreadInfoReader));

            return _innerThreadInfo.TryGetThreadInfo(osThreadId, out info);
        }

        public IEnumerable<ThreadInfo> EnumerateThreadInfo()
            => _innerThreadInfo?.EnumerateThreadInfo()
               ?? throw CreateUnsupportedInterfaceException(nameof(IThreadInfoReader));

        // IProcessInfoProvider passthrough
        public ProcessInfo GetProcessInfo()
            => _innerProcessInfo?.GetProcessInfo()
               ?? throw CreateUnsupportedInterfaceException(nameof(IProcessInfoProvider));

        // IMemoryRegionReader passthrough
        public IEnumerable<MemoryRegion> EnumerateMemoryRegions()
            => _innerRegions?.EnumerateMemoryRegions()
               ?? throw CreateUnsupportedInterfaceException(nameof(IMemoryRegionReader));

        private static NotSupportedException CreateUnsupportedInterfaceException(string interfaceName)
            => new($"The inner data reader does not implement {interfaceName}.");

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
