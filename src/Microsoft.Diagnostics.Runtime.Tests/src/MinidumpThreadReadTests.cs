// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MinidumpThreadReadTests
    {
        private const uint Magic1 = 0x504d444d;
        private const ushort Magic2 = 0xa793;

        // Layout written by CreateMinidump: header, three directory entries, system info, module count, thread list.
        private const long ThreadListRva = 32 + 3 * 12 + 56 + sizeof(uint);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LoadDump_ReadsThreadListBeforeReturning(bool uncached)
        {
            // Async reads on this stream wait until released, so a thread-list read still running when LoadDump
            // returns stays pending. Disposing would then close the file under it. With the default cache size this
            // dump is memory-mapped; with a cache size below CachedMemoryReader.MinimumCacheSize the memory reader
            // owns the stream.
            string path = Path.GetTempFileName();
            GatedFileStream stream = null;
            DataTarget dataTarget = null;
            try
            {
                File.WriteAllBytes(path, CreateMinidump(threadCount: 1));

                var options = new DataTargetOptions();
                if (uncached)
                {
                    options.CacheOptions.MaxDumpCacheSize = 1;
                }

                stream = new GatedFileStream(path);
                dataTarget = DataTarget.LoadDump(path, stream, leaveOpen: false, options);

                Assert.False(stream.ReadPending, "a read of the dump was still pending after LoadDump returned");

                var threadReader = Assert.IsAssignableFrom<IThreadReader>(dataTarget.DataReader);
                Assert.Equal(new uint[] { 1 }, threadReader.EnumerateOSThreadIds().ToArray());

                dataTarget.Dispose();
                dataTarget = null;

                Assert.True(stream.IsClosed);
                Assert.False(stream.ReadAfterClose);
            }
            finally
            {
                // If an assertion above failed, a thread-list read may still be held. Release it and wait for the whole
                // read to finish (asking for thread data waits for it) before closing anything.
                stream?.ReleaseReads();
                if (dataTarget?.DataReader is IThreadReader pendingReader)
                {
                    try
                    {
                        _ = pendingReader.EnumerateOSThreadIds().ToArray();
                    }
                    catch (AggregateException)
                    {
                    }
                }

                try
                {
                    dataTarget?.Dispose();
                }
                finally
                {
                    try
                    {
                        stream?.Dispose();
                    }
                    finally
                    {
                        File.Delete(path);
                    }
                }
            }
        }

        [Fact]
        public void LoadDump_DefersThreadListFailure()
        {
            // A thread count over the limit makes reading the thread list fail. Loading still succeeds, the failure
            // surfaces where thread data is used, and Dispose does not throw.
            using var stream = new MemoryStream(CreateMinidump(threadCount: uint.MaxValue));
            var dataTarget = DataTarget.LoadDump("test.dmp", stream);
            Exception disposeException;
            try
            {
                var threadReader = Assert.IsAssignableFrom<IThreadReader>(dataTarget.DataReader);
                var ex = Assert.Throws<AggregateException>(() => threadReader.EnumerateOSThreadIds().ToArray());
                Assert.IsType<InvalidDataException>(ex.InnerException);
            }
            finally
            {
                disposeException = Record.Exception(dataTarget.Dispose);
            }

            Assert.Null(disposeException);
        }

        [Fact]
        public void LoadDump_DefersThreadListCancellationAsCanceled()
        {
            // A stream that throws OperationCanceledException while the thread list is read leaves thread data
            // canceled, so using it throws TaskCanceledException inside the AggregateException, as before.
            using var stream = new CancelingMemoryStream(CreateMinidump(threadCount: 1), ThreadListRva);
            var dataTarget = DataTarget.LoadDump("test.dmp", stream);
            try
            {
                var threadReader = Assert.IsAssignableFrom<IThreadReader>(dataTarget.DataReader);
                var ex = Assert.Throws<AggregateException>(() => threadReader.EnumerateOSThreadIds().ToArray());
                Assert.IsType<TaskCanceledException>(ex.InnerException);
            }
            finally
            {
                dataTarget.Dispose();
            }
        }

        private static byte[] CreateMinidump(uint threadCount)
        {
            // Header, then directories for SystemInfo, ModuleList (no modules) and ThreadList, then their data.
            const uint headerSize = 32;
            const uint directoryEntrySize = 12;
            const uint numStreams = 3;
            const uint systemInfoSize = 56;
            const uint threadSize = 48;

            uint directoryRva = headerSize;
            uint systemInfoRva = directoryRva + numStreams * directoryEntrySize;
            uint moduleListRva = systemInfoRva + systemInfoSize;
            uint threadListRva = moduleListRva + sizeof(uint);
            uint threadsWritten = Math.Min(threadCount, 1u);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(Magic1);
            bw.Write(Magic2);
            bw.Write((ushort)0);      // Version
            bw.Write(numStreams);
            bw.Write(directoryRva);
            bw.Write((uint)0);        // CheckSum
            bw.Write((uint)0);        // TimeDateStamp
            bw.Write((ulong)0);       // Flags

            bw.Write((uint)7);        // SystemInfoStream
            bw.Write(systemInfoSize);
            bw.Write(systemInfoRva);

            bw.Write((uint)4);        // ModuleListStream
            bw.Write((uint)sizeof(uint));
            bw.Write(moduleListRva);

            bw.Write((uint)3);        // ThreadListStream
            bw.Write(sizeof(uint) + threadsWritten * threadSize);
            bw.Write(threadListRva);

            // MINIDUMP_SYSTEM_INFO: ProcessorArchitecture = Amd64 (9), rest zero.
            bw.Write((ushort)9);
            bw.Write(new byte[systemInfoSize - sizeof(ushort)]);

            // Module list: no modules.
            bw.Write((uint)0);

            // Thread list: the count, then one MINIDUMP_THREAD with ThreadId 1 and no context.
            bw.Write(threadCount);
            if (threadsWritten != 0)
            {
                bw.Write((uint)1);
                bw.Write(new byte[threadSize - sizeof(uint)]);
            }

            bw.Flush();
            Assert.Equal(ThreadListRva, threadListRva);
            return ms.ToArray();
        }

        /// <summary>
        /// A FileStream whose async reads wait until <see cref="ReleaseReads"/> is called. It reports the file as closed once its handle is closed
        /// (a memory-mapped Minidump closes the handle rather than this stream) or it is disposed, and records whether a
        /// held read resumed after that.
        /// </summary>
        private sealed class GatedFileStream : FileStream
        {
            private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _pendingReads;
            private volatile bool _disposed;
            private volatile bool _readAfterClose;

            public GatedFileStream(string path)
                : base(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)
            {
            }

            public bool ReadPending => Volatile.Read(ref _pendingReads) != 0;

            // CanRead is false once the underlying handle is closed, without exposing the handle.
            public bool IsClosed => _disposed || !base.CanRead;

            public bool ReadAfterClose => _readAfterClose;

            public void ReleaseReads() => _gate.TrySetResult(true);

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _pendingReads);
                try
                {
                    await _gate.Task.ConfigureAwait(false);
                    if (IsClosed)
                    {
                        _readAfterClose = true;
                    }

                    return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingReads);
                }
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _pendingReads);
                try
                {
                    await _gate.Task.ConfigureAwait(false);
                    if (IsClosed)
                    {
                        _readAfterClose = true;
                    }

                    return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingReads);
                }
            }

            protected override void Dispose(bool disposing)
            {
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// A MemoryStream that throws OperationCanceledException from any non-empty read, sync or async, at one offset.
        /// </summary>
        private sealed class CancelingMemoryStream : MemoryStream
        {
            private readonly long _cancelAt;

            public CancelingMemoryStream(byte[] buffer, long cancelAt)
                : base(buffer, writable: false)
            {
                _cancelAt = cancelAt;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfCancelPoint(count);
                return base.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                ThrowIfCancelPoint(buffer.Length);
                return base.Read(buffer);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ThrowIfCancelPoint(count);
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                ThrowIfCancelPoint(buffer.Length);
                return base.ReadAsync(buffer, cancellationToken);
            }

            private void ThrowIfCancelPoint(int count)
            {
                if (count > 0 && Position == _cancelAt)
                {
                    throw new OperationCanceledException();
                }
            }
        }
    }
}