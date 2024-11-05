// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrTypeFactory
    {
        private const int mdtTypeDef = 0x02000000;
        private const int mdtTypeRef = 0x01000000;

        private readonly ClrHeap _heap;
        private readonly IAbstractTypeHelpers _typeHelpers;
        private readonly CacheOptions _options;
        private readonly GCState _gcInfo;
        private volatile ClrType?[]? _basicTypes;
        private readonly Dictionary<ulong, ClrType> _types = new();
        private readonly ClrType _objectType;
        private Dictionary<ulong, ClrModule>? _modules;
        private ClrModule? _errorModule;
        private ClrType? _errorType;

        public ClrTypeFactory(ClrHeap heap, IAbstractTypeHelpers typeHelpers, in GCState gcInfo)
        {
            _heap = heap;
            _typeHelpers = typeHelpers;
            _options = heap.Runtime.DataTarget.CacheOptions;
            _gcInfo = gcInfo;

            if (_gcInfo.ObjectMethodTable == 0)
                throw new InvalidDataException("Debugging layer reported ObjectMethodTable of 0.");

            _objectType = CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _gcInfo.ObjectMethodTable, "System.Object") ?? throw new InvalidDataException("Could not create Object type.");
            _types = new() { { _gcInfo.ObjectMethodTable, _objectType } };
        }

        public ClrType CreateFreeType() => CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _gcInfo.FreeMethodTable, "Free") ?? throw new InvalidDataException("Could not create Free type.");

        public ClrType CreateStringType()
        {
            ClrType? stringType = CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _gcInfo.StringMethodTable, "System.String");
            if (stringType is null)
            {
                if (!_typeHelpers.GetTypeInfo(_gcInfo.StringMethodTable, out TypeInfo stringInfo))
                {
                    stringInfo = new()
                    {
                        ParentMethodTable = _gcInfo.ObjectMethodTable,
                        MethodTable = _gcInfo.StringMethodTable,
                        IsShared = true,
                        StaticSize = IntPtr.Size + sizeof(int),
                        ComponentSize = sizeof(char),
                    };
                }

                stringType = new ClrStringType(_heap.Runtime.BaseClassLibrary, _typeHelpers, _heap, stringInfo);
            }

            return stringType;
        }

        public string? GetTypeName(ulong module, ulong mt, int token) => DacNameParser.Parse(_typeHelpers.GetTypeName(module, mt, token));

        public ClrType ObjectType => _objectType;

        public ClrType CreateExceptionType() => CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _gcInfo.ExceptionMethodTable, "System.Exception") ?? _objectType;
        public ClrType ErrorType
        {
            get
            {
                if (_errorType is null)
                {
                    ClrDacType e = new ClrDacType(_typeHelpers, _heap, null, null, _heap.Runtime.BaseClassLibrary, default);
                    Interlocked.CompareExchange(ref _errorType, e, null);
                }

                return _errorType;
            }
        }

        public ClrType? CreateSystemType(ClrHeap heap, ClrModule bcl, ulong mt, string typeName)
        {
            _typeHelpers.GetTypeInfo(mt, out TypeInfo mtd);

            ClrType? baseType = null;
            if (mtd.ParentMethodTable != 0)
            {
                lock (_types)
                    if (!_types.TryGetValue(mtd.ParentMethodTable, out baseType))
                        throw new InvalidOperationException($"Base type for '{typeName}' was not pre-created from MethodTable {mtd.ParentMethodTable:x}.");
            }

            ClrDacType result = new(_typeHelpers, heap, baseType, null, bcl, mtd, typeName);

            // Regardless of caching options, we always cache important system types and basic types
            lock (_types)
                _types[mt] = result;

            return result;
        }

        public ClrType? GetOrCreateType(ulong mt, ulong obj)
        {
            if (mt == 0)
                return null;

            // Remove marking bit.
            mt &= ~1ul;

            ClrType? existing = TryGetType(mt);
            if (existing != null)
            {
                if (obj != 0 && !existing.IsString && existing.ComponentSize != 0 && existing.ComponentType is null && existing is ClrDacType type)
                    type.SetComponentType(TryGetComponentType(obj));

                return existing;
            }

            if (!_typeHelpers.GetTypeInfo(mt, out TypeInfo mtd))
                return null;

            ClrType? baseType = GetOrCreateType(mtd.ParentMethodTable, 0);
            ClrModule module = GetModule(mtd.ModuleAddress);
            ClrType? componentType = null;

            if (obj != 0 && mtd.ComponentSize != 0)
                componentType = TryGetComponentType(obj);

            ClrType result = new ClrDacType(_typeHelpers, _heap, baseType, componentType, module, mtd);
            if (_options.CacheTypes)
            {
                lock (_types)
                    _types[mt] = result;
            }

            return result;
        }

        public ClrType? TryGetType(ulong mt)
        {
            lock (_types)
            {
                _types.TryGetValue(mt, out ClrType? result);
                return result;
            }
        }

        public ClrType? GetOrCreateTypeFromSignature(ClrModule module, SigParser parser, IEnumerable<ClrGenericParameter> typeParameters, IEnumerable<ClrGenericParameter> methodParameters)
        {
            // ECMA 335 - II.23.2.12 - Type

            if (!parser.GetElemType(out ClrElementType etype))
                return null;

            if (etype.IsPrimitive() || etype == ClrElementType.Void || etype == ClrElementType.Object || etype == ClrElementType.String)
                return GetOrCreateBasicType(etype);

            if (etype == ClrElementType.Array)
            {
                ClrType? innerType = GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters);
                innerType ??= GetOrCreateBasicType(ClrElementType.Void);  // Need a placeholder if we can't determine type

                // II.23.2.13
                if (!parser.GetData(out int rank))
                    return null;

                if (!parser.GetData(out int numSizes))
                    return null;

                for (int i = 0; i < numSizes; i++)
                    if (!parser.GetData(out _))
                        return null;

                if (!parser.GetData(out int numLowBounds))
                    return null;

                for (int i = 0; i < numLowBounds; i++)
                    if (!parser.GetData(out _))
                        return null;

                // We should probably use sizes and lower bounds, but this is so rare I won't worry about it for now
                ClrType? result = GetOrCreateArrayType(innerType, rank);
                return result;
            }

            if (etype is ClrElementType.Class or ClrElementType.Struct)
            {
                if (!parser.GetToken(out int token))
                    return null;

                ClrType? result = module != null ? GetOrCreateTypeFromToken(module, token) : null;
                result ??= GetOrCreateBasicType(etype);

                return result;
            }

            if (etype == ClrElementType.FunctionPointer)
            {
                if (!parser.GetToken(out _))
                    return null;

                // We don't have a type for function pointers so we'll make it a void pointer
                ClrType inner = GetOrCreateBasicType(ClrElementType.Void);
                return GetOrCreatePointerType(inner, 1);
            }

            if (etype == ClrElementType.GenericInstantiation)
            {
                if (!parser.GetElemType(out ClrElementType _))
                    return null;

                if (!parser.GetToken(out int token))
                    return null;

                if (!parser.GetData(out int count))
                    return null;

                // Even though we don't make use of these types we need to move past them in the parser.
                for (int i = 0; i < count; i++)
                    GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters);

                ClrType? result = GetOrCreateTypeFromToken(module, token);
                return result;
            }

            if (etype is ClrElementType.MVar or ClrElementType.Var)
            {
                if (!parser.GetData(out int index))
                    return null;

                ClrGenericParameter[] param = (etype == ClrElementType.Var ? typeParameters : methodParameters).ToArray();
                if (index < 0 || index >= param.Length)
                    return null;

                return new ClrGenericType(_typeHelpers, _heap, module, param[index]);
            }

            if (etype == ClrElementType.Pointer || etype == ClrElementType.ByRef)
            {
                if (!parser.SkipCustomModifiers())
                    return null;

                ClrType? innerType = GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters) ?? GetOrCreateBasicType(ClrElementType.Void);
                return GetOrCreatePointerType(innerType, 1);
            }

            if (etype == ClrElementType.SZArray)
            {
                if (!parser.SkipCustomModifiers())
                    return null;

                ClrType? innerType = GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters) ?? GetOrCreateBasicType(ClrElementType.Void);
                return GetOrCreateArrayType(innerType, 1);
            }

            DebugOnly.Assert(false);  // What could we have forgotten?  Should only happen in a corrupted signature.
            return null;
        }

        public ClrType? GetOrCreateTypeFromToken(ClrModule? module, int token)
        {
            if (module is null)
                return null;

            IEnumerable<(ulong MethodTable, int Token)> tokenMap;
            if ((token & mdtTypeDef) != 0)
                tokenMap = module.EnumerateTypeDefToMethodTableMap();
            else if ((token & mdtTypeRef) != 0)
                tokenMap = module.EnumerateTypeRefToMethodTableMap();
            else
                return null;

            ulong mt = tokenMap.FirstOrDefault(r => r.Token == token).MethodTable;

            return GetOrCreateType(mt, 0);
        }

