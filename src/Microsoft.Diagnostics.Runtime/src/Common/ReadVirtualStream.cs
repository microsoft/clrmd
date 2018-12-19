// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ReadVirtualStream : Stream
    {
        private byte[] _tmp;
        private long _pos;
        private readonly long _disp;
        private long _len;
        private readonly IDataReader _dataReader;

        public ReadVirtualStream(IDataReader dataReader, long displacement, long len)
        {
            _dataReader = dataReader;
            _disp = displacement;
            _len = len;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;

        public override void Flush()
        {
        }

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => _pos;
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
                if (_dataReader.ReadMemory((ulong)(_pos + _disp), buffer, count, out int read))
                {
                    if (read > 0)
                        _pos += read;

                    return read;
                }

                return 0;
            }
            else
            {
                if (_tmp == null || _tmp.Length < count)
                    _tmp = new byte[count];

                if (!_dataReader.ReadMemory((ulong)(_pos + _disp), _tmp, count, out int read))
                    return 0;

                if (read > 0)
                {
                    Buffer.BlockCopy(_tmp, 0, buffer, offset, read);
                    _pos += read;
                }

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
}