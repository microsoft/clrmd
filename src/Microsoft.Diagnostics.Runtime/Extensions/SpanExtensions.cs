// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NETCOREAPP
using System.Buffers;
using System.IO;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !NETCOREAPP
using System.Text;
#endif

namespace Microsoft.Diagnostics.Runtime
{
    internal static class SpanExtensions
    {
#if !NETCOREAPP
        public static int Read(this Stream stream, Span<byte> span)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(span.Length);
            try
            {
                int numRead = stream.Read(buffer, 0, span.Length);
                new ReadOnlySpan<byte>(buffer, 0, numRead).CopyTo(span);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
#endif

#if !NETCOREAPP
        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
                return string.Empty;

            fixed (byte* bytesPtr = bytes)
            {
                return encoding.GetString(bytesPtr, bytes.Length);
            }
        }
#endif
        public static unsafe TargetPointer AsPointer(this Span<byte> span, int pointerSize, int offset)
        {
            if (offset > 0)
                span = span.Slice(offset);

            if (pointerSize == 8)
            {
                DebugOnly.Assert(span.Length >= sizeof(ulong));
                return Unsafe.As<byte, ulong>(ref MemoryMarshal.GetReference(span));
            }
            else
            {
                DebugOnly.Assert(span.Length >= sizeof(uint));
                return Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(span));
            }
        }

        public static unsafe TargetPointer AsPointer(this Span<byte> span, int pointerSize, ulong offset) => AsPointer(span, pointerSize, (int)offset);

        public static unsafe int AsInt32(this Span<byte> span, int offset = 0)
        {
            if (offset > 0)
                span = span.Slice(offset);

            DebugOnly.Assert(span.Length >= sizeof(int));
            DebugOnly.Assert(unchecked((int)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))) % sizeof(int) == 0);
            return Unsafe.As<byte, int>(ref MemoryMarshal.GetReference(span));
        }

        public static unsafe uint AsUInt32(this Span<byte> span, int offset = 0)
        {
            if (offset > 0)
                span = span.Slice(offset);

            DebugOnly.Assert(span.Length >= sizeof(uint));
            DebugOnly.Assert(unchecked((int)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))) % sizeof(uint) == 0);
            return Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(span));
        }

        public static unsafe uint AsUInt32(this Span<byte> span, ulong offset = 0) => AsUInt32(span, (int)offset);
        public static unsafe uint AsUInt32(this Span<byte> span) => AsUInt32(span, 0);

#if !NETCOREAPP3_1
        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            return source.Contains(value, comparisonType);
        }
#endif
    }
}