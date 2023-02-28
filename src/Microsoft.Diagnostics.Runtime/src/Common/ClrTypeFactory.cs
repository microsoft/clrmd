using Microsoft.Diagnostics.Runtime.Builders;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrTypeFactory
    {
        ClrType FreeType { get; }
        ClrType StringType { get; }
        ClrType ObjectType { get; }
        ClrType ExceptionType { get; }

        ClrType? GetOrCreateType(ulong mt, ulong obj);
        ClrType GetOrCreateBasicType(ClrElementType basicType);
        ClrType? GetOrCreateArrayType(ClrType inner, int ranks);
        ClrType? GetOrCreateTypeFromToken(ClrModule module, int token);
        ClrType? GetOrCreateTypeFromSignature(ClrModule? module, SigParser parser, IEnumerable<ClrGenericParameter> typeParameters, IEnumerable<ClrGenericParameter> methodParameters);
        ClrType? GetOrCreatePointerType(ClrType innerType, int depth);

        bool CreateMethodsForType(ClrType type, out ImmutableArray<ClrMethod> methods);
        bool CreateFieldsForType(ClrType type, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> staticFields);
    }

    internal class ClrTypeFactory : IClrTypeFactory
    {
        private readonly SOSDac _sos;
        private readonly CacheOptions _options;
        private readonly ObjectPool<FieldBuilder> _fieldBuilders;
        private readonly ObjectPool<TypeBuilder> _typeBuilders;
        private readonly ClrHeap _heap;
        private volatile ClrType?[]? _basicTypes;
        private readonly Dictionary<ulong, ClrType> _types = new();
        private readonly CommonMethodTables _commonMTs;
        private Dictionary<ulong, ClrModule>? _modules;

        public ClrTypeFactory(ClrHeap heap, SOSDac sos, CacheOptions options)
        {
            _heap = heap;
            _sos = sos;
            _options = options;
            _fieldBuilders = new ObjectPool<FieldBuilder>((owner, obj) => obj.Owner = owner);
            _typeBuilders = new ObjectPool<TypeBuilder>((owner, obj) => obj.Owner = owner);

            _sos.GetCommonMethodTables(out _commonMTs);
        }

        public ClrType FreeType => CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _commonMTs.FreeMethodTable, "Free");

        public ClrType StringType
        {
            get
            {
                try
                {
                    return CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _commonMTs.StringMethodTable, "System.String");
                }
                catch (System.IO.InvalidDataException)
                {
                    int token = 0;
                    if (_sos.GetMethodTableData(_commonMTs.StringMethodTable, out MethodTableData mtd))
                        token = (int)mtd.Token;

                    return new ClrmdStringType((ITypeHelpers)_helpers, this, data.StringMethodTable, token);
                }
            }
        }

        public ClrType ObjectType => CreateSystemType(_heap, _heap.Runtime.BaseClassLibrary, _commonMTs.FreeMethodTable, "System.ObjectType");

        public ClrType ExceptionType
        {
            get
            {
                try
                {
                    ExceptionType = _helpers.Factory.CreateSystemType(this, data.ExceptionMethodTable, "System.Exception");
                }
                catch
                {
                    ExceptionType = ObjectType;
                }
            }
        }

        public ClrType CreateSystemType(ClrHeap heap, ClrModule? bcl, ulong mt, string typeName)
        {
            using TypeBuilder typeData = _typeBuilders.Rent();
            if (!typeData.Init(_sos, mt, this))
                throw new InvalidDataException($"Could not create well known type '{typeName}' from MethodTable {mt:x}.");

            ClrType? baseType = null;

            if (typeData.ParentMethodTable != 0 && !_types.TryGetValue(typeData.ParentMethodTable, out baseType))
                throw new InvalidOperationException($"Base type for '{typeName}' was not pre-created from MethodTable {typeData.ParentMethodTable:x}.");

            ClrmdType result;
            if (typeData.ComponentSize == 0)
                result = new ClrmdType(heap, baseType, bcl, typeData, typeName);
            else
                result = new ClrmdArrayType(heap, baseType, bcl, typeData, typeName);

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

            {
                ClrType? result = TryGetType(mt);
                if (result != null)
                {
                    if (obj != 0 && result.ComponentType is null && result.IsArray && result is ClrmdArrayType type)
                        TryGetComponentType(type, obj);

                    return result;
                }
            }

            {
                using TypeBuilder typeData = _typeBuilders.Rent();
                if (!typeData.Init(_sos, mt, this))
                    return null;

                ClrType? baseType = GetOrCreateType(typeData.ParentMethodTable, 0);

                ClrModule? module = GetModule(typeData.Module);
                if (typeData.ComponentSize == 0)
                {
                    ClrmdType result = new ClrmdType(_heap, baseType, module, typeData);

                    if (_options.CacheTypes)
                    {
                        lock (_types)
                            _types[mt] = result;
                    }

                    return result;
                }
                else
                {
                    ClrmdArrayType result = new ClrmdArrayType(_heap, baseType, module, typeData);

                    if (_options.CacheTypes)
                    {
                        lock (_types)
                            _types[mt] = result;
                    }

                    if (obj != 0 && result.IsArray && result.ComponentType is null)
                    {
                        TryGetComponentType(result, obj);
                    }

                    return result;
                }
            }
        }
        
        public ClrType? TryGetType(ulong mt)
        {
            lock (_types)
            {
                _types.TryGetValue(mt, out ClrType? result);
                return result;
            }
        }

        public ClrType? GetOrCreateTypeFromSignature(ClrModule? module, SigParser parser, IEnumerable<ClrGenericParameter> typeParameters, IEnumerable<ClrGenericParameter> methodParameters)
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

            if (etype == ClrElementType.Class || etype == ClrElementType.Struct)
            {
                if (!parser.GetToken(out int token))
                    return null;

                ClrType? result = module != null ? GetOrCreateTypeFromToken(module, token) : null;
                if (result == null)
                {
                    // todo, create a type from metadata instead of returning a basic type?
                    result = GetOrCreateBasicType(etype);
                }

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

                ClrType? result = module?.ResolveToken(token);
                return result;
            }

            if (etype == ClrElementType.MVar || etype == ClrElementType.Var)
            {
                if (!parser.GetData(out int index))
                    return null;

                ClrGenericParameter[] param = (etype == ClrElementType.Var ? typeParameters : methodParameters).ToArray();
                if (index < 0 || index >= param.Length)
                    return null;

                return new ClrmdGenericType(this, _heap, module, param[index]);
            }

            if (etype == ClrElementType.Pointer)
            {
                if (!parser.SkipCustomModifiers())
                    return null;

                ClrType? innerType = GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters);
                if (innerType == null)
                    innerType = GetOrCreateBasicType(ClrElementType.Void);

                return GetOrCreatePointerType(innerType, 1);
            }

            if (etype == ClrElementType.SZArray)
            {
                if (!parser.SkipCustomModifiers())
                    return null;

                ClrType? innerType = GetOrCreateTypeFromSignature(module, parser, typeParameters, methodParameters);
                if (innerType == null)
                    innerType = GetOrCreateBasicType(ClrElementType.Void);

                return GetOrCreateArrayType(innerType, 1);
            }

            DebugOnly.Assert(false);  // What could we have forgotten?  Should only happen in a corrupted signature.
            return null;
        }

        public ClrType? GetOrCreateTypeFromToken(ClrModule module, int token) => module.ResolveToken(token);

        public ClrType? GetOrCreateArrayType(ClrType innerType, int ranks) => innerType != null ? new ClrmdConstructedType(innerType, ranks, pointer: false) : null;
        public ClrType? GetOrCreatePointerType(ClrType innerType, int depth) => innerType != null ? new ClrmdConstructedType(innerType, depth, pointer: true) : null;

        private void TryGetComponentType(ClrmdArrayType type, ulong obj)
        {
            ClrType? result = null;
            if (_sos.GetObjectData(obj, out ObjectData data))
            {
                if (data.ElementTypeHandle != 0)
                    result = GetOrCreateType(data.ElementTypeHandle, 0);

                if (result is null && data.ElementType != 0)
                    result = GetOrCreateBasicType((ClrElementType)data.ElementType);

                type.SetComponentType(result);
            }
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
                if (bcl != null && bcl.MetadataImport != null)
                {
                    foreach ((ulong mt, int _) in bcl.EnumerateTypeDefToMethodTableMap())
                    {
                        string? name = _sos.GetMethodTableName(mt);
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

            return basicTypes[index] = new ClrmdPrimitiveType(this, bcl, _heap, basicType);
        }

        public bool CreateMethodsForType(ClrType type, out ImmutableArray<ClrMethod> methods)
        {
            ulong mt = type.MethodTable;
            if (!_sos.GetMethodTableData(mt, out MethodTableData data) || data.NumMethods == 0)
            {
                methods = ImmutableArray<ClrMethod>.Empty;
                return true;
            }

            using MethodBuilder builder = _methodBuilders.Rent();
            ImmutableArray<ClrMethod>.Builder result = ImmutableArray.CreateBuilder<ClrMethod>(data.NumMethods);
            result.Count = result.Capacity;

            int curr = 0;
            for (uint i = 0; i < data.NumMethods; i++)
            {
                if (builder.Init(_sos, mt, i, this))
                    result[curr++] = new ClrmdMethod(type, builder);
            }

            if (curr == 0)
            {
                methods = ImmutableArray<ClrMethod>.Empty;
                return true;
            }

            result.Capacity = result.Count = curr;
            methods = result.MoveToImmutable();
            return _options.CacheMethods;
        }

        public bool CreateFieldsForType(ClrType type, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> staticFields)
        {
            CreateFieldsForMethodTableWorker(type, out fields, out staticFields);

            if (fields.IsDefault)
                fields = ImmutableArray<ClrInstanceField>.Empty;

            if (staticFields.IsDefault)
                staticFields = ImmutableArray<ClrStaticField>.Empty;

            return _options.CacheFields;
        }

        private void CreateFieldsForMethodTableWorker(ClrType type, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> statics)
        {
            fields = default;
            statics = default;

            // If "type.BaseType" is null then this is either System.Object which has no fields, or the parent MethodTable
            // is invalid.  In this latter case, we actually get bogus field data from GetFieldInfo, leading to reporting
            // incorrect fields from this type.
            if (type.IsFree || type.BaseType is null)
                return;

            if (!_sos.GetFieldInfo(type.MethodTable, out DacInterface.FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
            {
                if (type.BaseType != null)
                    fields = type.BaseType.Fields;
                return;
            }

            ImmutableArray<ClrInstanceField>.Builder fieldsBuilder = ImmutableArray.CreateBuilder<ClrInstanceField>(fieldInfo.NumInstanceFields);
            ImmutableArray<ClrStaticField>.Builder staticsBuilder = ImmutableArray.CreateBuilder<ClrStaticField>(fieldInfo.NumStaticFields);

            fieldsBuilder.AddRange(type.BaseType.Fields);

            using FieldBuilder fieldData = _fieldBuilders.Rent();

            ulong nextField = fieldInfo.FirstFieldAddress;
            int other = 0;
            while (other + fieldsBuilder.Count + staticsBuilder.Count < fieldsBuilder.Capacity + staticsBuilder.Capacity && nextField != 0)
            {
                if (!fieldData.Init(_sos, nextField, this))
                    break;

                if (fieldData.IsContextLocal || fieldData.IsThreadLocal)
                {
                    other++;
                }
                else if (fieldData.IsStatic)
                {
                    ClrmdStaticField staticField = new(type, fieldData);
                    staticsBuilder.Add(staticField);
                }
                else
                {
                    ClrmdField field = new(type, fieldData);
                    fieldsBuilder.Add(field);
                }

                nextField = fieldData.NextField;
            }

            fieldsBuilder.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            fields = fieldsBuilder.MoveOrCopyToImmutable();
            statics = staticsBuilder.MoveOrCopyToImmutable();
        }

        private ClrModule? GetModule(ulong moduleAddress)
        {
            if (_modules is null)
            {
                var modules = _heap.Runtime.EnumerateModules().ToDictionary(k => k.Address, v => v);
                Interlocked.CompareExchange(ref _modules, modules, null);
            }

            _modules.TryGetValue(moduleAddress, out var module);
            return module;
        }
    }
}
