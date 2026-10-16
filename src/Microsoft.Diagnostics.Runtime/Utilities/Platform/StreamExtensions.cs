// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities.Platform;

internal static class StreamExtensions
{
    extension(Stream stream)
    {
        public bool TryReadUleb128(out ulong number, long? end = null)
        {
            number = 0;
            int shift = 0;

            for (int i = 0; i < 10; i++)
            {
                if (!stream.TryReadByte(out byte value, end))
                {
                    return false;
                }

                ulong payload = (uint)(value & 0x7f);
                if (shift == 63 && payload > 1)
                {
                    return false;
                }

                number |= payload << shift;
                if ((value & 0x80) == 0)
                {
                    return true;
                }

                shift += 7;
            }

            return false;
        }

        public bool TryReadByte(out byte value, long? end = null)
        {
            value = 0;

            if (end.HasValue && stream.Position >= end)
            {
                return false;
            }

            int byteRead = stream.ReadByte();
            if (byteRead == -1)
            {
                value = 0;
                return false;
            }

            value = (byte)byteRead;
            return true;
        }

        public void WriteUleb128(ulong value)
        {
            do
            {
                byte next = (byte)(value & 0x7f);
                value >>= 7;
                if (value != 0)
                {
                    next |= 0x80;
                }

                stream.WriteByte(next);
            }
            while (value != 0);
        }
    }
}