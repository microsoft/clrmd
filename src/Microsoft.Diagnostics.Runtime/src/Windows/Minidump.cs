// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class Minidump : IDisposable
    {
        private readonly string _crashDump;
        private bool _disposed;
        
        private readonly MinidumpDirectory[] _directories;

        private readonly Task<MinidumpMemoryReader> _memoryTask;
        private MinidumpMemoryReader? _readerCached;

        private readonly Task<ImmutableArray<MinidumpContextData>> _threadTask;
        private ImmutableArray<MinidumpContextData> _contextsCached;

        public MinidumpMemoryReader MemoryReader
        {
            get
            {
                if (_readerCached != null)
                    return _readerCached;

                _readerCached = _memoryTask.Result;
                return _readerCached;
            }
        }

        public ImmutableArray<MinidumpContextData> ContextData
        {
            get
            {
                if (!_contextsCached.IsDefault)
                    return _contextsCached;

                _contextsCached = _threadTask.Result;
                return _contextsCached;
            }
        }

        public ImmutableArray<MinidumpModule> Modules { get; }

        public MinidumpProcessorArchitecture Architecture { get; }

        public int PointerSize
        {
            get
            {
                switch (Architecture)
                {
                    case MinidumpProcessorArchitecture.Arm64:
                    case MinidumpProcessorArchitecture.Amd64:
                        return 8;

                    case MinidumpProcessorArchitecture.Intel:
                    case MinidumpProcessorArchitecture.Arm:
                        return 4;

                    default:
                        throw new NotImplementedException($"Not implemented for architecture {Architecture}.");
                }
            }
        }

        public Minidump(string crashDump, Stream stream)
        {
            if (!File.Exists(crashDump))
                throw new FileNotFoundException(crashDump);

            _crashDump = crashDump;

            // Load header
            MinidumpHeader header = Read<MinidumpHeader>(stream);
            if (!header.IsValid)
                throw new InvalidDataException($"File '{crashDump}' is not a Minidump.");

            _directories = new MinidumpDirectory[header.NumberOfStreams];

            stream.Position = header.StreamDirectoryRva;
            if (!Read(stream, _directories))
                throw new InvalidDataException($"Unable to read directories from minidump '{crashDump} offset 0x{header.StreamDirectoryRva:x}");

            (int systemInfoIndex, int moduleListIndex) = FindImportantStreams(crashDump);

            // Architecture is the first entry in MINIDUMP_SYSTEM_INFO.  We need nothing else out of that struct,
            // so we only read the first entry.
            // https://docs.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_system_info
            Architecture = Read<MinidumpProcessorArchitecture>(stream, _directories[systemInfoIndex].Rva);

            // Initialize modules.  DataTarget will need a module list immediately, so there's no reason to delay
            // filling in the module list.
            long rva = _directories[moduleListIndex].Rva;
            uint count = Read<uint>(stream, rva);

            rva += sizeof(uint);
            MinidumpModule[] modules = new MinidumpModule[count];

            if (Read(stream, rva, modules))
                Modules = modules.AsImmutableArray();
            else
                Modules = ImmutableArray<MinidumpModule>.Empty;

            // Read segments async.
            _memoryTask = GetMemoryReader();
            _threadTask = ReadThreadData(stream);
        }

        public IEnumerable<MinidumpModuleInfo> EnumerateModuleInfo() => Modules.Select(m => new MinidumpModuleInfo(MemoryReader, m));

        private (int systemInfo, int moduleList) FindImportantStreams(string crashDump)
        {
            int systemInfo = -1;
            int moduleList = -1;

            for (int i = 0; i < _directories.Length; i++)
            {
                switch (_directories[i].StreamType)
                {
                    case MinidumpStreamType.ModuleListStream:
                        if (moduleList != -1)
                            throw new InvalidDataException($"Minidump '{crashDump}' had multiple module lists.");

                        moduleList = i;
                        break;

                    case MinidumpStreamType.SystemInfoStream:
                        if (systemInfo != -1)
                            throw new InvalidDataException($"Minidump '{crashDump}' had multiple system info streams.");

                        systemInfo = i;
                        break;
                }
            }

            if (systemInfo == -1)
                throw new InvalidDataException($"Minidump '{crashDump}' did not contain a system info stream.");
            if (moduleList == -1)
                throw new InvalidDataException($"Minidump '{crashDump}' did not contain a module list stream.");

            return (systemInfo, moduleList);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _readerCached?.Dispose();
            }
        }

        #region ReadThreadData
        private async Task<ImmutableArray<MinidumpContextData>> ReadThreadData(Stream stream)
        {
            Dictionary<uint, (uint Rva, uint Size)> threadContextLocations = new Dictionary<uint, (uint Rva, uint Size)>();

            using (stream)
            {
                // This will select ThreadListStread, ThreadExListStream, and ThreadInfoListStream in that order.
                // We prefer to pull contexts from the *ListStreams but if those don't exist or are missing threads
                // we still want threadIDs which don't have context records for IDataReader.EnumerateThreads.
                var directories = from d in _directories
                                  where d.StreamType == MinidumpStreamType.ThreadListStream ||
                                        d.StreamType == MinidumpStreamType.ThreadExListStream ||
                                        d.StreamType == MinidumpStreamType.ThreadInfoListStream
                                  orderby d.StreamType ascending
                                  select d;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
                try
                {

                    foreach (MinidumpDirectory directory in _directories.Where(d => d.StreamType == MinidumpStreamType.ThreadListStream || d.StreamType == MinidumpStreamType.ThreadExListStream))
                    {
                        if (directory.StreamType == MinidumpStreamType.ThreadListStream)
                        {
                            uint numThreads = await ReadAsync<uint>(stream, buffer, directory.Rva).ConfigureAwait(false);
                            if (numThreads == 0)
                                continue;

                            int count = ResizeBytesForArray<MinidumpThread>(numThreads, ref buffer);
                            int read = await stream.ReadAsync(buffer, 0, count).ConfigureAwait(false);

                            for (int i = 0; i < read; i += SizeOf<MinidumpThread>())
                            {
                                MinidumpThread thread = Unsafe.As<byte, MinidumpThread>(ref buffer[i]);
                                threadContextLocations[thread.ThreadId] = (thread.ThreadContext.Rva, thread.ThreadContext.DataSize);
                            }
                        }
                        else if (directory.StreamType == MinidumpStreamType.ThreadExListStream)
                        {
                            uint numThreads = await ReadAsync<uint>(stream, buffer, directory.Rva).ConfigureAwait(false);
                            if (numThreads == 0)
                                continue;

                            int count = ResizeBytesForArray<MinidumpThreadEx>(numThreads, ref buffer);
                            int read = await stream.ReadAsync(buffer, 0, count).ConfigureAwait(false);

                            for (int i = 0; i < read; i += SizeOf<MinidumpThreadEx>())
                            {
                                MinidumpThreadEx thread = Unsafe.As<byte, MinidumpThreadEx>(ref buffer[i]);
                                threadContextLocations[thread.ThreadId] = (thread.ThreadContext.Rva, thread.ThreadContext.DataSize);
                            }
                        }
                        else if (directory.StreamType == MinidumpStreamType.ThreadInfoListStream)
                        {
                            MinidumpThreadInfoList threadInfoList = await ReadAsync<MinidumpThreadInfoList>(stream, buffer, directory.Rva).ConfigureAwait(false);
                            if (threadInfoList.NumberOfEntries <= 0)
                                continue;

                            if (threadInfoList.SizeOfEntry != SizeOf<MinidumpThreadInfo>())
                                throw new InvalidDataException($"ThreadInfoList.SizeOfEntry=0x{threadInfoList.SizeOfEntry:x}, but sizeof(MinidumpThreadInfo)=0x{SizeOf<MinidumpThreadInfo>()}");

                            stream.Position = directory.Rva + threadInfoList.SizeOfHeader;
                            int count = ResizeBytesForArray<MinidumpThreadInfo>((ulong)threadInfoList.NumberOfEntries, ref buffer);

                            int read = await stream.ReadAsync(buffer, 0, count).ConfigureAwait(false);

                            for (int i = 0; i < read; i += threadInfoList.SizeOfEntry)
                            {
                                MinidumpThreadInfo thread = Unsafe.As<byte, MinidumpThreadInfo>(ref buffer[i]);
                                if (!threadContextLocations.ContainsKey(thread.ThreadId))
                                    threadContextLocations[thread.ThreadId] = (0, 0);
                            }
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            var result = from entry in threadContextLocations
                         let threadId = entry.Key
                         let rva = entry.Value.Rva
                         let size = entry.Value.Size
                         orderby threadId
                         select new MinidumpContextData(threadId, rva, size);

            return result.ToImmutableArray();
        }
        #endregion


        #region GetMemoryReader
        private async Task<MinidumpMemoryReader> GetMemoryReader()
        {
            List<MinidumpSegment> segments = new List<MinidumpSegment>();

            FileStream fileStream = File.Open(_crashDump, FileMode.Open, FileAccess.Read, FileShare.Read);

            byte[]? buffer = ArrayPool<byte>.Shared.Rent(128);
            try
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    if (_directories[i].StreamType == MinidumpStreamType.MemoryListStream)
                    {
                        // MINIDUMP_MEMORY_LIST only contains a count followed by MINIDUMP_MEMORY_DESCRIPTORs
                        uint count = await ReadAsync<uint>(fileStream, buffer, _directories[i].Rva).ConfigureAwait(false);
                        int byteCount = ResizeBytesForArray<MinidumpMemoryDescriptor>(count, ref buffer);

                        if (await fileStream.ReadAsync(buffer, 0, byteCount).ConfigureAwait(false) == byteCount)
                            AddSegments(segments, buffer, byteCount);
                    }
                    else if (_directories[i].StreamType == MinidumpStreamType.Memory64ListStream)
                    {
                        MinidumpMemory64List memList64 = await ReadAsync<MinidumpMemory64List>(fileStream, buffer, _directories[i].Rva).ConfigureAwait(false);
                        int byteCount = ResizeBytesForArray<MinidumpMemoryDescriptor>(memList64.NumberOfMemoryRanges, ref buffer);

                        if (await fileStream.ReadAsync(buffer, 0, byteCount).ConfigureAwait(false) == byteCount)
                            AddSegments(segments, memList64.Rva, buffer, byteCount);
                    }
                }
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }

            MinidumpSegment[] result = segments.Where(s => s.Size > 0).OrderBy(s => s.VirtualAddress).ToArray();
            return new MinidumpMemoryReader(result, fileStream, PointerSize);
        }

        private static unsafe void AddSegments(List<MinidumpSegment> segments, byte[] buffer, int byteCount)
        {
            int count = byteCount / sizeof(MinidumpMemoryDescriptor);

            fixed (byte* ptr = buffer)
            {
                MinidumpMemoryDescriptor* desc = (MinidumpMemoryDescriptor*)ptr;
                for (int i = 0; i < count; i++)
                    segments.Add(new MinidumpSegment(desc[i].Rva, desc[i].StartAddress, desc[i].DataSize32));
            }
        }

        private static unsafe void AddSegments(List<MinidumpSegment> segments, ulong rva, byte[] buffer, int byteCount)
        {
            int count = byteCount / sizeof(MinidumpMemoryDescriptor);

            fixed (byte* ptr = buffer)
            {
                MinidumpMemoryDescriptor* desc = (MinidumpMemoryDescriptor*)ptr;
                for (int i = 0; i < count; i++)
                {
                    segments.Add(new MinidumpSegment(rva, desc[i].StartAddress, desc[i].DataSize64));
                    rva += desc[i].DataSize64;
                }
            }
        }

        private static unsafe int ResizeBytesForArray<T>(ulong count, [NotNull] ref byte[]? buffer)
            where T: unmanaged
        {
            int size = (int)count * sizeof(T);
            if (buffer == null)
            {
                buffer = new byte[size];
            }
            else if (buffer.Length < size)
            {
                ArrayPool<byte> pool = ArrayPool<byte>.Shared;
                
                pool.Return(buffer);
                buffer = pool.Rent(size);
            }

            return size;
        }
        #endregion

        private static async Task<T> ReadAsync<T>(Stream stream, byte[] buffer, long offset)
            where T : unmanaged
        {
            int size = SizeOf<T>();
            if (buffer.Length < size)
                buffer = new byte[size];

            stream.Position = offset;
            int read = await stream.ReadAsync(buffer, 0, size).ConfigureAwait(false);
            if (read == size)
            {
                T result = Unsafe.As<byte, T>(ref buffer[0]);
                return result;
            }

            return default;
        }

        private static unsafe int SizeOf<T>() where T : unmanaged => sizeof(T);

        private static T Read<T>(Stream stream, long offset)
            where T : unmanaged
        {
            stream.Position = offset;
            return Read<T>(stream);
        }

        private static unsafe T Read<T>(Stream stream)
            where T: unmanaged
        {
            int size = sizeof(T);
            Span<byte> buffer = stackalloc byte[size];

            int read = stream.Read(buffer);
            if (read < size)
                return default;

            return Unsafe.As<byte, T>(ref buffer[0]);
        }

        private static bool Read<T>(Stream stream, long offset, T[] array)
            where T : unmanaged
        {
            stream.Position = offset;
            return Read(stream, array);
        }

        private static unsafe bool Read<T>(Stream stream, T[] array)
            where T : unmanaged
        {
            Span<byte> buffer = MemoryMarshal.AsBytes(new Span<T>(array));
            int read = stream.Read(buffer);
            return read == buffer.Length;
        }

        public override string ToString() => _crashDump;
    }


    internal readonly struct MinidumpContextData
    {
        public readonly uint ThreadId;
        public readonly uint ContextRva;
        public readonly uint ContextBytes;

        public MinidumpContextData(uint threadId, uint contextRva, uint contextBytes)
        {
            ThreadId = threadId;
            ContextRva = contextRva;
            ContextBytes = contextBytes;
        }
    }
}
