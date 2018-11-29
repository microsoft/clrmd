using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A PEBuffer represents 
    /// </summary>
    internal unsafe sealed class PEBuffer : IDisposable
    {
        public PEBuffer(Stream stream, int buffSize = 512)
        {
            _stream = stream;
            GetBuffer(buffSize);
        }

        ~PEBuffer()
        {
            if (_pinningHandle.IsAllocated)
                _pinningHandle.Free();
        }

        public byte* Fetch(int filePos, int size)
        {
            if (size > _buff.Length)
                GetBuffer(size);
            if (!(_buffPos <= filePos && filePos + size <= _buffPos + _buffLen))
            {
                // Read in the block of 'size' bytes at filePos
                _buffPos = filePos;
                _stream.Seek(_buffPos, SeekOrigin.Begin);
                _buffLen = 0;
                while (_buffLen < _buff.Length)
                {
                    var count = _stream.Read(_buff, _buffLen, size - _buffLen);
                    if (count == 0)
                        break;

                    _buffLen += count;
                }
            }

            return &_buffPtr[filePos - _buffPos];
        }

        public byte* Buffer
        {
            get { return _buffPtr; }
        }
        public int Length
        {
            get { return _buffLen; }
        }

        public void Dispose()
        {
            if (_pinningHandle.IsAllocated)
                _pinningHandle.Free();

            GC.SuppressFinalize(this);
        }

        #region private
        private void GetBuffer(int buffSize)
        {
            if (_pinningHandle.IsAllocated)
                _pinningHandle.Free();

            _buff = new byte[buffSize];
            _pinningHandle = GCHandle.Alloc(_buff, GCHandleType.Pinned);
            fixed (byte* ptr = _buff)
                _buffPtr = ptr;
            _buffLen = 0;
        }

        private int _buffPos;
        private int _buffLen; // Number of valid bytes in _buff
        private byte[] _buff;
        private byte* _buffPtr;
        private GCHandle _pinningHandle;
        private Stream _stream;
        #endregion
    }
}