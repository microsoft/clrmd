// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A PEBuffer represents
    /// </summary>
    internal sealed unsafe class PEBuffer : IDisposable
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
            if (!(_buffPos <= filePos && filePos + size <= _buffPos + Length))
            {
                // Read in the block of 'size' bytes at filePos
                _buffPos = filePos;
                _stream.Seek(_buffPos, SeekOrigin.Begin);
                Length = 0;
                while (Length < _buff.Length)
                {
                    int count = _stream.Read(_buff, Length, size - Length);
                    if (count == 0)
                        break;

                    Length += count;
                }
            }

            return &Buffer[filePos - _buffPos];
        }

        public byte* Buffer { get; private set; }
        public int Length { get; private set; }

        public void Dispose()
        {
            if (_pinningHandle.IsAllocated)
                _pinningHandle.Free();

            GC.SuppressFinalize(this);
        }

        private void GetBuffer(int buffSize)
        {
            if (_pinningHandle.IsAllocated)
                _pinningHandle.Free();

            _buff = new byte[buffSize];
            _pinningHandle = GCHandle.Alloc(_buff, GCHandleType.Pinned);
            fixed (byte* ptr = _buff)
                Buffer = ptr;
            Length = 0;
        }

        private int _buffPos;
        private byte[] _buff;
        private GCHandle _pinningHandle;
        private readonly Stream _stream;
    }
}