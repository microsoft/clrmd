using System;
using System.Buffers;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    // TODO: REmove this code
    static class ValueReader
    {
        private static bool _initializedStringFields;
        private static ClrInstanceField _firstChar;
        private static ClrInstanceField _stringLength;

        internal static object GetValueAtAddress(ClrHeap heap, IDataReader reader, ClrElementType cet, ulong addr)
        {
            switch (cet)
            {
                case ClrElementType.String:
                    return GetStringContents(heap.StringType, reader, addr);

                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                    {
                        return reader.ReadPointerUnsafe(addr);
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
                        return reader.ReadPointerUnsafe(addr);
                    }

                case ClrElementType.NativeInt: // native int
                    {
                        return reader.ReadPointerUnsafe(addr);
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

        internal static string GetStringContents(ClrType stringType, IDataReader reader, ulong strAddr)
        {
            if (strAddr == 0)
                return null;

            if (!_initializedStringFields)
            {
                _firstChar = stringType.GetFieldByName("m_firstChar");
                _stringLength = stringType.GetFieldByName("m_stringLength");

                // .Type being null can happen in minidumps.  In that case we will fall back to
                // hardcoded values and hope they don't get out of date.
                if (_firstChar?.Type == null)
                    _firstChar = null;

                if (_stringLength?.Type == null)
                    _stringLength = null;

                _initializedStringFields = true;
            }

            if (_firstChar == null || _stringLength == null)
                return string.Empty;

            int length = (int)_stringLength.GetValue(strAddr);
            ulong data = _firstChar.GetAddress(strAddr);

            return ReadString(reader, data, length);
        }

        internal static string ReadString(IDataReader reader, ulong dataAddress, int length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length * 2);
            try
            {
                if (!reader.ReadMemory(dataAddress, new Span<byte>(buffer, 0, length * 2), out int count))
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
