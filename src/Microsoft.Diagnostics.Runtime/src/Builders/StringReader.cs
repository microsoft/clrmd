// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed class StringReader
    {
        private readonly uint _firstChar = (uint)IntPtr.Size + 4;
        private readonly uint _stringLength = (uint)IntPtr.Size;

        public IDataReader DataReader { get; }

        public StringReader(IDataReader reader, ClrType stringType)
        {
            if (!stringType.IsString)
                throw new InvalidOperationException($"Type {stringType?.Name} is not the string type.");

            DataReader = reader;
        }

        public string? ReadString(IDataReader reader, ulong address, int maxLength)
        {
            if (address == 0)
                return null;

            int length = DataReader.Read<int>(address + _stringLength);
            length = Math.Min(length, maxLength);
            if (length == 0)
                return string.Empty;

            ulong data = address + _firstChar;
            char[] buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                Span<char> charSpan = new Span<char>(buffer).Slice(0, length);
                Span<byte> bytes = MemoryMarshal.AsBytes(charSpan);
                if (!reader.Read(data, bytes, out int count))
                    return null;

                return Encoding.Unicode.GetString(bytes.Slice(0, count));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }
}
