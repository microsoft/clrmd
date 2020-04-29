// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdArrayType : ClrmdType
    {
        private int _baseArrayOffset;
        private ClrType? _componentType;

        public override int ComponentSize { get; }
        public override ClrType? ComponentType => _componentType;

        public ClrmdArrayType(ClrHeap heap, ClrType? baseType, ClrModule? module, ITypeData data, string? name = null)
            : base(heap, baseType, module, data, name)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            ComponentSize = data.ComponentSize;
            ulong componentMT = data.ComponentMethodTable;
            if (componentMT != 0)
                _componentType = Helpers.Factory.GetOrCreateType(heap, componentMT, 0);
        }

        public void SetComponentType(ClrType? type) => _componentType = type;

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            // todo: remove?
            if (_baseArrayOffset == 0)
            {
                ClrType? componentType = ComponentType;

                IObjectData data = Helpers.GetObjectData(objRef);
                if (data != null)
                {
                    _baseArrayOffset = (int)(data.DataPointer - objRef);
                    DebugOnly.Assert(_baseArrayOffset >= 0);
                }
                else if (componentType != null)
                {
                    if (!componentType.IsObjectReference)
                        _baseArrayOffset = IntPtr.Size * 2;
                }
                else
                {
                    return 0;
                }
            }

            return objRef + (ulong)(_baseArrayOffset + index * ComponentSize);
        }

        public override object? GetArrayElementValue(ulong objRef, int index)
        {
            ulong address = GetArrayElementAddress(objRef, index);
            if (address == 0)
                return null;

            ClrType? componentType = ComponentType;
            ClrElementType cet;
            if (componentType != null)
            {
                cet = componentType.ElementType;
            }
            else
            {
                // Slow path, we need to get the element type of the array.
                IObjectData data = Helpers.GetObjectData(objRef);
                if (data is null)
                    return null;

                cet = data.ElementType;
            }

            if (cet == ClrElementType.Unknown)
                return null;

            if (cet == ClrElementType.String)
                address = DataReader.ReadPointer(address);

            return GetValueAtAddress(DataReader, cet, address);
        }


        internal object? GetValueAtAddress(IDataReader reader, ClrElementType cet, ulong addr)
        {
            // TODO: delete this, factor GetArrayElementValue into its component types.
            switch (cet)
            {
                case ClrElementType.String:
                    return Helpers.ClrObjectHelpers.ReadString(addr, 4096);

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


        public override T[]? GetArrayElementValues<T>(ulong objRef, int count)
        {
            ulong address = GetArrayElementAddress(objRef, 0);
            ClrType? componentType = ComponentType;
            ClrElementType cet;
            if (componentType != null)
            {
                cet = componentType.ElementType;
            }
            else
            {
                // Slow path, we need to get the element type of the array.
                IObjectData data = Helpers.GetObjectData(objRef);
                if (data is null)
                    return null;

                cet = data.ElementType;
            }

            if (cet == ClrElementType.Unknown)
                return null;

            if (address == 0)
                return null;

            var values = new T[count];
            Span<byte> buffer = MemoryMarshal.Cast<T, byte>(values);

            if (DataReader.Read(address, buffer, out int bytesRead) && bytesRead == buffer.Length)
            {
                return values;
            }

            return default;
        }
    }
}
