// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class BitAccess
    {
        internal BitAccess(int capacity)
        {
            Buffer = new byte[capacity];
        }

        internal byte[] Buffer { get; private set; }

        internal void FillBuffer(Stream stream, int capacity)
        {
            MinCapacity(capacity);
            stream.Read(Buffer, 0, capacity);
            Position = 0;
        }

        internal void Append(Stream stream, int count)
        {
            var newCapacity = Position + count;
            if (Buffer.Length < newCapacity)
            {
                var newBuffer = new byte[newCapacity];
                Array.Copy(Buffer, newBuffer, Buffer.Length);
                Buffer = newBuffer;
            }

            stream.Read(Buffer, Position, count);
            Position += count;
        }

        internal int Position { get; set; }

        internal void MinCapacity(int capacity)
        {
            if (Buffer.Length < capacity)
            {
                Buffer = new byte[capacity];
            }

            Position = 0;
        }

        internal void Align(int alignment)
        {
            while (Position % alignment != 0)
            {
                Position++;
            }
        }

        internal void ReadInt16(out short value)
        {
            unchecked
            {
                value = (short)((Buffer[Position + 0] & 0xFF) |
                    (Buffer[Position + 1] << 8));
                Position += 2;
            }
        }

        internal void ReadInt8(out sbyte value)
        {
            value = (sbyte)Buffer[Position];
            Position += 1;
        }

        internal void ReadInt32(out int value)
        {
            unchecked
            {
                value = (Buffer[Position + 0] & 0xFF) |
                    (Buffer[Position + 1] << 8) |
                    (Buffer[Position + 2] << 16) |
                    (Buffer[Position + 3] << 24);
            }

            Position += 4;
        }

        internal void ReadInt64(out long value)
        {
            unchecked
            {
                value = (long)(((ulong)Buffer[Position + 0] & 0xFF) |
                    ((ulong)Buffer[Position + 1] << 8) |
                    ((ulong)Buffer[Position + 2] << 16) |
                    ((ulong)Buffer[Position + 3] << 24) |
                    ((ulong)Buffer[Position + 4] << 32) |
                    ((ulong)Buffer[Position + 5] << 40) |
                    ((ulong)Buffer[Position + 6] << 48) |
                    ((ulong)Buffer[Position + 7] << 56));
            }

            Position += 8;
        }

        internal void ReadUInt16(out ushort value)
        {
            unchecked
            {
                value = (ushort)((Buffer[Position + 0] & 0xFF) |
                    (Buffer[Position + 1] << 8));
            }

            Position += 2;
        }

        internal void ReadUInt8(out byte value)
        {
            unchecked
            {
                value = (byte)(Buffer[Position + 0] & 0xFF);
            }

            Position += 1;
        }

        internal void ReadUInt32(out uint value)
        {
            unchecked
            {
                value = (uint)((Buffer[Position + 0] & 0xFF) |
                    (Buffer[Position + 1] << 8) |
                    (Buffer[Position + 2] << 16) |
                    (Buffer[Position + 3] << 24));
                Position += 4;
            }
        }

        internal void ReadUInt64(out ulong value)
        {
            unchecked
            {
                value = ((ulong)Buffer[Position + 0] & 0xFF) |
                    ((ulong)Buffer[Position + 1] << 8) |
                    ((ulong)Buffer[Position + 2] << 16) |
                    ((ulong)Buffer[Position + 3] << 24) |
                    ((ulong)Buffer[Position + 4] << 32) |
                    ((ulong)Buffer[Position + 5] << 40) |
                    ((ulong)Buffer[Position + 6] << 48) |
                    ((ulong)Buffer[Position + 7] << 56);
                Position += 8;
            }
        }

        internal void ReadInt32(int[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                ReadInt32(out values[i]);
            }
        }

        internal void ReadUInt32(uint[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                ReadUInt32(out values[i]);
            }
        }

        internal void ReadBytes(byte[] bytes)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Buffer[Position++];
            }
        }

        internal float ReadFloat()
        {
            var result = BitConverter.ToSingle(Buffer, Position);
            Position += 4;
            return result;
        }

        internal double ReadDouble()
        {
            var result = BitConverter.ToDouble(Buffer, Position);
            Position += 8;
            return result;
        }

        internal decimal ReadDecimal()
        {
            var bits = new int[4];
            ReadInt32(bits);
            return new decimal(bits[2], bits[3], bits[1], bits[0] < 0, (byte)((bits[0] & 0x00FF0000) >> 16));
        }

        internal void ReadBString(out string value)
        {
            ushort len;
            ReadUInt16(out len);
            value = Encoding.UTF8.GetString(Buffer, Position, len);
            Position += len;
        }

        internal void ReadCString(out string value)
        {
            var len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len++;
            }

            value = Encoding.UTF8.GetString(Buffer, Position, len);
            Position += len + 1;
        }

        internal void SkipCString(out string value)
        {
            var len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len++;
            }

            Position += len + 1;
            value = null;
        }

        internal void ReadGuid(out Guid guid)
        {
            ReadUInt32(out var a);
            ReadUInt16(out var b);
            ReadUInt16(out var c);
            ReadUInt8(out var d);
            ReadUInt8(out var e);
            ReadUInt8(out var f);
            ReadUInt8(out var g);
            ReadUInt8(out var h);
            ReadUInt8(out var i);
            ReadUInt8(out var j);
            ReadUInt8(out var k);

            guid = new Guid(a, b, c, d, e, f, g, h, i, j, k);
        }

        internal string ReadString()
        {
            var len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len += 2;
            }

            var result = Encoding.Unicode.GetString(Buffer, Position, len);
            Position += len + 2;
            return result;
        }
    }
}