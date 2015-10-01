// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ReadVirtualStream : Stream
    {
        private byte[] _tmp;
        private long _pos;
        private long _disp;
        private long _len;
        private IDataReader _dataReader;

        public ReadVirtualStream(IDataReader dataReader, long displacement, long len)
        {
            _dataReader = dataReader;
            _disp = displacement;
            _len = len;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                return _pos;
            }
            set
            {
                _pos = value;
                if (_pos > _len)
                    _pos = _len;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset == 0)
            {
                int read;
                if (_dataReader.ReadMemory((ulong)(_pos + _disp), buffer, count, out read))
                    return read;

                return 0;
            }
            else
            {
                if (_tmp == null || _tmp.Length < count)
                    _tmp = new byte[count];

                int read;
                if (!_dataReader.ReadMemory((ulong)(_pos + _disp), _tmp, count, out read))
                    return 0;

                Buffer.BlockCopy(_tmp, 0, buffer, offset, read);
                return read;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _pos = offset;
                    break;

                case SeekOrigin.End:
                    _pos = _len + offset;
                    if (_pos > _len)
                        _pos = _len;
                    break;

                case SeekOrigin.Current:
                    _pos += offset;
                    if (_pos > _len)
                        _pos = _len;
                    break;
            }

            return _pos;
        }

        public override void SetLength(long value)
        {
            _len = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }

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
        #region Variables
        protected ulong _currPageStart;
        protected int _currPageSize;
        protected byte[] _data;
        private byte[] _ptr;
        private byte[] _dword;
        protected IDataReader _dataReader;
        protected int _cacheSize;
        #endregion

        public MemoryReader(IDataReader dataReader, int cacheSize)
        {
            _data = new byte[cacheSize];
            _dataReader = dataReader;
            uint sz = _dataReader.GetPointerSize();
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
            if ((addr < _currPageStart) || (addr >= _currPageStart + (uint)_currPageSize))
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
            uint tmp = 0;
            bool res = ReadDword(addr, out tmp);

            value = (int)tmp;
            return res;
        }

        internal bool TryReadPtr(ulong addr, out ulong value)
        {
            if ((_currPageStart <= addr) && (addr - _currPageStart < (uint)_currPageSize))
            {
                ulong offset = addr - _currPageStart;
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
            if ((_currPageStart <= addr) && (addr - _currPageStart < (uint)_currPageSize))
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
            if ((_currPageStart <= addr) && (addr - _currPageStart < (uint)_currPageSize))
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
            if ((addr < _currPageStart) || (addr - _currPageStart > (uint)_currPageSize))
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

            // If MoveToPage succeeds, we MUST be on the right page.
            Debug.Assert(addr >= _currPageStart);

            // However, the amount of data requested may fall off of the page.  In that case,
            // fall back to MisalignedRead.
            ulong offset = addr - _currPageStart;
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

        virtual public void EnsureRangeInCache(ulong addr)
        {
            if (!Contains(addr))
                MoveToPage(addr);
        }


        public bool Contains(ulong addr)
        {
            return ((_currPageStart <= addr) && (addr - _currPageStart < (uint)_currPageSize));
        }

        #region Private Functions
        private bool MisalignedRead(ulong addr, out ulong value)
        {
            int size = 0;
            bool res = _dataReader.ReadMemory(addr, _ptr, _ptr.Length, out size);
            fixed (byte* b = _ptr)
                if (_ptr.Length == 4)
                value = *((uint*)b);
            else
                value = *((ulong*)b);
            return res;
        }

        private bool MisalignedRead(ulong addr, out uint value)
        {
            int size = 0;
            bool res = _dataReader.ReadMemory(addr, _dword, _dword.Length, out size);
            value = BitConverter.ToUInt32(_dword, 0);
            return res;
        }

        private bool MisalignedRead(ulong addr, out int value)
        {
            int size = 0;
            bool res = _dataReader.ReadMemory(addr, _dword, _dword.Length, out size);
            value = BitConverter.ToInt32(_dword, 0);
            return res;
        }

        virtual protected bool MoveToPage(ulong addr)
        {
            return ReadMemory(addr);
        }

        protected virtual bool ReadMemory(ulong addr)
        {
            _currPageStart = addr;
            bool res = _dataReader.ReadMemory(_currPageStart, _data, _cacheSize, out _currPageSize);

            if (!res)
            {
                _currPageStart = 0;
                _currPageSize = 0;
            }

            return res;
        }
        #endregion
    }
    
    internal class GCDesc
    {
        private static readonly int s_GCDescSize = IntPtr.Size * 2;

        #region Variables
        private byte[] _data;
        #endregion

        #region Functions
        public GCDesc(byte[] data)
        {
            _data = data;
        }

        public void WalkObject(ulong addr, ulong size, MemoryReader cache, Action<ulong, int> refCallback)
        {
            Debug.Assert(size >= (ulong)IntPtr.Size);

            int series = GetNumSeries();
            int highest = GetHighestSeries();
            int curr = highest;

            if (series > 0)
            {
                int lowest = GetLowestSeries();
                do
                {
                    ulong ptr = addr + GetSeriesOffset(curr);
                    ulong stop = (ulong)((long)ptr + (long)GetSeriesSize(curr) + (long)size);

                    while (ptr < stop)
                    {
                        ulong ret;
                        if (cache.ReadPtr(ptr, out ret) && ret != 0)
                            refCallback(ret, (int)(ptr - addr));

                        ptr += (ulong)IntPtr.Size;
                    }

                    curr -= s_GCDescSize;
                } while (curr >= lowest);
            }
            else
            {
                ulong ptr = addr + GetSeriesOffset(curr);

                while (ptr < (addr + size - (ulong)IntPtr.Size))
                {
                    for (int i = 0; i > series; i--)
                    {
                        uint nptrs = GetPointers(curr, i);
                        uint skip = GetSkip(curr, i);

                        ulong stop = ptr + (ulong)(nptrs * IntPtr.Size);
                        do
                        {
                            ulong ret;
                            if (cache.ReadPtr(ptr, out ret) && ret != 0)
                                refCallback(ret, (int)(ptr - addr));

                            ptr += (ulong)IntPtr.Size;
                        } while (ptr < stop);

                        ptr += skip;
                    }
                }
            }
        }
        #endregion

        #region Private Functions
        private uint GetPointers(int curr, int i)
        {
            int offset = i * IntPtr.Size;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);
            else
                return BitConverter.ToUInt32(_data, curr + offset);
        }

        private uint GetSkip(int curr, int i)
        {
            int offset = i * IntPtr.Size + IntPtr.Size / 2;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(_data, curr + offset);
            else
                return BitConverter.ToUInt32(_data, curr + offset);
        }

        private int GetSeriesSize(int curr)
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(_data, curr);
            else
                return (int)BitConverter.ToInt64(_data, curr);
        }

        private ulong GetSeriesOffset(int curr)
        {
            ulong offset;
            if (IntPtr.Size == 4)
                offset = BitConverter.ToUInt32(_data, curr + IntPtr.Size);
            else
                offset = BitConverter.ToUInt64(_data, curr + IntPtr.Size);

            return offset;
        }

        private int GetHighestSeries()
        {
            return _data.Length - IntPtr.Size * 3;
        }

        private int GetLowestSeries()
        {
            return _data.Length - ComputeSize(GetNumSeries());
        }

        static private int ComputeSize(int series)
        {
            return IntPtr.Size + series * IntPtr.Size * 2;
        }

        private int GetNumSeries()
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(_data, _data.Length - IntPtr.Size);
            else
                return (int)BitConverter.ToInt64(_data, _data.Length - IntPtr.Size);
        }
        #endregion
    }
}