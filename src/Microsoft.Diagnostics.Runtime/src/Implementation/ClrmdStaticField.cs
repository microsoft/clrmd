// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public class ClrmdStaticField : ClrStaticField
    {
        private ulong _address = ulong.MaxValue - 1;
        private readonly IFieldHelpers _helpers;
        private string? _name;
        private ClrType? _type;
        private FieldAttributes _attributes = FieldAttributes.ReservedMask;

        public override ClrElementType ElementType { get; }

        public override bool IsObjectReference => ElementType.IsObjectReference();
        public override bool IsValueClass => ElementType.IsValueClass();
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

        public override ClrType Type
        {
            get
            {
                if (_type != null)
                    return _type;

                InitData();
                return _type!;
            }
        }

        public override int Size => ClrmdField.GetSize(Type, ElementType);

        public override uint Token { get; }
        public override int Offset { get; }

        public override ClrType Parent { get; }

        public ClrmdStaticField(ClrType parent, IFieldData data)
        {
            if (parent is null)
                throw new ArgumentNullException(nameof(parent));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            Parent = parent;
            Token = data.Token;
            ElementType = data.ElementType;
            Offset = data.Offset;

            _helpers = data.Helpers;

            // Must be the last use of 'data' in this constructor. 
            _type = _helpers.Factory.GetOrCreateType(data.TypeMethodTable, 0);
        }

        private void InitData()
        {
            if (_attributes != FieldAttributes.ReservedMask)
                return;

            ReadData();
        }

        private string? ReadData()
        {
            if (!_helpers.ReadProperties(Parent, Token, out string? name, out _attributes, out SigParser sigParser))
                return null;

            StringCaching options = Parent.Heap.Runtime.DataTarget?.CacheOptions.CacheFieldNames ?? StringCaching.Cache;
            if (name != null)
            {
                if (options == StringCaching.Intern)
                    _name = string.Intern(name);
                else if (options == StringCaching.Cache)
                    _name = name;
            }

            // We may have to try to construct a type from the sigParser if the method table was a bust in the constructor
            if (_type != null)
                return name;

            _type = ClrmdField.GetTypeForFieldSig(_helpers.Factory, sigParser, Parent.Heap, Parent.Module);
            return name;
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

        public override ulong Address => _address != ulong.MaxValue - 1 ? _address : _address = _helpers.GetStaticFieldAddress(this, Type?.Module?.AppDomain);

        public override bool IsInitialized(ClrAppDomain appDomain) => Address != 0;

        public override T Read<T>()
        {
            ulong address = Address;
            if (address == 0)
                return default;

            if (!_helpers.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        public override ClrObject ReadObject()
        {
            if (Address == 0 || !_helpers.DataReader.ReadPointer(Address, out ulong obj) || obj == 0)
                return default;

            ulong mt = _helpers.DataReader.ReadPointerUnsafe(obj);
            ClrType? type = _helpers.Factory.GetOrCreateType(mt, obj);
            if (type is null)
                return default;

            return new ClrObject(obj, type);
        }

        public override ClrValueClass ReadStruct()
        {
            if (Address == 0)
                return default;

            return new ClrValueClass(Address, Type, interior: true);
        }

        public override string? ReadString()
        {
            ClrObject obj = ReadObject();
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }
    }
}