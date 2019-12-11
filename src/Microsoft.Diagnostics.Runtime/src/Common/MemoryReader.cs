// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal class MemoryReader
    {
        protected ulong _currPageStart;
        protected int _currPageSize;
        protected byte[] _data;
        protected IDataReader _dataReader;
        protected int _cacheSize;

        public MemoryReader(IDataReader dataReader, int cacheSize)
        {
            _data = new byte[cacheSize];
            _dataReader = dataReader;
            _cacheSize = cacheSize;
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
                ref byte b = ref _data[offset];
                if (IntPtr.Size == 4)
                    value = Unsafe.As<byte, uint>(ref b);
                else
                    value = Unsafe.As<byte, ulong>(ref b);

                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out uint value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = Unsafe.As<byte, uint>(ref _data[offset]);
                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out int value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = Unsafe.As<byte, int>(ref _data[offset]);
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
            ref byte b = ref _data[offset];
            if (IntPtr.Size == 4)
                value = Unsafe.As<byte, uint>(ref b);
            else
                value = Unsafe.As<byte, ulong>(ref b);

            return true;
        }

        public virtual void EnsureRangeInCache(ulong addr)
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

            ref byte b = ref MemoryMarshal.GetReference(span);
            if (IntPtr.Size == 4)
                value = Unsafe.As<byte, uint>(ref b);
            else
                value = Unsafe.As<byte, ulong>(ref b);
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

        protected virtual bool MoveToPage(ulong addr)
        {
            return ReadMemory(addr);
        }

        protected virtual bool ReadMemory(ulong addr)
        {
            _currPageStart = addr;
            bool res = _dataReader.Read(_currPageStart, new Span<byte>(_data, 0, _cacheSize), out _currPageSize);

            if (!res)
            {
                _currPageStart = 0;
                _currPageSize = 0;
            }

            return res;
        }
    }
}