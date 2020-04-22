// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class MinidumpMemoryReader : IDisposable, IMemoryReader
    {
        const int MaxStackAllocBytes = 64;

        private readonly MinidumpSegment[] _segments;
        private readonly FileStream _stream;
        private readonly object _sync = new object();

        // We shouldn't dispose from multiple threads or race, but we'll be safe and assume it can happen
        private volatile bool _disposed;

        public MinidumpMemoryReader(MinidumpSegment[] segments, FileStream stream, int pointerSize)
        {
            _segments = segments;
            _stream = stream;
            PointerSize = pointerSize;
        }

        public int PointerSize { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_sync)
                {
                    if (!_disposed)
                    {
                        _disposed = true;
                        _stream.Dispose();
                    }
                }
            }
        }

        public string ReadStringFromRVA(long rva)
        {
            // todo: fix this before merging
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                int count;

                lock (_sync)
                {
                    _stream.Position = rva;
                    count = _stream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.Unicode.GetString(buffer, 0, count);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer, out int size) && size == buffer.Length)
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T t);
            return t;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[PointerSize];
            if (Read(address, buffer, out int bytesRead) && bytesRead == PointerSize)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public ulong ReadPointer(ulong address)
        {
            if (ReadPointer(address, out ulong value))
                return value;

            return 0;
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            int curr = GetFirstSegment(address);
            if (curr == -1)
            {
                bytesRead = 0;
                return false;
            }

            lock (_sync)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MinidumpMemoryReader));

                try
                {
                    ulong currAddress = address;
                    Span<byte> currBuffer = buffer;
                    bytesRead = 0;

                    ref MinidumpSegment seg = ref _segments[curr];
                    while (bytesRead < buffer.Length && seg.Contains(currAddress))
                    {
                        ulong offset = currAddress - seg.VirtualAddress;

                        Span<byte> slice = currBuffer.Slice(bytesRead, Math.Min(buffer.Length - bytesRead, (int)(seg.Size - offset)));
                        _stream.Position = (long)(seg.FileOffset + offset);
                        int read = _stream.Read(slice);

                        bytesRead += read;
                        seg = ref _segments[++curr];
                    }

                    return true;
                }
                catch (IOException)
                {
                    bytesRead = 0;
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns the first segment containing address.  It's unclear if a dump would ever come through that could
        /// contain overlapping segments.
        /// </summary>
        private int GetFirstSegment(ulong address)
        {
            int result = -1;
            int lower = 0;
            int upper = _segments.Length - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;
                ref MinidumpSegment seg = ref _segments[mid];

                if (seg.Contains(address))
                {
                    result = mid;
                    break;
                }

                if (address < seg.VirtualAddress)
                    upper = mid - 1;
                else
                    lower = mid + 1;
            }

            while (result > 0 && _segments[result - 1].Contains(address))
                result--;

            return result;
        }
    }
}
