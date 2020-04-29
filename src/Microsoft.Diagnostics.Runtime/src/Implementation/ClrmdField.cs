// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdField : ClrInstanceField
    {
        private readonly IFieldHelpers _helpers;
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

        public override int Size => GetSize(Type, ElementType);

        public override int Token { get; }
        public override int Offset { get; }

        public override ClrType Parent { get; }

        public ClrmdField(ClrType parent, IFieldData data)
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
            if (!_helpers.ReadProperties(Parent, Token, out string? name, out _attributes, out SigParser sigParser))
                return null;

            StringCaching options = Parent.Heap.Runtime.DataTarget?.CacheOptions.CacheFieldNames ?? StringCaching.Cache;
            if (name != null)
            {
                if (options == StringCaching.Intern)
                    name = string.Intern(name);

                if (options != StringCaching.None)
                    _name = name;
            }

            // We may have to try to construct a type from the sigParser if the method table was a bust in the constructor
            if (_type != null)
                return name;

            _type = GetTypeForFieldSig(_helpers.Factory, sigParser, Parent.Heap, Parent.Module);
            return name;
        }

        internal static ClrType? GetTypeForFieldSig(ITypeFactory factory, SigParser sigParser, ClrHeap heap, ClrModule? module)
        {
            ClrType? result = null;
            bool res;
            int etype = 0;

            if (res = sigParser.GetCallingConvInfo(out int sigType))
                DebugOnly.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

            res = res && sigParser.SkipCustomModifiers();
            res = res && sigParser.GetElemType(out etype);

            // Generic instantiation
            if (etype == 0x15)
                res = res && sigParser.GetElemType(out etype);

            if (res)
            {
                ClrElementType type = (ClrElementType)etype;

                if (type == ClrElementType.Array)
                {
                    res = sigParser.PeekElemType(out etype);
                    res = res && sigParser.SkipExactlyOne();

                    int ranks = 0;
                    res = res && sigParser.GetData(out ranks);

                    if (res)
                    {
                        ClrType inner = factory.GetOrCreateBasicType((ClrElementType)etype);
                        result = factory.GetOrCreateArrayType(inner, ranks);
                    }
                }
                else if (type == ClrElementType.SZArray)
                {
                    sigParser.PeekElemType(out etype);
                    type = (ClrElementType)etype;

                    if (type.IsObjectReference())
                    {
                        result = factory.GetOrCreateBasicType(ClrElementType.SZArray);
                    }
                    else
                    {
                        ClrType inner = factory.GetOrCreateBasicType((ClrElementType)etype);
                        result = factory.GetOrCreateArrayType(inner, 1);
                    }
                }
                else if (type == ClrElementType.Pointer)
                {
                    // Only deal with single pointers for now and types that have already been constructed
                    sigParser.GetElemType(out etype);
                    type = (ClrElementType)etype;

                    sigParser.GetToken(out int token);

                    if (module != null)
                    {
                        ClrType? innerType;
                        if (type.IsPrimitive())
                        {
                            innerType = factory.GetOrCreateBasicType(type);
                        }
                        else
                        {
                            innerType = factory.GetOrCreateTypeFromToken(module, token);

                            // Fallback just in case 
                            if (innerType is null)
                                innerType = factory.GetOrCreateBasicType(type);
                        }
                        

                        result = factory.GetOrCreatePointerType(innerType, 1);
                    }
                }
                else if (type == ClrElementType.Object || type == ClrElementType.Class)
                {
                    result = heap.ObjectType;
                }
                else
                {
                    // struct, then try to get the token
                    int token = 0;
                    if (etype == 0x11 || etype == 0x12)
                        sigParser.GetToken(out token);

                    if (token != 0 && module != null)
                        result = factory.GetOrCreateTypeFromToken(module, token);

                    if (result is null)
                        result = factory.GetOrCreateBasicType((ClrElementType)etype);
                }
            }

            if (result is null)
                return result;

            if (result.IsArray && result.ComponentType is null && result is ClrmdArrayType clrmdType)
            {
                etype = 0;

                if (res = sigParser.GetCallingConvInfo(out sigType))
                    DebugOnly.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

                res = res && sigParser.SkipCustomModifiers();
                res = res && sigParser.GetElemType(out etype);

                _ = res && sigParser.GetElemType(out etype);

                // Generic instantiation
                if (etype == 0x15)
                    sigParser.GetElemType(out etype);

                // If it's a class or struct, then try to get the token
                int token = 0;
                if (etype == 0x11 || etype == 0x12)
                    sigParser.GetToken(out token);

                if (token != 0 && module != null)
                    clrmdType.SetComponentType(factory.GetOrCreateTypeFromToken(module, token));
                else
                    clrmdType.SetComponentType(factory.GetOrCreateBasicType((ClrElementType)etype));
            }

            return result;
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

            ulong mt = _helpers.DataReader.ReadPointer(obj);
            ClrType? type = _helpers.Factory.GetOrCreateType(mt, obj);
            if (type is null)
                return default;

            return new ClrObject(obj, type);
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

        internal static int GetSize(ClrType type, ClrElementType cet)
        {
            // todo:  What if we have a struct which is not fully constructed (null MT,
            //        null type) and need to get the size of the field?
            switch (cet)
            {
                case ClrElementType.Struct:
                    if (type is null)
                        return 1;

                    ClrField? last = null;
                    ImmutableArray<ClrInstanceField> fields = type.Fields;
                    foreach (ClrField field in fields)
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