// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class UncachedMemoryReader : MinidumpMemoryReader
    {
        private readonly ImmutableArray<MinidumpSegment> _segments;
        private readonly Stream _stream;
        private readonly object _sync = new object();

        public UncachedMemoryReader(ImmutableArray<MinidumpSegment> segments, Stream stream, int pointerSize)
        {
            _segments = segments;
            _stream = stream;
            PointerSize = pointerSize;
        }

        public override void Dispose()
        {
            _stream.Dispose();
        }

        public override int PointerSize { get; }

        public override int ReadFromRva(ulong rva, Span<byte> buffer)
        {
            // todo: test bounds
            lock (_sync)
            {
                if ((ulong)_stream.Length <= rva)
                    return 0;

                _stream.Position = (long)rva;
                return _stream.Read(buffer);
            }
        }


        public override unsafe bool Read<T>(ulong address, out T value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer) == buffer.Length)
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public override T Read<T>(ulong address)
        {
            Read(address, out T t);
            return t;
        }

        public override bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[PointerSize];
            if (Read(address, buffer) == PointerSize)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public override ulong ReadPointer(ulong address)
        {
            if (ReadPointer(address, out ulong value))
                return value;

            return 0;
        }

        public override int Read(ulong address, Span<byte> buffer)
        {
            if (address == 0)
                return 0;

            lock (_sync)
            {
                try
                {
                    int bytesRead = 0;

                    while (bytesRead < buffer.Length)
                    {
                        ulong currAddress = address + (uint)bytesRead;
                        int curr = GetSegmentContaining(currAddress);
                        if (curr == -1)
                            break;

                        MinidumpSegment seg = _segments[curr];
                        ulong offset = currAddress - seg.VirtualAddress;

                        Span<byte> slice = buffer.Slice(bytesRead, Math.Min(buffer.Length - bytesRead, (int)(seg.Size - offset)));
                        _stream.Position = (long)(seg.FileOffset + offset);
                        int read = _stream.Read(slice);
                        if (read == 0)
                            break;

                        bytesRead += read;
                    }

                    return bytesRead;
                }
                catch (IOException)
                {
                    return 0;
                }
            }
        }

        private int IndexOf(ulong end, MinidumpSegment[] segments)
        {
            for (int i = 0; i < segments.Length; i++)
                if (segments[i].Contains(end))
                    return i;

            return -1;
        }

        private int GetSegmentContaining(ulong address)
        {
            int result = -1;
            int lower = 0;
            int upper = _segments.Length - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;
                MinidumpSegment seg = _segments[mid];

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

            return result;
        }
    }
}
