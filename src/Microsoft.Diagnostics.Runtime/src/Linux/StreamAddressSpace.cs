using System.IO;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class StreamAddressSpace : IAddressSpace
    {
        private readonly Stream _stream;

        public StreamAddressSpace(Stream stream) => _stream = stream;

        public long Length => _stream.Length;

        public string Name => _stream.GetFilename() ?? _stream.GetType().Name;

        public int Read(long position, byte[] buffer, int bufferOffset, int count)
        {
            _stream.Seek(position, SeekOrigin.Begin);
            return _stream.Read(buffer, bufferOffset, count);
        }
    }
}
