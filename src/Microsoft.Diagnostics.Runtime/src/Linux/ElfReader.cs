using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class Reader : IDisposable
    {
        public const int MaxHeldBuffer = 4096;
        public const int InitialBufferSize = 64;

        private byte[] _buffer;
        private GCHandle _handle;
        private bool _disposed;


        public IAddressSpace DataSource { get; }

        public Reader(IAddressSpace source)
        {
            DataSource = source;
            _buffer = new byte[512];
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }
        
        public T Read<T>(long position) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            EnsureSize(size);

            int read = DataSource.Read(position, _buffer, 0, size);
            if (read != size)
                throw new IOException();

            T result = (T)Marshal.PtrToStructure(_handle.AddrOfPinnedObject(), typeof(T));
            return result;
        }

        public T Read<T>(ref long position) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            EnsureSize(size);

            int read = DataSource.Read(position, _buffer, 0, size);
            if (read != size)
                throw new IOException();

            T result = (T)Marshal.PtrToStructure(_handle.AddrOfPinnedObject(), typeof(T));

            position += size;
            return result;
        }

        public byte[] ReadBytes(long offset, int size)
        {
            byte[] buffer = new byte[size];
            int read = DataSource.Read(offset, buffer, 0, size);

            if (read != size)
                throw new IOException();

            return buffer;
        }

        private void EnsureSize(int size)
        {
            if (_buffer.Length < size)
            {
                if (size > MaxHeldBuffer)
                    throw new InvalidOperationException();

                _handle.Free();

                _buffer = new byte[size];
                _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            }
        }

        ~Reader() => Dispose(false);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _handle.Free();
            }
        }

        public string ReadNullTerminatedAscii(long position, int len)
        {
            byte[] buffer = _buffer;
            if (len > _buffer.Length)
                buffer = new byte[len];

            int read = DataSource.Read(position, buffer, 0, len);
            if (read == 0)
                return "";
            
            if (buffer[read - 1] == 0)
                read--;

            return Encoding.ASCII.GetString(buffer, 0, read);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
