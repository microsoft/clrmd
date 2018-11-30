// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
#if _TRACING
    class TraceDataReader : IDataReader
    {
        private IDataReader _reader;
        private StreamWriter _file;

        public TraceDataReader(IDataReader reader)
        {
            _reader = reader;
            _file = File.CreateText("datareader.txt");
            _file.AutoFlush = true;
            _file.WriteLine(reader.GetType().ToString());
        }

        public void Close()
        {
            _file.WriteLine("Close");
            _reader.Close();
        }

        public void Flush()
        {
            _file.WriteLine("Flush");
            _reader.Flush();
        }

        public Architecture GetArchitecture()
        {
            var arch = _reader.GetArchitecture();
            _file.WriteLine("GetArchitecture - {0}", arch);
            return arch;
        }

        public uint GetPointerSize()
        {
            var ptrsize = _reader.GetPointerSize();
            _file.WriteLine("GetPointerSize - {0}", ptrsize);
            return ptrsize;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            var modules = _reader.EnumerateModules();

            int hash = 0;
            foreach (var module in modules)
                hash ^= module.FileName.ToLower().GetHashCode();

            _file.WriteLine("EnumerateModules - {0} {1:x}", modules.Count, hash);
            return modules;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            _reader.GetVersionInfo(baseAddress, out version);
            _file.WriteLine("GetVersionInfo - {0:x} {1}", baseAddress, version.ToString());
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bool result = _reader.ReadMemory(address, buffer, bytesRequested, out bytesRead);

            StringBuilder sb = new StringBuilder();
            int count = bytesRead > 8 ? 8 : bytesRead;
            for (int i = 0; i < count; ++i)
                sb.Append(buffer[i].ToString("x"));

            _file.WriteLine("ReadMemory {0}- {1:x} {2} {3}", result ? "" : "failed ", address, bytesRead, sb.ToString());

            return result;
        }

        public ulong GetThreadTeb(uint thread)
        {
            ulong teb = _reader.GetThreadTeb(thread);
            _file.WriteLine("GetThreadTeb - {0:x} {1:x}", thread, teb);
            return teb;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            List<uint> threads = new List<uint>(_reader.EnumerateAllThreads());

            bool first = true;
            StringBuilder sb = new StringBuilder();
            foreach (uint id in threads)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(id.ToString("x"));
            }

            _file.WriteLine("Threads: {0} {1}", threads.Count, sb.ToString());
            return threads;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            bool result = _reader.VirtualQuery(addr, out vq);
            _file.WriteLine("VirtualQuery {0}: {1:x} {2:x} {3}", result ? "" : "failed ", addr, vq.BaseAddress, vq.Size);
            return result;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            bool result = _reader.GetThreadContext(threadID, contextFlags, contextSize, context);
            _file.WriteLine("GetThreadContext - {0}", result);
            return result;
        }
    }
#endif

    internal unsafe class MemoryReader
    {
        protected ulong _currPageStart;
        protected int _currPageSize;
        protected byte[] _data;
        private readonly byte[] _ptr;
        private readonly byte[] _dword;
        protected IDataReader _dataReader;
        protected int _cacheSize;

        public MemoryReader(IDataReader dataReader, int cacheSize)
        {
            _data = new byte[cacheSize];
            _dataReader = dataReader;
            var sz = _dataReader.GetPointerSize();
            if (sz != 4 && sz != 8)
                throw new InvalidOperationException("DataReader reported an invalid pointer size.");

            _ptr = new byte[sz];
            _dword = new byte[4];
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
            var offset = addr - _currPageStart;
            if (offset + size > (uint)_currPageSize)
                return MisalignedRead(addr, out value);

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            value = BitConverter.ToUInt32(_data, (int)offset);
            return true;
        }

        public bool ReadDword(ulong addr, out int value)
        {
            var res = ReadDword(addr, out uint tmp);

            value = (int)tmp;
            return res;
        }

        internal bool TryReadPtr(ulong addr, out ulong value)
        {
            if (_currPageStart <= addr && addr - _currPageStart < (uint)_currPageSize)
            {
                var offset = addr - _currPageStart;
                fixed (byte* b = &_data[offset])
                    if (_ptr.Length == 4)
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
                var offset = addr - _currPageStart;
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
                var offset = addr - _currPageStart;
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
            var offset = addr - _currPageStart;
            if (offset + (uint)_ptr.Length > (uint)_currPageSize)
            {
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

                offset = 0;
            }

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            fixed (byte* b = &_data[offset])
                if (_ptr.Length == 4)
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
            var res = _dataReader.ReadMemory(addr, _ptr, _ptr.Length, out var size);
            fixed (byte* b = _ptr)
                if (_ptr.Length == 4)
                    value = *((uint*)b);
                else
                    value = *((ulong*)b);
            return res;
        }

        private bool MisalignedRead(ulong addr, out uint value)
        {
            var res = _dataReader.ReadMemory(addr, _dword, _dword.Length, out var size);
            value = BitConverter.ToUInt32(_dword, 0);
            return res;
        }

        private bool MisalignedRead(ulong addr, out int value)
        {
            var res = _dataReader.ReadMemory(addr, _dword, _dword.Length, out var size);
            value = BitConverter.ToInt32(_dword, 0);
            return res;
        }

        protected virtual bool MoveToPage(ulong addr)
        {
            return ReadMemory(addr);
        }

        protected virtual bool ReadMemory(ulong addr)
        {
            _currPageStart = addr;
            var res = _dataReader.ReadMemory(_currPageStart, _data, _cacheSize, out _currPageSize);

            if (!res)
            {
                _currPageStart = 0;
                _currPageSize = 0;
            }

            return res;
        }
    }
}