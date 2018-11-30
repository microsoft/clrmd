// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopStaticField : ClrStaticField
    {
        public DesktopStaticField(
            DesktopGCHeap heap,
            IFieldData field,
            BaseDesktopHeapType containingType,
            string name,
            FieldAttributes attributes,
            object defaultValue,
            IntPtr sig,
            int sigLen)
        {
            _field = field;
            Name = name;
            _attributes = attributes;
            _type = (BaseDesktopHeapType)heap.GetTypeByMethodTable(field.TypeMethodTable, 0);
            _defaultValue = defaultValue;
            _heap = heap;
            Token = field.FieldToken;

            if (_type != null && ElementType != ClrElementType.Class)
                _type.ElementType = ElementType;

            _containingType = containingType;

            if (_type == null)
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
                                _type = heap.GetArrayType((ClrElementType)etype, ranks, null);
                        }
                        else if (type == ClrElementType.SZArray)
                        {
                            res = sigParser.PeekElemType(out etype);
                            type = (ClrElementType)etype;

                            if (ClrRuntime.IsObjectReference(type))
                                _type = (BaseDesktopHeapType)heap.GetBasicType(ClrElementType.SZArray);
                            else
                                _type = heap.GetArrayType(type, -1, null);
                        }
                        else if (type == ClrElementType.Pointer)
                        {
                            // Only deal with single pointers for now and types that have already been constructed
                            res = sigParser.GetElemType(out etype);
                            type = (ClrElementType)etype;

                            sigParser.GetToken(out var token);
                            var innerType = (BaseDesktopHeapType)heap.GetGCHeapTypeFromModuleAndToken(field.Module, Convert.ToUInt32(token));

                            if (innerType == null)
                            {
                                innerType = (BaseDesktopHeapType)heap.GetBasicType(type);
                            }

                            _type = heap.CreatePointerType(innerType, type, null);
                        }
                    }
                }
            }

            if (_type == null)
            {
                _typeResolver = new Lazy<ClrType>(
                    () =>
                        {
                            ClrType type = (BaseDesktopHeapType)TryBuildType(_heap);

                            if (type == null)
                                type = (BaseDesktopHeapType)heap.GetBasicType(ElementType);

                            return type;
                        });
            }
        }

        public override uint Token { get; }
        public override bool HasDefaultValue => _defaultValue != null;

        public override object GetDefaultValue()
        {
            return _defaultValue;
        }

        public override bool IsPublic => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
        public override bool IsPrivate => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
        public override bool IsInternal => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
        public override bool IsProtected => (_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
        public override ClrElementType ElementType => (ClrElementType)_field.CorElementType;
        public override string Name { get; }

        public override ClrType Type
        {
            get
            {
                if (_type == null)
                    return _typeResolver.Value;

                return _type;
            }
        }

        private ClrType TryBuildType(ClrHeap heap)
        {
            var runtime = heap.Runtime;
            var domains = runtime.AppDomains;
            var types = new ClrType[domains.Count];

            var elType = ElementType;
            if (ClrRuntime.IsPrimitive(elType) || elType == ClrElementType.String)
                return ((DesktopGCHeap)heap).GetBasicType(elType);

            var count = 0;
            foreach (var domain in domains)
            {
                var value = GetValue(domain);
                if (value != null && value is ulong && (ulong)value != 0)
                {
                    types[count++] = heap.GetObjectType((ulong)value);
                }
            }

            var depth = int.MaxValue;
            ClrType result = null;
            for (var i = 0; i < count; ++i)
            {
                var curr = types[i];
                if (curr == result || curr == null)
                    continue;

                var nextDepth = GetDepth(curr);
                if (nextDepth < depth)
                {
                    result = curr;
                    depth = nextDepth;
                }
            }

            return result;
        }

        private int GetDepth(ClrType curr)
        {
            var depth = 0;
            while (curr != null)
            {
                curr = curr.BaseType;
                depth++;
            }

            return depth;
        }

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1).
        /// </summary>
        public override int Offset => (int)_field.Offset;

        /// <summary>
        /// Given an object reference, fetch the address of the field.
        /// </summary>

        public override bool HasSimpleValue => _containingType != null;
        public override int Size
        {
            get
            {
                if (_type == null)
                    _type = (BaseDesktopHeapType)TryBuildType(_heap);
                return DesktopInstanceField.GetSize(_type, ElementType);
            }
        }

        public override object GetValue(ClrAppDomain appDomain, bool convertStrings = true)
        {
            if (!HasSimpleValue)
                return null;

            var addr = GetAddress(appDomain);

            if (ElementType == ClrElementType.String)
            {
                var val = _containingType.DesktopHeap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return convertStrings ? null : (object)(ulong)0;

                addr = (ulong)val;
                if (!convertStrings)
                    return addr;
            }

            // Structs are stored as objects.
            var elementType = ElementType;
            if (elementType == ClrElementType.Struct)
                elementType = ClrElementType.Object;

            if (elementType == ClrElementType.Object && addr == 0)
                return (ulong)0;

            return _containingType.DesktopHeap.GetValueAtAddress(elementType, addr);
        }

        public override ulong GetAddress(ClrAppDomain appDomain)
        {
            if (_containingType == null)
                return 0;

            var shared = _containingType.Shared;

            IDomainLocalModuleData data = null;
            if (shared)
            {
                var id = _containingType.DesktopModule.ModuleId;
                data = _containingType.DesktopHeap.DesktopRuntime.GetDomainLocalModule(appDomain.Address, id);
                if (!IsInitialized(data))
                    return 0;
            }
            else
            {
                var modAddr = _containingType.GetModuleAddress(appDomain);
                if (modAddr != 0)
                    data = _containingType.DesktopHeap.DesktopRuntime.GetDomainLocalModule(modAddr);
            }

            if (data == null)
                return 0;

            ulong addr;
            if (ClrRuntime.IsPrimitive(ElementType))
                addr = data.NonGCStaticDataStart + _field.Offset;
            else
                addr = data.GCStaticDataStart + _field.Offset;

            return addr;
        }

        public override bool IsInitialized(ClrAppDomain appDomain)
        {
            if (_containingType == null)
                return false;

            if (!_containingType.Shared)
                return true;

            var id = _containingType.DesktopModule.ModuleId;
            var data = _containingType.DesktopHeap.DesktopRuntime.GetDomainLocalModule(appDomain.Address, id);
            if (data == null)
                return false;

            return IsInitialized(data);
        }

        private bool IsInitialized(IDomainLocalModuleData data)
        {
            if (data == null || _containingType == null)
                return false;

            var flagsAddr = data.ClassData + (_containingType.MetadataToken & ~0x02000000u) - 1;
            if (!_heap.DesktopRuntime.ReadByte(flagsAddr, out byte flags))
                return false;

            return (flags & 1) != 0;
        }

        private readonly IFieldData _field;
        private BaseDesktopHeapType _type;
        private readonly BaseDesktopHeapType _containingType;
        private readonly FieldAttributes _attributes;
        private readonly object _defaultValue;
        private readonly DesktopGCHeap _heap;
        private readonly Lazy<ClrType> _typeResolver;
    }
}