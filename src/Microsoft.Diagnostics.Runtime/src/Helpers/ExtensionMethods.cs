using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public unsafe static ulong AsPointer(this Span<byte> span, int index = 0)
        {
            if (index != 0)
                span = span.Slice(index * IntPtr.Size, IntPtr.Size);

            Debug.Assert(span.Length >= IntPtr.Size);
            fixed (byte* ptr = span)
            {
                if (IntPtr.Size == 4)
                    return Unsafe.ReadUnaligned<uint>(ptr);

                return Unsafe.ReadUnaligned<ulong>(ptr);
            }
        }


        public unsafe static uint AsUInt32(this Span<byte> span)
        {
            Debug.Assert(span.Length >= 4);
            fixed (byte* ptr = span)
                return Unsafe.ReadUnaligned<uint>(ptr);
        }

        public unsafe static int AsInt32(this Span<byte> span)
        {
            Debug.Assert(span.Length >= 4);
            fixed (byte* ptr = span)
                return Unsafe.ReadUnaligned<int>(ptr);
        }
    }
}
