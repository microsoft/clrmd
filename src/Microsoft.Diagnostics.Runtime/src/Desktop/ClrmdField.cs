// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ClrmdField : ClrInstanceField
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
        public override int Size => GetSize(Type, ElementType);

        public override uint Token { get; }
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
            if (ElementType == ClrElementType.Class)
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

            if (!_helpers.ReadProperties(Parent, Token, out _name, out _attributes, out SigParser sigParser))
                return;

            // We may have to try to construct a type from the sigParser if the method table was a bust in the constructor
            if (_type != null)
                return;

            _type = GetTypeForFieldSig(_helpers.Factory, sigParser, Parent?.Heap, Parent?.Module);
        }

        internal static ClrType GetTypeForFieldSig(ITypeFactory factory, SigParser sigParser, ClrHeap heap, ClrModule module)
        {
            ClrType result = null;
            bool res;
            int etype = 0;

            if (res = sigParser.GetCallingConvInfo(out int sigType))
                Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

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

                    ClrType innerType = factory.GetOrCreateTypeFromToken(module, token);
                    if (innerType == null)
                        innerType = factory.GetOrCreateBasicType(type);

                    result = factory.GetOrCreatePointerType(innerType, 1);
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

                    if (token != 0)
                        result = factory.GetOrCreateTypeFromToken(module, token);

                    if (result == null)
                        result = factory.GetOrCreateBasicType((ClrElementType)etype);
                }
            }

            if (result.IsArray && result.ComponentType == null && result is ClrmdType clrmdType)
            {
                etype = 0;

                if (res = sigParser.GetCallingConvInfo(out sigType))
                    Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

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

                if (token != 0)
                    clrmdType.SetComponentType(factory.GetOrCreateTypeFromToken(module, token));

                else
                    clrmdType.SetComponentType(factory.GetOrCreateBasicType((ClrElementType)etype));
            }

            return result;
        }

        public override object GetValue(ulong objRef, bool interior = false, bool convertStrings = true)
        {
            // TODO:  Move to IFieldHelpers?
            if (!HasSimpleValue)
                return null;

            ulong addr = GetAddress(objRef, interior);

            if (ElementType == ClrElementType.String)
            {
                object val = ValueReader.GetValueAtAddress(Parent?.Heap, _helpers.DataReader, ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return convertStrings ? null : (object)(ulong)0;

                addr = (ulong)val;
                if (!convertStrings)
                    return addr;
            }

            return ValueReader.GetValueAtAddress(Parent?.Heap, _helpers.DataReader, ElementType, addr);
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
                    if (type == null)
                        return 1;

                    return type.BaseSize;

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