using Microsoft.Diagnostics.Runtime.Windows;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    abstract class MinidumpMemoryReader : IMemoryReader, IDisposable
    {
        public abstract int PointerSize { get; }

        public abstract void Dispose();

        public abstract int Read(ulong address, Span<byte> buffer);
        public abstract bool Read<T>(ulong address, out T value) where T : unmanaged;
        public abstract T Read<T>(ulong address) where T : unmanaged;
        public abstract bool ReadPointer(ulong address, out ulong value);
        public abstract ulong ReadPointer(ulong address);

        public abstract int ReadFromRva(ulong rva, Span<byte> buffer);

        public string? ReadCountedUnicode(ulong rva)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                int count;

                Span<byte> span = new Span<byte>(buffer);
                if (ReadFromRva(rva, span.Slice(0, sizeof(int))) != sizeof(int))
                    return null;

                int len = Unsafe.As<byte, int>(ref buffer[0]);
                len = Math.Min(len, buffer.Length);

                if (len <= 0)
                    return null;

                count = ReadFromRva(rva + sizeof(int), buffer);
                string result = Encoding.Unicode.GetString(buffer, 0, count);

                int index = result.IndexOf('\0');
                if (index != -1)
                    result = result.Substring(0, index);

                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
