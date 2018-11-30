// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            int newCapacity = Position + count;
            if (Buffer.Length < newCapacity)
            {
                byte[] newBuffer = new byte[newCapacity];
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
            for (int i = 0; i < values.Length; i++)
            {
                ReadInt32(out values[i]);
            }
        }

        internal void ReadUInt32(uint[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                ReadUInt32(out values[i]);
            }
        }

        internal void ReadBytes(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Buffer[Position++];
            }
        }

        internal float ReadFloat()
        {
            float result = BitConverter.ToSingle(Buffer, Position);
            Position += 4;
            return result;
        }

        internal double ReadDouble()
        {
            double result = BitConverter.ToDouble(Buffer, Position);
            Position += 8;
            return result;
        }

        internal decimal ReadDecimal()
        {
            int[] bits = new int[4];
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
            int len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len++;
            }

            value = Encoding.UTF8.GetString(Buffer, Position, len);
            Position += len + 1;
        }

        internal void SkipCString(out string value)
        {
            int len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len++;
            }

            Position += len + 1;
            value = null;
        }

        internal void ReadGuid(out Guid guid)
        {
            ReadUInt32(out uint a);
            ReadUInt16(out ushort b);
            ReadUInt16(out ushort c);
            ReadUInt8(out byte d);
            ReadUInt8(out byte e);
            ReadUInt8(out byte f);
            ReadUInt8(out byte g);
            ReadUInt8(out byte h);
            ReadUInt8(out byte i);
            ReadUInt8(out byte j);
            ReadUInt8(out byte k);

            guid = new Guid(a, b, c, d, e, f, g, h, i, j, k);
        }

        internal string ReadString()
        {
            int len = 0;
            while (Position + len < Buffer.Length && Buffer[Position + len] != 0)
            {
                len += 2;
            }

            string result = Encoding.Unicode.GetString(Buffer, Position, len);
            Position += len + 2;
            return result;
        }
    }
}