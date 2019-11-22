// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal unsafe class MemoryReader
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
            Debug.Assert(addr >= _currPageStart);

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
                fixed (byte* b = &_data[offset])
                    if (IntPtr.Size == 4)
                        value = *((uint*)b);
                    else
                        value = *((ulong*)b);

                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out uint value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                value = BitConverter.ToUInt32(_data, (int)offset);
                fixed (byte* b = &_data[offset])
                    value = *((uint*)b);
                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out int value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                ulong offset = addr - _currPageStart;
                fixed (byte* b = &_data[offset])
                    value = *((int*)b);

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
            Debug.Assert(addr >= _currPageStart);

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
            fixed (byte* b = &_data[offset])
                if (IntPtr.Size == 4)
                    value = *((uint*)b);
                else
                    value = *((ulong*)b);

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
            bool res = _dataReader.ReadMemory(addr, span, out int size);

            fixed (byte* b = span)
                if (IntPtr.Size == 4)
                    value = *(uint*)b;
                else
                    value = *(ulong*)b;
            return res;
        }

        private bool MisalignedRead(ulong addr, out uint value)
        {
            Span<byte> span = stackalloc byte[4];
            bool res = _dataReader.ReadMemory(addr, span, out _);
            
            fixed (byte *ptr = span)
                value = Unsafe.ReadUnaligned<uint>(ptr);

            return res;
        }

        private bool MisalignedRead(ulong addr, out int value)
        {
            Span<byte> span = stackalloc byte[4];
            bool res = _dataReader.ReadMemory(addr, span, out _);

            fixed (byte* ptr = span)
                value = Unsafe.ReadUnaligned<int>(ptr);

            return res;
        }

        protected virtual bool MoveToPage(ulong addr)
        {
            return ReadMemory(addr);
        }

        protected virtual bool ReadMemory(ulong addr)
        {
            _currPageStart = addr;
            bool res = _dataReader.ReadMemory(_currPageStart, new Span<byte>(_data, 0, _cacheSize), out _currPageSize);

            if (!res)
            {
                _currPageStart = 0;
                _currPageSize = 0;
            }

            return res;
        }
    }
}