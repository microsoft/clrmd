// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrmdField : ClrInstanceField
    {
        private readonly IClrFieldHelpers _helpers;
        private string? _name;
        private ClrType? _type;
        private FieldAttributes _attributes = FieldAttributes.ReservedMask;

        public override ClrElementType ElementType { get; }

        public override bool IsObjectReference => ElementType.IsObjectReference();
        public override bool IsValueType => ElementType.IsValueType();
        public override bool IsPrimitive => ElementType.IsPrimitive();

        public override string? Name
        {
            get
            {
                if (_name != null)
                    return _name;

                return ReadData();
            }
        }

        public override ClrType? Type
        {
            get
            {
                if (_type != null)
                    return _type;

                InitData();
                return _type;
            }
        }

        public override int Size => GetSize(Type, ElementType);

        public override int Token { get; }
        public override int Offset { get; }

        public override ClrType ContainingType { get; }

        public ClrmdField(ClrType containingType, ClrType? type, IClrFieldHelpers helpers, in FieldData data)
        {
            if (containingType is null)
                throw new ArgumentNullException(nameof(containingType));

            ContainingType = containingType;
            Token = (int)data.FieldToken;
            ElementType = (ClrElementType)data.ElementType;
            Offset = (int)data.Offset;

            _helpers = helpers;

            // Must be the last use of 'data' in this constructor.
            _type = type;
            if (ElementType == ClrElementType.Class && _type != null)
                ElementType = _type.ElementType;

            DebugOnlyLoadLazyValues();
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            InitData();
        }

        public override bool IsPublic
        {
            get
            {
                InitData();
                return (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
            }
        }

        public override bool IsPrivate
        {
            get
            {
                InitData();
                return (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
            }
        }

        public override bool IsInternal
        {
            get
            {
                InitData();
                return (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
            }
        }

        public override bool IsProtected
        {
            get
            {
                InitData();
                return (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
            }
        }

        private void InitData()
        {
            if (_attributes != FieldAttributes.ReservedMask)
                return;

            ReadData();
        }

        private string? ReadData()
        {
            if (!_helpers.ReadProperties(ContainingType, Token, out string? name, out _attributes, ref _type))
                return null;

            StringCaching options = ContainingType.Heap.Runtime.DataTarget?.CacheOptions.CacheFieldNames ?? StringCaching.Cache;
            if (name != null)
            {
                if (options == StringCaching.Intern)
                    name = string.Intern(name);

                if (options != StringCaching.None)
                    _name = name;
            }

            return name;
        }

        public override T Read<T>(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            if (!_helpers.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        public override ClrObject ReadObject(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0 || !_helpers.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return ContainingType.Heap.GetObject(obj);
        }

        public override ClrValueType ReadStruct(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            return new ClrValueType(address, Type, interior: true);
        }

        public override string? ReadString(ulong objRef, bool interior)
        {
            ClrObject obj = ReadObject(objRef, interior);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }

        public override ulong GetAddress(ulong objRef, bool interior = false)
        {
            if (interior)
                return objRef + (ulong)Offset;

            return objRef + (ulong)(Offset + IntPtr.Size);
        }

        internal static int GetSize(ClrType? type, ClrElementType cet)
        {
            // todo:  What if we have a struct which is not fully constructed (null MT,
            //        null type) and need to get the size of the field?
            switch (cet)
            {
                case ClrElementType.Struct:
                    if (type is null)
                        return 1;

                    ClrField? last = null;
                    foreach (ClrField field in type.Fields)
                    {
                        if (last is null)
                            last = field;
                        else if (field.Offset > last.Offset)
                            last = field;
                        else if (field.Offset == last.Offset && field.Size > last.Size)
                            last = field;
                    }

                    if (last is null)
                        return 0;

                    return last.Offset + last.Size;

                case ClrElementType.Int8:
                case ClrElementType.UInt8:
                case ClrElementType.Boolean:
                    return 1;

                case ClrElementType.Float:
                case ClrElementType.Int32:
                case ClrElementType.UInt32:
                    return 4;

                case ClrElementType.Double: // double
                case ClrElementType.Int64:
                case ClrElementType.UInt64:
                    return 8;

                case ClrElementType.String:
                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                case ClrElementType.NativeInt: // native int
                case ClrElementType.NativeUInt: // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                    return IntPtr.Size;

                case ClrElementType.UInt16:
                case ClrElementType.Int16:
                case ClrElementType.Char: // u2
                    return 2;
            }

            throw new Exception("Unexpected element type.");
        }
    }
}