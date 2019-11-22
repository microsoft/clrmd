using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    static class ExtensionMethods
    {
        public static int Read(this Stream stream, Span<byte> span)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(span.Length);
            try
            {
                int numRead = stream.Read(buffer, 0, span.Length);
                new Span<byte>(buffer, 0, numRead).CopyTo(span);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