#pragma warning disable CA1822 // Mark members as static
        public ClrType? GetOrCreateArrayType(ClrType innerType, int ranks) => innerType != null ? new ClrConstructedType(innerType, ranks, pointer: false) : null;

        public ClrType? GetOrCreatePointerType(ClrType innerType, int depth) => innerType != null ? new ClrConstructedType(innerType, depth, pointer: true) : null;
#pragma warning restore CA1822 // Mark members as static

        private ClrType? TryGetComponentType(ulong obj)
        {
            ClrType? result = null;
            if (_typeHelpers.GetObjectArrayInformation(obj, out ObjectArrayInformation data))
            {
                if (data.ComponentType != 0)
                    result = GetOrCreateType(data.ComponentType, 0);

                if (result is null && data.ComponentElementType != 0)
                    result = GetOrCreateBasicType(data.ComponentElementType);
            }

            return result;
        }

        public ClrType GetOrCreateBasicType(ClrElementType basicType)
        {
            ClrModule bcl = _heap.Runtime.BaseClassLibrary;

            // We'll assume 'Class' is just System.Object
            if (basicType == ClrElementType.Class)
                basicType = ClrElementType.Object;

            ClrType?[]? basicTypes = _basicTypes;
            if (basicTypes is null)
            {
                basicTypes = new ClrType[(int)ClrElementType.SZArray];
                int count = 0;
                if (bcl.MetadataReader != null)
                {
                    foreach ((ulong mt, int _) in bcl.EnumerateTypeDefToMethodTableMap())
                    {
                        string? name = _typeHelpers.GetTypeName(0, mt, 0);
                        ClrElementType type = name switch
                        {
                            "System.Void" => ClrElementType.Void,
                            "System.Boolean" => ClrElementType.Boolean,
                            "System.Char" => ClrElementType.Char,
                            "System.SByte" => ClrElementType.Int8,
                            "System.Byte" => ClrElementType.UInt8,
                            "System.Int16" => ClrElementType.Int16,
                            "System.UInt16" => ClrElementType.UInt16,
                            "System.Int32" => ClrElementType.Int32,
                            "System.UInt32" => ClrElementType.UInt32,
                            "System.Int64" => ClrElementType.Int64,
                            "System.UInt64" => ClrElementType.UInt64,
                            "System.Single" => ClrElementType.Float,
                            "System.Double" => ClrElementType.Double,
                            "System.IntPtr" => ClrElementType.NativeInt,
                            "System.UIntPtr" => ClrElementType.NativeUInt,
                            "System.ValueType" => ClrElementType.Struct,
                            _ => ClrElementType.Unknown,
                        };

                        if (type != ClrElementType.Unknown)
                        {
                            basicTypes[(int)type - 1] = GetOrCreateType(mt, 0);
                            count++;

                            if (count == 16)
                                break;
                        }
                    }
                }

                basicTypes[(int)ClrElementType.Object] = _heap.ObjectType;
                basicTypes[(int)ClrElementType.String] = _heap.StringType;

                Interlocked.CompareExchange(ref _basicTypes, basicTypes, null);
            }

            int index = (int)basicType - 1;
            if (index < 0 || index > basicTypes.Length)
                throw new ArgumentException($"Cannot create type for ClrElementType {basicType}");

            ClrType? result = basicTypes[index];
            if (result is not null)
                return result;

            return basicTypes[index] = new ClrPrimitiveType(_typeHelpers, bcl, _heap, basicType);
        }

        private ClrModule GetModule(ulong moduleAddress)
        {
            if (_modules is null)
            {
                Dictionary<ulong, ClrModule> modules = _heap.Runtime.EnumerateModules().ToDictionary(k => k.Address, v => v);
                Interlocked.CompareExchange(ref _modules, modules, null);
            }

            if (!_modules.TryGetValue(moduleAddress, out ClrModule? module))
                module = _errorModule ??= new(_heap.Runtime.AppDomains[0], 0, _heap.Runtime.GetServiceOrThrow<IAbstractModuleHelpers>(), null, _heap.Runtime.DataTarget.DataReader);

            return module;
        }
    }
}