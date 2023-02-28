// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrmdStaticField : ClrStaticField
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

        public override int Token { get; }
        public override int Offset { get; }

        public override ClrType ContainingType { get; }

        public ClrmdStaticField(ClrType containingType, ClrType? type, IClrFieldHelpers helpers, in FieldData data)
        {
            if (containingType is null)
                throw new ArgumentNullException(nameof(containingType));

            ContainingType = containingType;
            _type = type;
            _helpers = helpers;
            Token = (int)data.FieldToken;
            ElementType = (ClrElementType)data.ElementType;
            Offset = (int)data.Offset;
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
                    _name = string.Intern(name);
                else if (options == StringCaching.Cache)
                    _name = name;
            }

            // We may have to try to construct a type from the sigParser if the method table was a bust in the constructor
            if (_type == null)
            {
            }

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

        public override ulong GetAddress(ClrAppDomain domain)
        {
            ulong address = _helpers.GetStaticFieldAddress(this, domain);
            return address;
        }

        public override bool IsInitialized(ClrAppDomain appDomain) => GetAddress(appDomain) != 0;

        public override T Read<T>(ClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default;

            if (!_helpers.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        public override ClrObject ReadObject(ClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0 || !_helpers.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return ContainingType.Heap.GetObject(obj);
        }

        public override ClrValueType ReadStruct(ClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default;

            return new ClrValueType(address, Type, interior: true);
        }

        public override string? ReadString(ClrAppDomain appDomain)
        {
            ClrObject obj = ReadObject(appDomain);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }
    }
}