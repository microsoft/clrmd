// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopInstanceField : ClrInstanceField
    {
        public override uint Token { get; }
        public override bool IsPublic => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
        public override bool IsPrivate => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
        public override bool IsInternal => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
        public override bool IsProtected => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;

        public DesktopInstanceField(DesktopGCHeap heap, IFieldData data, string name, FieldAttributes attributes, IntPtr sig, int sigLen)
        {
            Name = name;
            _field = data;
            _attributes = attributes;
            Token = data.FieldToken;

            _heap = heap;
            _type = new Lazy<BaseDesktopHeapType>(() => GetType(_heap, data, sig, sigLen, (ClrElementType)_field.CorElementType));
        }

        private static BaseDesktopHeapType GetType(DesktopGCHeap heap, IFieldData data, IntPtr sig, int sigLen, ClrElementType elementType)
        {
            BaseDesktopHeapType result = null;
            var mt = data.TypeMethodTable;
            if (mt != 0)
                result = (BaseDesktopHeapType)heap.GetTypeByMethodTable(mt, 0);

            if (result == null)
            {
                if (sig != IntPtr.Zero && sigLen > 0)
                {
                    var sigParser = new SigParser(sig, sigLen);

                    bool res;
                    var etype = 0;

                    if (res = sigParser.GetCallingConvInfo(out var sigType))
                        Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

                    res = res && sigParser.SkipCustomModifiers();
                    res = res && sigParser.GetElemType(out etype);

                    // Generic instantiation
                    if (etype == 0x15)
                        res = res && sigParser.GetElemType(out etype);

                    if (res)
                    {
                        var type = (ClrElementType)etype;

                        if (type == ClrElementType.Array)
                        {
                            res = sigParser.PeekElemType(out etype);
                            res = res && sigParser.SkipExactlyOne();

                            var ranks = 0;
                            res = res && sigParser.GetData(out ranks);

                            if (res)
                                result = heap.GetArrayType((ClrElementType)etype, ranks, null);
                        }
                        else if (type == ClrElementType.SZArray)
                        {
                            res = sigParser.PeekElemType(out etype);
                            type = (ClrElementType)etype;

                            if (ClrRuntime.IsObjectReference(type))
                                result = (BaseDesktopHeapType)heap.GetBasicType(ClrElementType.SZArray);
                            else
                                result = heap.GetArrayType(type, -1, null);
                        }
                        else if (type == ClrElementType.Pointer)
                        {
                            // Only deal with single pointers for now and types that have already been constructed
                            res = sigParser.GetElemType(out etype);
                            type = (ClrElementType)etype;

                            sigParser.GetToken(out var token);
                            var innerType = (BaseDesktopHeapType)heap.GetGCHeapTypeFromModuleAndToken(data.Module, Convert.ToUInt32(token));

                            if (innerType == null)
                            {
                                innerType = (BaseDesktopHeapType)heap.GetBasicType(type);
                            }

                            result = heap.CreatePointerType(innerType, type, null);
                        }
                        else if (type == ClrElementType.Object || type == ClrElementType.Class)
                        {
                            result = (BaseDesktopHeapType)heap.ObjectType;
                        }
                        else
                        {
                            // struct, then try to get the token
                            var token = 0;
                            if (etype == 0x11 || etype == 0x12)
                                res = res && sigParser.GetToken(out token);

                            if (token != 0)
                                result = (BaseDesktopHeapType)heap.GetGCHeapTypeFromModuleAndToken(data.Module, (uint)token);

                            if (result == null)
                                if ((result = (BaseDesktopHeapType)heap.GetBasicType((ClrElementType)etype)) == null)
                                    result = heap.ErrorType;
                        }
                    }
                }

                if (result == null)
                    result = (BaseDesktopHeapType)heap.GetBasicType(elementType);
            }
            else if (elementType != ClrElementType.Class)
            {
                result.ElementType = elementType;
            }

            if (result.IsArray && result.ComponentType == null)
            {
                if (sig != IntPtr.Zero && sigLen > 0)
                {
                    var sigParser = new SigParser(sig, sigLen);

                    bool res;
                    var etype = 0;

                    if (res = sigParser.GetCallingConvInfo(out var sigType))
                        Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

                    res = res && sigParser.SkipCustomModifiers();
                    res = res && sigParser.GetElemType(out etype);

                    res = res && sigParser.GetElemType(out etype);

                    // Generic instantiation
                    if (etype == 0x15)
                        res = res && sigParser.GetElemType(out etype);

                    // If it's a class or struct, then try to get the token
                    var token = 0;
                    if (etype == 0x11 || etype == 0x12)
                        res = res && sigParser.GetToken(out token);

                    if (token != 0)
                        result.ComponentType = heap.GetGCHeapTypeFromModuleAndToken(data.Module, (uint)token);

                    else if (result.ComponentType == null)
                        if ((result.ComponentType = heap.GetBasicType((ClrElementType)etype)) == null)
                            result.ComponentType = heap.ErrorType;
                }
            }

            return result;
        }

        public override bool IsObjectReference => ClrRuntime.IsObjectReference((ClrElementType)_field.CorElementType);
        public override bool IsValueClass => ClrRuntime.IsValueClass((ClrElementType)_field.CorElementType);
        public override bool IsPrimitive => ClrRuntime.IsPrimitive((ClrElementType)_field.CorElementType);

        public override ClrElementType ElementType
        {
            get
            {
                if (_elementType != ClrElementType.Unknown)
                    return _elementType;

                ClrType type = _type.Value;
                if (type == null)
                    _elementType = (ClrElementType)_field.CorElementType;

                else if (type.IsEnum)
                    _elementType = type.GetEnumElementType();

                else
                    _elementType = type.ElementType;

                return _elementType;
            }
        }

        public override string Name { get; }

        public override ClrType Type => _type.Value;

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1).
        /// </summary>
        public override int Offset => (int)_field.Offset;

        /// <summary>
        /// Given an object reference, fetch the address of the field.
        /// </summary>

        public override bool HasSimpleValue => _type != null && !ClrRuntime.IsValueClass(ElementType);
        public override int Size => GetSize(_type.Value, ElementType);

        private readonly DesktopGCHeap _heap;
        private readonly Lazy<BaseDesktopHeapType> _type;
        private readonly IFieldData _field;
        private readonly FieldAttributes _attributes;
        private ClrElementType _elementType = ClrElementType.Unknown;

        public override object GetValue(ulong objRef, bool interior = false, bool convertStrings = true)
        {
            if (!HasSimpleValue)
                return null;

            var addr = GetAddress(objRef, interior);

            if (ElementType == ClrElementType.String)
            {
                var val = _heap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return convertStrings ? null : (object)(ulong)0;

                addr = (ulong)val;
                if (!convertStrings)
                    return addr;
            }

            return _heap.GetValueAtAddress(ElementType, addr);
        }

        public override ulong GetAddress(ulong objRef, bool interior = false)
        {
            if (interior)
                return objRef + (ulong)Offset;

            // TODO:  Type really shouldn't be null here, but due to the dac it can be.  We still need
            //        to respect m_heap.PointerSize, so there needs to be a way to track this when m_type is null.
            if (_type == null)
                return objRef + (ulong)(Offset + IntPtr.Size);

            return objRef + (ulong)(Offset + _heap.PointerSize);
        }

        internal static int GetSize(BaseDesktopHeapType type, ClrElementType cet)
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
                    if (type == null)
                        return IntPtr.Size; // todo: fixme

                    return type.DesktopHeap.PointerSize;

                case ClrElementType.UInt16:
                case ClrElementType.Int16:
                case ClrElementType.Char: // u2
                    return 2;
            }

            throw new Exception("Unexpected element type.");
        }
    }
}