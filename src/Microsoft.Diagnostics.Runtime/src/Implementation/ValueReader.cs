// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    // TODO: Factor this into the appropriate place
    internal static class ValueReader
    {
        private static bool _initializedStringFields;
        private static ClrType? _stringType;
        private static ClrInstanceField? _firstChar;
        private static ClrInstanceField? _stringLength;

        internal static object? GetValueAtAddress(ClrHeap heap, IDataReader reader, ClrElementType cet, ulong addr)
        {
            switch (cet)
            {
                case ClrElementType.String:
                    return GetStringContents(heap.StringType, reader, addr, 4096);

                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                    {
                        return reader.ReadPointer(addr);
                    }

                case ClrElementType.Boolean:
                    {
                        if (!reader.Read(addr, out byte val))
                            return null;

                        return val != 0;
                    }

                case ClrElementType.Int32:
                    {
                        if (!reader.Read(addr, out int val))
                            return null;

                        return val;
                    }

                case ClrElementType.UInt32:
                    {
                        if (!reader.Read(addr, out uint val))
                            return null;

                        return val;
                    }

                case ClrElementType.Int64:
                    {
                        if (!reader.Read(addr, out long val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.UInt64:
                    {
                        if (!reader.Read(addr, out ulong val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.NativeUInt: // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                    {
                        return reader.ReadPointer(addr);
                    }

                case ClrElementType.NativeInt: // native int
                    {
                        return reader.ReadPointer(addr);
                    }

                case ClrElementType.Int8:
                    {
                        if (!reader.Read(addr, out sbyte val))
                            return null;

                        return val;
                    }

                case ClrElementType.UInt8:
                    {
                        if (!reader.Read(addr, out byte val))
                            return null;

                        return val;
                    }

                case ClrElementType.Float:
                    {
                        if (!reader.Read(addr, out float val))
                            return null;

                        return val;
                    }

                case ClrElementType.Double: // double
                    {
                        if (!reader.Read(addr, out double val))
                            return null;

                        return val;
                    }

                case ClrElementType.Int16:
                    {
                        if (!reader.Read(addr, out short val))
                            return null;

                        return val;
                    }

                case ClrElementType.Char: // u2
                    {
                        if (!reader.Read(addr, out ushort val))
                            return null;

                        return (char)val;
                    }

                case ClrElementType.UInt16:
                    {
                        if (!reader.Read(addr, out ushort val))
                            return null;

                        return val;
                    }
            }

            throw new Exception("Unexpected element type.");
        }

        internal static string? GetStringContents(ClrType stringType, IDataReader reader, ulong strAddr, int maxLen)
        {
            if (strAddr == 0)
                return null;

            _initializedStringFields = _initializedStringFields && _stringType == stringType;

            if (!_initializedStringFields)
            {
                // since .NET Core 2.1
                _firstChar = stringType.GetFieldByName("_firstChar");
                _stringLength = stringType.GetFieldByName("_stringLength");

                _firstChar ??= stringType.GetFieldByName("m_firstChar");
                _stringLength ??= stringType.GetFieldByName("m_stringLength");

                // .Type being null can happen in minidumps.  In that case we will fall back to
                // hardcoded values and hope they don't get out of date.
                if (_firstChar?.Type is null)
                    _firstChar = null;

                if (_stringLength?.Type is null)
                    _stringLength = null;

                _stringType = stringType;
                _initializedStringFields = true;
            }

            if (_firstChar is null || _stringLength is null)
                return string.Empty;

            int length = _stringLength.Read<int>(strAddr, interior: false);
            ulong data = _firstChar.GetAddress(strAddr);

            length = Math.Min(length, maxLen);
            return ReadString(reader, data, length);
        }

        internal static string? ReadString(IDataReader reader, ulong dataAddress, int length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length * 2);
            try
            {
                if (!reader.Read(dataAddress, new Span<byte>(buffer, 0, length * 2), out int count))
                    return null;

                return Encoding.Unicode.GetString(buffer, 0, count);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
