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
    /// A representation of a field in the target process.
    /// </summary>
    public abstract class ClrField : IClrField
    {
        private string? _name;
        internal readonly IAbstractTypeHelpers _helpers;
        private ClrType? _type;
        private int _attributes = (int)FieldAttributes.ReservedMask;

        internal FieldInfo FieldInfo { get; }

        internal ClrField(ClrType containingType, ClrType? type, IAbstractTypeHelpers helpers, FieldInfo data)
        {
            _helpers = helpers;
            ContainingType = containingType;
            _type = type;

            if (data.ElementType == ClrElementType.Class && _type != null)
                data.ElementType = _type.ElementType;

            FieldInfo = data;
            DebugOnlyLoadLazyValues();
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            InitData(false);
        }


        /// <summary>
        /// Gets the <see cref="ClrType"/> containing this field.
        /// </summary>
        public ClrType ContainingType { get; }

        IClrType IClrField.ContainingType => ContainingType;

        /// <summary>
        /// Gets the name of the field.
        /// </summary>

        public string? Name
        {
            get
            {
                if (_name is not null)
                    return _name;

                StringCaching options = ContainingType.Heap.Runtime.DataTarget.CacheOptions.CacheFieldNames;
                if (options == StringCaching.None)
                {
                    IAbstractMetadataReader? import = ContainingType.Module.MetadataReader;
                    if (import is null || !import.GetFieldDefInfo(Token, out FieldDefInfo info))
                        return null;

                    return info.Name;
                }

                InitData(true);
                return _name;
            }
        }

        /// <summary>
        /// Gets the type token of this field.
        /// </summary>
        public int Token => FieldInfo.Token;

        /// <summary>
        /// Gets the type of the field.  Note this property may return <see langword="null"/> on error.  There is a bug in several versions
        /// of our debugging layer which causes this.  You should always null-check the return value of this field.
        /// </summary>
        public virtual ClrType? Type
        {
            get
            {
                if (_type != null)
                    return _type;

                InitData(false);
                return _type;
            }
        }

        private void InitData(bool forName)
        {
            if (!forName && _attributes != (int)FieldAttributes.ReservedMask)
                return;

            IAbstractMetadataReader? import = ContainingType.Module.MetadataReader;
            if (import is null || !import.GetFieldDefInfo(Token, out FieldDefInfo info))
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

        IClrType? IClrField.Type => Type;

        /// <summary>
        /// Gets the element type of this field.  Note that even when Type is <see langword="null"/>, this should still tell you
        /// the element type of the field.
        /// </summary>
        public ClrElementType ElementType => FieldInfo.ElementType;

        /// <summary>
        /// Gets a value indicating whether this field is a primitive (<see cref="int"/>, <see cref="float"/>, etc).
        /// </summary>
        /// <returns>True if this field is a primitive (<see cref="int"/>, <see cref="float"/>, etc), false otherwise.</returns>
        public bool IsPrimitive => ElementType.IsPrimitive();

        /// <summary>
        /// Gets a value indicating whether this field is a value type.
        /// </summary>
        /// <returns>True if this field is a value type, false otherwise.</returns>
        public bool IsValueType => ElementType.IsValueType();

        /// <summary>
        /// Gets a value indicating whether this field is an object reference.
        /// </summary>
        /// <returns>True if this field is an object reference, false otherwise.</returns>
        public bool IsObjectReference => ElementType.IsObjectReference();

        /// <summary>
        /// Gets the size of this field.
        /// </summary>
        public int Size => GetSize(Type, ElementType);

        /// <summary>
        /// Attributes of this field;
        /// </summary>
        public FieldAttributes Attributes
        {
            get
            {
                InitData(false);
                return (FieldAttributes)_attributes;
            }
        }

        /// <summary>
        /// For instance fields, this is the offset of the field within the object.
        /// For static fields this is the offset within the block of memory allocated for the module's static fields.
        /// </summary>
        public int Offset => FieldInfo.Offset;

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string? ToString()
        {
            ClrType? type = Type;
            if (type is null)
                return Name;

            return $"{type.Name} {Name}";
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