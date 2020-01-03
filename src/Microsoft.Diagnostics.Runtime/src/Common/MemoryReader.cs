// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class MemoryReader : IDisposable
    {
        private ulong _currPageStart;
        private int _currPageSize;
        private readonly byte[] _data;
        private readonly IDataReader _dataReader;

        public MemoryReader(IDataReader dataReader, int cacheSize)
        {
            _data = ArrayPool<byte>.Shared.Rent(cacheSize);
            _dataReader = dataReader;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_data);
        }

        public bool ReadDword(ulong addr, out uint value)
        {
            uint size = 4;
            // Is addr on the current page?  If not read the page of memory addr is on.
            // If this fails, we will fall back to a raw read out of the process (which
            // is what MisalignedRead does).
            if (addr < _currPageStart || addr >= _currPageStart + (uint)_currPageSize)
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

            // If MoveToPage succeeds, we MUST be on the right page.
            DebugOnly.Assert(addr >= _currPageStart);

            // However, the amount of data requested may fall off of the page.  In that case,
            // fall back to MisalignedRead.
            ulong offset = addr - _currPageStart;
            if (offset + size > (uint)_currPageSize)
                return MisalignedRead(addr, out value);

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            value = BitConverter.ToUInt32(_data, (int)offset);
            return true;
        }

        public bool ReadDword(ulong addr, out int value)
        {
            bool res = ReadDword(addr, out uint tmp);

            value = (int)tmp;
            return res;
        }

        internal bool TryReadPtr(ulong addr, out ulong value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = _data.AsSpan((int)offset).AsPointer();

                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out uint value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = _data.AsSpan((int)offset).AsUInt32();
                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out int value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = _data.AsSpan((int)offset).AsInt32();
                return true;
            }

            return MisalignedRead(addr, out value);
        }

        public bool ReadPtr(ulong addr, out ulong value)
        {
            // Is addr on the current page?  If not read the page of memory addr is on.
            // If this fails, we will fall back to a raw read out of the process (which
            // is what MisalignedRead does).
            if (addr < _currPageStart || addr - _currPageStart > (uint)_currPageSize)
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

            // If MoveToPage succeeds, we MUST be on the right page.
            DebugOnly.Assert(addr >= _currPageStart);

            // However, the amount of data requested may fall off of the page.  In that case,
            // fall back to MisalignedRead.
            ulong offset = addr - _currPageStart;
            if (offset + (uint)IntPtr.Size > (uint)_currPageSize)
            {
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

                offset = 0;
            }

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            value = _data.AsSpan((int)offset).AsPointer();

            return true;
        }

        public void EnsureRangeInCache(ulong addr)
        {
            if (!Contains(addr))
                MoveToPage(addr);
        }

        public bool Contains(ulong addr)
        {
            return _currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize;
        }

        private bool MisalignedRead(ulong addr, out ulong value)
        {
            Span<byte> span = stackalloc byte[IntPtr.Size];
            bool res = _dataReader.Read(addr, span, out int size);

            value = span.AsPointer();

            return res;
        }

        private bool MisalignedRead(ulong addr, out uint value)
        {
            Span<byte> span = stackalloc byte[sizeof(uint)];
            bool res = _dataReader.Read(addr, span, out _);

            value = span.AsUInt32();

            return res;
        }

        private bool MisalignedRead(ulong addr, out int value)
        {
            Span<byte> span = stackalloc byte[sizeof(int)];
            bool res = _dataReader.Read(addr, span, out _);

            value = span.AsInt32();

            return res;
        }

        private bool MoveToPage(ulong addr)
        {
            _currPageStart = addr;
            bool res = _dataReader.Read(_currPageStart, _data, out _currPageSize);

            if (!res)
            {
                _currPageStart = 0;
                _currPageSize = 0;
            }

            return res;
        }
    }
}