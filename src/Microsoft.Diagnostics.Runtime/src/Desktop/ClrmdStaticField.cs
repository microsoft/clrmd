// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ClrmdStaticField : ClrStaticField
    {
        private readonly IFieldHelpers _helpers;
        private string _name;
        private ClrType _type;
        private FieldAttributes _attributes = FieldAttributes.ReservedMask;

        public override ClrElementType ElementType { get; }

        public override bool IsObjectReference => ElementType.IsObjectReference();
        public override bool IsValueClass => ElementType.IsValueClass();
        public override bool IsPrimitive => ElementType.IsPrimitive();

        public override string Name
        {
            get
            {
                if (_name != null)
                    return _name;

                InitData();
                return _name;
            }
        }

        public override ClrType Type
        {
            get
            {
                if (_type != null)
                    return _type;

                InitData();
                return _type;
            }
        }

        /// <summary>
        /// Given an object reference, fetch the address of the field.
        /// </summary>

        public override bool HasSimpleValue => !ElementType.IsValueClass();
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
            _type = _helpers.Factory.GetOrCreateType(parent.Heap, data.TypeMethodTable, 0);
        }


        private void InitData()
        {
            if (_attributes != FieldAttributes.ReservedMask)
                return;

            if (!_helpers.ReadProperties(Parent, out _name, out _attributes, out SigParser sigParser))
                return;

            // We may have to try to construct a type from the sigParser if the method table was a bust in the constructor
            if (_type != null)
                return;

            _type = ClrmdField.GetTypeForFieldSig(_helpers.Factory, sigParser, Parent.Heap, Parent.Module);
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


        public override object GetValue(ClrAppDomain appDomain, bool convertStrings = true)
        {
            // TODO:  Move to IFieldHelpers?
            if (!HasSimpleValue)
                return null;

            ulong addr = GetAddress(appDomain);

            if (ElementType == ClrElementType.String)
            {
                return ValueReader.GetValueAtAddress(Parent.Heap, _helpers.DataReader, ClrElementType.String, addr);
            }

            // Structs are stored as objects.
            ClrElementType elementType = ElementType;
            if (elementType == ClrElementType.Struct)
                elementType = ClrElementType.Object;

            if (elementType == ClrElementType.Object && addr == 0)
                return (ulong)0;

            return ValueReader.GetValueAtAddress(Parent.Heap, _helpers.DataReader, ElementType, addr);
        }

        public override ulong GetAddress(ClrAppDomain appDomain) => _helpers.GetStaticFieldAddress(this, appDomain);

        public override bool IsInitialized(ClrAppDomain appDomain) => GetAddress(appDomain) != 0;
    }
}