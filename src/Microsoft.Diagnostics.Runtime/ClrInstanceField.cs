// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Microsoft.Diagnostics.Runtime.Utilities;
using FieldInfo = Microsoft.Diagnostics.Runtime.AbstractDac.FieldInfo;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it represents a name and a type
    /// </summary>
    public sealed class ClrInstanceField : ClrField, IClrInstanceField
    {
        private int _attributes = (int)FieldAttributes.ReservedMask;

        private readonly IClrTypeHelpers _helpers;
        private readonly FieldInfo _data;
        private string? _name;
        private ClrType? _type;

        internal ClrInstanceField(ClrType containingType, ClrType? type, IClrTypeHelpers helpers, in FieldInfo data)
        {
            _helpers = helpers;
            _data = data;
            ContainingType = containingType;

            _type = type;
            if (_data.ElementType == ClrElementType.Class && _type != null)
                _data.ElementType = _type.ElementType;

            DebugOnlyLoadLazyValues();
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            InitData();
        }

        private void InitData()
        {
            if (_attributes != (int)FieldAttributes.ReservedMask)
                return;

            MetadataImport? import = ContainingType.Module.MetadataImport;
            if (import is null || !_helpers.GetFieldMetadataInfo(import, Token, out FieldMetadataInfo info))
            {
                _attributes = 0;
                return;
            }

            StringCaching options = ContainingType.Heap.Runtime.DataTarget.CacheOptions.CacheFieldNames;
            if (info.Name != null)
            {
                if (options == StringCaching.Intern)
                    info.Name = string.Intern(info.Name);

                if (options != StringCaching.None)
                    _name = info.Name;
            }

            if (_type is null)
            {
                SigParser sigParser = new(info.Signature, info.SignatureSize);
                if (sigParser.GetCallingConvInfo(out int sigType) && sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD)
                {
                    sigParser.SkipCustomModifiers();
                    _type = ContainingType.Heap.GetOrCreateTypeFromSignature(ContainingType.Module, sigParser, ContainingType.EnumerateGenericParameters(), Array.Empty<ClrGenericParameter>());
                }
            }

            Interlocked.Exchange(ref _attributes, (int)info.Attributes);
        }

        public override FieldAttributes Attributes
        {
            get
            {
                InitData();
                return (FieldAttributes)_attributes;
            }
        }

        public override ClrElementType ElementType => _data.ElementType;
        public override bool IsObjectReference => ElementType.IsObjectReference();
        public override bool IsValueType => ElementType.IsValueType();
        public override bool IsPrimitive => ElementType.IsPrimitive();

        public override string? Name
        {
            get
            {
                if (_name != null)
                    return _name;

                InitData();
                return _name;
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

        public override int Token => _data.Token;

        public override int Offset => _data.Offset;

        public override ClrType ContainingType { get; }

        /// <summary>
        /// Reads the value of the field as an unmanaged struct or primitive type.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type.</typeparam>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public T Read<T>(ulong objRef, bool interior) where T : unmanaged
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            if (!ContainingType.Module.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        /// <summary>
        /// Reads the value of an object field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public ClrObject ReadObject(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0 || !ContainingType.Module.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return ContainingType.Heap.GetObject(obj);
        }

        IClrValue IClrInstanceField.ReadObject(ulong objRef, bool interior) => ReadObject(objRef, interior);

        /// <summary>
        /// Reads a ValueType struct from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public ClrValueType ReadStruct(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            return new ClrValueType(address, Type, interior: true);
        }

        IClrValue IClrInstanceField.ReadStruct(ulong objRef, bool interior) => ReadStruct(objRef, interior);

        /// <summary>
        /// Reads a string from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public string? ReadString(ulong objRef, bool interior)
        {
            ClrObject obj = ReadObject(objRef, interior);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <returns>The value of the field.</returns>
        public ulong GetAddress(ulong objRef)
        {
            return GetAddress(objRef, false);
        }

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <param name="interior">
        /// Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.
        /// </param>
        /// <returns>The value of the field.</returns>
        public ulong GetAddress(ulong objRef, bool interior)
        {
            if (interior)
                return objRef + (ulong)Offset;

            return objRef + (ulong)(Offset + IntPtr.Size);
        }
    }
}