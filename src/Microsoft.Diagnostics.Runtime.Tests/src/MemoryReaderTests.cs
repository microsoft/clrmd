// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MemoryReaderTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SearchMemoryTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // We make sure SearchMemory will find things that are not pointer aligned with 'i' here.
            Span<byte> buffer = stackalloc byte[sizeof(ulong) + sizeof(int)];

            foreach (ClrSegment segment in heap.Segments)
            {
                HashSet<ulong> seen = new() { 0 };
                List<ClrObject> firstSeenObjectOfType = new();

                // We will search for method tables so make sure we
                foreach (ClrObject obj in segment.EnumerateObjects())
                    if (seen.Add(obj.Type.MethodTable))
                        firstSeenObjectOfType.Add(obj);

                foreach (ClrObject obj in firstSeenObjectOfType)
                {
                    for (int i = 0; i < sizeof(int); i++)
                    {
                        ulong expectedOffset = obj.Address - (uint)i;
                        if (expectedOffset < segment.Start)
                            continue;

                        Span<byte> slice = buffer.Slice(0, sizeof(ulong) + i);
                        if (dt.DataReader.Read(expectedOffset, slice) != slice.Length)
                            continue;

                        ulong addressFound = dt.DataReader.SearchMemory(segment.Start, (int)segment.Length, buffer.Slice(0, i + sizeof(ulong)));

                        // There could still accidentally be a pattern that matches this somewhere
                        while (addressFound != 0 && addressFound < expectedOffset)
                            addressFound = dt.DataReader.SearchMemory(addressFound + 1, (int)(segment.End - (addressFound + 1)), buffer.Slice(0, i + sizeof(ulong)));

                        Assert.Equal(expectedOffset, addressFound);
                    }
                }
            }
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_TryGetDirectSpan_MatchesRead()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            ISegmentedDirectMemoryAccess direct = Assert.IsAssignableFrom<ISegmentedDirectMemoryAccess>(dt.DataReader);

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            int hits = 0;
            byte[] expected = new byte[8192];

            foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
            {
                if (obj.Address == 0)
                    continue;

                ulong size = obj.Size;
                if (size == 0 || size > (ulong)expected.Length)
                    continue;

                int len = (int)size;

                if (!direct.TryGetDirectSpan(obj.Address, len, out ReadOnlySpan<byte> span))
                    continue;   // segment-boundary case, not what this test exercises

                Assert.Equal(len, span.Length);

                int read = dt.DataReader.Read(obj.Address, expected.AsSpan(0, len));
                Assert.Equal(len, read);
                Assert.True(span.SequenceEqual(expected.AsSpan(0, len)),
                    $"direct span and Read() bytes diverged at object {obj.Address:x}");

                if (++hits >= 50)
                    break;
            }

            Assert.True(hits > 0, "fixture exposed no walkable objects (LockFreeMmfDataReader did not satisfy any TryGetDirectSpan)");
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_TryGetDirectSpan_BogusAddressFails()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            ISegmentedDirectMemoryAccess direct = Assert.IsAssignableFrom<ISegmentedDirectMemoryAccess>(dt.DataReader);

            Assert.False(direct.TryGetDirectSpan(0xBAADF00DBAADF00D, 8, out ReadOnlySpan<byte> span));
            Assert.Equal(0, span.Length);

            // Zero-length succeeds even on a bogus address by contract.
            Assert.True(direct.TryGetDirectSpan(0xBAADF00DBAADF00D, 0, out span));
            Assert.Equal(0, span.Length);
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_IsThreadSafe()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            Assert.True(dt.DataReader.IsThreadSafe);
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_ConcurrentReads_MatchSerialReads()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            Assert.True(dt.DataReader.IsThreadSafe);

            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            // Build a baseline (address, length, expected bytes) tuple set serially. Each
            // parallel worker will replay reads against the same addresses and assert it
            // gets the same bytes back.
            List<(ulong Address, byte[] Expected)> baseline = new();
            foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
            {
                if (obj.Address == 0)
                    continue;

                ulong size = obj.Size;
                if (size == 0 || size > 4096)
                    continue;

                int len = (int)size;
                byte[] buf = new byte[len];
                if (dt.DataReader.Read(obj.Address, buf) != len)
                    continue;

                baseline.Add((obj.Address, buf));
                if (baseline.Count >= 2000)
                    break;
            }

            Assert.True(baseline.Count > 100, $"fixture too small for a stress test (got {baseline.Count})");

            // Each thread iterates the baseline in a different order to maximize stripe
            // contention and force segment-cache misses.
            int threadCount = Math.Max(4, Environment.ProcessorCount);
            int iterations = 4;
            System.Collections.Concurrent.ConcurrentQueue<Exception> failures = new();

            System.Threading.Tasks.Parallel.For(0, threadCount, threadIndex =>
            {
                try
                {
                    Random rng = new(threadIndex * 17 + 1);
                    byte[] scratch = new byte[4096];

                    for (int iter = 0; iter < iterations; iter++)
                    {
                        for (int i = 0; i < baseline.Count; i++)
                        {
                            // Permute the order per-thread. Use unchecked arithmetic since
                            // we only want bit-mixing, not a meaningful integer result.
                            int idx;
                            unchecked
                            {
                                idx = ((i * (rng.Next() | 1)) + threadIndex * 31) & 0x7fffffff;
                            }
                            idx %= baseline.Count;

                            (ulong addr, byte[] expected) = baseline[idx];
                            Span<byte> slice = scratch.AsSpan(0, expected.Length);
                            int read = dt.DataReader.Read(addr, slice);
                            Assert.Equal(expected.Length, read);
                            Assert.True(slice.SequenceEqual(expected),
                                $"thread {threadIndex} got divergent bytes for address {addr:x}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                }
            });

            if (!failures.IsEmpty)
                throw new Xunit.Sdk.XunitException("concurrent read mismatch: " + failures.ToArray()[0]);
        }

        [WindowsFact]
        public void LockFreeMmfDataReader_ConcurrentDirectSpan_MatchesRead()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(
                options: DataReaderKind.LockFreeMmf.ToOptions());

            ISegmentedDirectMemoryAccess direct = Assert.IsAssignableFrom<ISegmentedDirectMemoryAccess>(dt.DataReader);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            List<(ulong Address, byte[] Expected)> baseline = new();
            foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
            {
                if (obj.Address == 0)
                    continue;

                ulong size = obj.Size;
                if (size == 0 || size > 4096)
                    continue;

                int len = (int)size;
                if (!direct.TryGetDirectSpan(obj.Address, len, out ReadOnlySpan<byte> span))
                    continue;

                baseline.Add((obj.Address, span.ToArray()));
                if (baseline.Count >= 1000)
                    break;
            }

            Assert.True(baseline.Count > 50, $"fixture too small (got {baseline.Count})");

            int threadCount = Math.Max(4, Environment.ProcessorCount);
            System.Collections.Concurrent.ConcurrentQueue<Exception> failures = new();

            System.Threading.Tasks.Parallel.For(0, threadCount, threadIndex =>
            {
                try
                {
                    foreach ((ulong addr, byte[] expected) in baseline)
                    {
                        Assert.True(direct.TryGetDirectSpan(addr, expected.Length, out ReadOnlySpan<byte> span));
                        Assert.True(span.SequenceEqual(expected),
                            $"thread {threadIndex} got divergent direct-span bytes for address {addr:x}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                }
            });

            if (!failures.IsEmpty)
                throw new Xunit.Sdk.XunitException("concurrent direct-span mismatch: " + failures.ToArray()[0]);
        }

        [Fact]
        public void LockFreeMmfDataReader_FiltersInvalidSegmentsWithoutBreakingAdjacentReads()
        {
            byte[] fileBytes = [1, 2, 3, 4, 5, 6, 7, 8];
            string path = WriteTempFile(fileBytes);
            try
            {
                using FakeDumpReader inner = new(new[]
                {
                    new DumpMemorySegment(0x1000, 0, 4),
                    new DumpMemorySegment(0x1004, ulong.MaxValue - 1, 4),
                    new DumpMemorySegment(0x1004, 4, 4),
                });
                using LockFreeMmfDataReader reader = new(path, inner);

                byte[] buffer = new byte[8];
                Assert.Equal(8, reader.Read(0x1000, buffer));
                Assert.Equal(fileBytes, buffer);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LockFreeMmfDataReader_FindsEarlierOverlappingSegment()
        {
            byte[] fileBytes = new byte[0x4000];
            fileBytes[0x1000] = 0xa1;
            fileBytes[0x1001] = 0xb2;
            fileBytes[0x1002] = 0xc3;
            fileBytes[0x1003] = 0xd4;
            string path = WriteTempFile(fileBytes);
            try
            {
                using FakeDumpReader inner = new(new[]
                {
                    new DumpMemorySegment(0x1000, 0, 0x2000),
                    new DumpMemorySegment(0x1800, 0x3000, 0x100),
                    new DumpMemorySegment(0x3000, 0x3100, 0x10),
                });
                using LockFreeMmfDataReader reader = new(path, inner);

                byte[] buffer = new byte[4];
                Assert.Equal(4, reader.Read(0x2000, buffer));
                Assert.Equal(new byte[] { 0xa1, 0xb2, 0xc3, 0xd4 }, buffer);

                ISegmentedDirectMemoryAccess direct = reader;
                Assert.True(direct.TryGetDirectSpan(0x2000, 4, out ReadOnlySpan<byte> span));
                Assert.True(span.SequenceEqual(buffer));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LockFreeMmfDataReader_RejectsOutOfViewDirectSpan()
        {
            string path = WriteTempFile([1, 2, 3, 4]);
            try
            {
                using FakeDumpReader inner = new(new[]
                {
                    new DumpMemorySegment(0x1000, ulong.MaxValue - 1, 4),
                });
                using LockFreeMmfDataReader reader = new(path, inner);

                ISegmentedDirectMemoryAccess direct = reader;
                Assert.False(direct.TryGetDirectSpan(0x1000, 1, out ReadOnlySpan<byte> span));
                Assert.Equal(0, span.Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LockFreeMmfDataReader_ReadAfterDisposeThrowsObjectDisposedException()
        {
            string path = WriteTempFile([1, 2, 3, 4]);
            try
            {
                using FakeDumpReader inner = new(new[] { new DumpMemorySegment(0x1000, 0, 4) });
                LockFreeMmfDataReader reader = new(path, inner);
                reader.Dispose();

                byte[] buffer = new byte[1];
                Assert.Throws<ObjectDisposedException>(() => reader.Read(0x1000, buffer));
                ISegmentedDirectMemoryAccess direct = reader;
                Assert.Throws<ObjectDisposedException>(() => direct.TryGetDirectSpan(0x1000, 1, out _));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task LockFreeMmfDataReader_ConcurrentDisposeDisposesInnerReaderOnce()
        {
            string path = WriteTempFile([1, 2, 3, 4]);
            try
            {
                FakeDumpReader inner = new(new[] { new DumpMemorySegment(0x1000, 0, 4) });
                LockFreeMmfDataReader reader = new(path, inner);
                int threadCount = Math.Max(8, Environment.ProcessorCount * 2);
                using System.Threading.ManualResetEventSlim gate = new(initialState: false);
                System.Collections.Concurrent.ConcurrentQueue<Exception> failures = new();
                System.Threading.Tasks.Task[] tasks = Enumerable.Range(0, threadCount)
                    .Select(_ => System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            gate.Wait();
                            reader.Dispose();
                        }
                        catch (Exception ex)
                        {
                            failures.Enqueue(ex);
                        }
                    }))
                    .ToArray();

                gate.Set();
                await System.Threading.Tasks.Task.WhenAll(tasks);

                if (!failures.IsEmpty)
                    throw new Xunit.Sdk.XunitException("concurrent dispose failed: " + failures.ToArray()[0]);

                Assert.Equal(1, inner.DisposeCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LockFreeMmfDataReader_ConstructorFailureDisposesInnerReader()
        {
            string path = WriteTempFile([1, 2, 3, 4]);
            try
            {
                FakeDumpReader inner = new(Array.Empty<DumpMemorySegment>(), throwOnEnumerate: true);

                Assert.Throws<InvalidOperationException>(() => new LockFreeMmfDataReader(path, inner));
                Assert.True(inner.Disposed);

                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Assert.Equal(4, stream.Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string WriteTempFile(byte[] bytes)
        {
            string path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private sealed class FakeDumpReader : IDataReader, IDumpFileMemorySource, IDisposable
        {
            private readonly IReadOnlyList<DumpMemorySegment> _segments;
            private readonly bool _throwOnEnumerate;

            public FakeDumpReader(IReadOnlyList<DumpMemorySegment> segments, bool throwOnEnumerate = false)
            {
                _segments = segments;
                _throwOnEnumerate = throwOnEnumerate;
            }

            public int DisposeCount;
            public bool Disposed => DisposeCount != 0;
            public int PointerSize => 8;
            public string DisplayName => nameof(FakeDumpReader);
            public bool IsThreadSafe => true;
            public OSPlatform TargetPlatform => OSPlatform.Windows;
            public Architecture Architecture => Architecture.X64;
            public int ProcessId => 0;

            public IReadOnlyList<DumpMemorySegment> EnumerateMemorySegments()
                => _throwOnEnumerate ? throw new InvalidOperationException("synthetic segment enumeration failure") : _segments;

            public int Read(ulong address, Span<byte> buffer) => 0;
            public bool Read<T>(ulong address, out T value) where T : unmanaged
            {
                value = default;
                return false;
            }
            public T Read<T>(ulong address) where T : unmanaged => default;
            public bool ReadPointer(ulong address, out ulong value)
            {
                value = 0;
                return false;
            }
            public ulong ReadPointer(ulong address) => 0;
            public IEnumerable<ModuleInfo> EnumerateModules() => Array.Empty<ModuleInfo>();
            public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) => false;
            public void FlushCachedData() { }
            public void Dispose() => System.Threading.Interlocked.Increment(ref DisposeCount);
        }
    }
}

