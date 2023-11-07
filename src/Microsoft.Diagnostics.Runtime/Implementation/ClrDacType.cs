// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using TypeInfo = Microsoft.Diagnostics.Runtime.AbstractDac.TypeInfo;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrDacType : ClrType
    {
        private string? _name;
        private TypeAttributes _attributes;
        private ulong _loaderAllocatorHandle = ulong.MaxValue - 1;
        private ulong _assemblyLoadContextHandle = ulong.MaxValue - 1;

        private ClrElementType _elementType;
        private GCDesc _gcDesc;
        private ClrType? _componentType;
        private int _baseArrayOffset;

        public override IEnumerable<ClrGenericParameter> EnumerateGenericParameters()
        {
            // We won't recover from Module being null, so we'll return an empty params list from that.
            ClrModule? module = Module;
            if (module is null)
                yield break;

            // We'll return default if we can't get MetdataImport.  This effectively means we'll try again
            // to get MetadataImport later.
            IAbstractMetadataReader? import = module.MetadataReader;
            if (import == null)
                yield break;

            foreach (GenericParameterInfo info in import.EnumerateGenericParameters(MetadataToken))
                yield return new ClrGenericParameter(info.Token, info.Index, info.Attributes, info.Name ?? "");
        }

        public override string? Name
        {
            get
            {
                // Name can't really be string.Empty for a valid type, so we use that as a sentinel for
                // "we tried to get the type name but it was null" to avoid calling GetTypeName over and
                // over.
                if (_name == null)
                {
                    string? name = Helpers.GetTypeName(Module.Address, MethodTable, MetadataToken);
                    name ??= string.Empty;

                    StringCaching caching = GetCacheOptions().CacheTypeNames;
                    if (caching is StringCaching.Cache)
                        _name = name;
                    if (caching is StringCaching.Intern)
                        _name = string.Intern(name);
                    else
                        return name.Length != 0 ? name : null;
                }

                if (_name.Length == 0)
                    return null;

                return _name;
            }
        }

        public override ClrType? ComponentType => _componentType;
        public override GCDesc GCDesc => GetOrCreateGCDesc();

        public override ClrElementType ElementType => GetElementType();

        public override ClrHeap Heap { get; }

        public override ClrType? BaseType { get; }


        public ClrDacType(IAbstractTypeHelpers helpers, ClrHeap heap, ClrType? baseType, ClrType? componentType, ClrModule module, in TypeInfo data, string? name = null)
            : base(module, data, helpers)
        {
            Heap = heap;
            BaseType = baseType;
            _componentType = componentType;
            _name = name;

            // If there are no methods, preempt the expensive work to create methods
            if (data.MethodCount == 0)
                _methods = ImmutableArray<ClrMethod>.Empty;

            DebugOnlyLoadLazyValues();
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            _ = Name;
        }

        public void SetComponentType(ClrType? type) => _componentType = type;

        private GCDesc GetOrCreateGCDesc()
        {
            if (!ContainsPointers || !_gcDesc.IsEmpty)
                return _gcDesc;

            IDataReader reader = Module.DataReader;
            if (reader is null)
                return default;

            DebugOnly.Assert(MethodTable != 0, "Attempted to fill GC desc with a constructed (not real) type.");
            if (!reader.Read(MethodTable - (ulong)IntPtr.Size, out int entries))
            {
                _gcDesc = default;
                return default;
            }

            // Get entries in map
            if (entries < 0)
                entries = -entries;

            int slots = 1 + entries * 2;
            byte[] buffer = new byte[slots * IntPtr.Size];
            if (reader.Read(MethodTable - (ulong)(slots * IntPtr.Size), buffer) != buffer.Length)
            {
                _gcDesc = default;
                return default;
            }

            // Construct the gc desc
            return _gcDesc = new GCDesc(buffer);
        }

        private ClrElementType GetElementType()
        {
            if (_elementType != ClrElementType.Unknown)
                return _elementType;

            if (this == Heap.ObjectType)
                return _elementType = ClrElementType.Object;

            if (this == Heap.StringType)
                return _elementType = ClrElementType.String;

            if (ComponentSize > 0)
                return _elementType = StaticSize > (uint)(3 * IntPtr.Size) ? ClrElementType.Array : ClrElementType.SZArray;

            ClrType? baseType = BaseType;
            if (baseType is null)
                return _elementType = ClrElementType.Object;

            if (baseType == Heap.ObjectType)
                return _elementType = ClrElementType.Class;

            if (baseType.Name != "System.ValueType")
            {
                ClrElementType et = baseType.ElementType;
                return _elementType = et;
            }

            return _elementType = Name switch
            {
                "System.Int32" => ClrElementType.Int32,
                "System.Int16" => ClrElementType.Int16,
                "System.Int64" => ClrElementType.Int64,
                "System.IntPtr" => ClrElementType.NativeInt,
                "System.UInt16" => ClrElementType.UInt16,
                "System.UInt32" => ClrElementType.UInt32,
                "System.UInt64" => ClrElementType.UInt64,
                "System.UIntPtr" => ClrElementType.NativeUInt,
                "System.Boolean" => ClrElementType.Boolean,
                "System.Single" => ClrElementType.Float,
                "System.Double" => ClrElementType.Double,
                "System.Byte" => ClrElementType.UInt8,
                "System.Char" => ClrElementType.Char,
                "System.SByte" => ClrElementType.Int8,
                "System.Enum" => ClrElementType.Int32,
                _ => ClrElementType.Struct,
            };
        }

        public override bool IsException
        {
            get
            {
                ClrType? type = this;
                while (type != null)
                    if (type == Heap.ExceptionType)
                        return true;
                    else
                        type = type.BaseType;

                return false;
            }
        }

        public override bool IsEnum
        {
            get
            {
                for (ClrType? type = this; type != null; type = type.BaseType)
                    if (type.Name == "System.Enum")
                        return true;

                return false;
            }
        }

        public override ClrEnum AsEnum()
        {
            if (!IsEnum)
                throw new InvalidOperationException($"{Name ?? nameof(ClrType)} is not an enum.  You must call {nameof(ClrType.IsEnum)} before using {nameof(AsEnum)}.");

            return new ClrEnum(this);
        }

        public override bool IsFree => this == Heap.FreeType;

        private const uint FinalizationSuppressedFlag = 0x40000000;

        public override bool IsFinalizeSuppressed(ulong obj)
        {
            // TODO move to ClrObject?
            uint value = Module.DataReader.Read<uint>(obj - 4);

            return (value & FinalizationSuppressedFlag) == FinalizationSuppressedFlag;
        }

        public override bool IsFinalizable => Methods.Any(method => (method.Attributes & MethodAttributes.Virtual) == MethodAttributes.Virtual && method.Name == "Finalize");

        public override bool IsArray => ComponentSize != 0 && !IsString && !IsFree;
        public override bool IsCollectible => LoaderAllocatorHandle != 0;

        public override ulong LoaderAllocatorHandle
        {
            get
            {
                if (_loaderAllocatorHandle != ulong.MaxValue - 1)
                    return _loaderAllocatorHandle;

                ulong handle = Helpers.GetLoaderAllocatorHandle(MethodTable);
                _loaderAllocatorHandle = handle;
                return handle;
            }
        }

        public override ulong AssemblyLoadContextAddress
        {
            get
            {
                if (_assemblyLoadContextHandle != ulong.MaxValue - 1)
                    return _assemblyLoadContextHandle;

                return _assemblyLoadContextHandle = Helpers.GetAssemblyLoadContextAddress(MethodTable);
            }
        }

        public override bool IsString => this == Heap.StringType;

        // TODO: remove
        public override ClrStaticField? GetStaticFieldByName(string name) => StaticFields.FirstOrDefault(f => f.Name == name);

        // TODO: remove
        public override ClrInstanceField? GetFieldByName(string name) => Fields.FirstOrDefault(f => f.Name == name);

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            if (ComponentSize == 0)
                throw new InvalidOperationException($"{Name} is not an array.");

            if (_baseArrayOffset == 0)
            {
                ClrType? componentType = ComponentType;

                if (Helpers.GetObjectArrayInformation(objRef, out ObjectArrayInformation data))
                {
                    if (data.DataPointer > 0)
                    {
                        _baseArrayOffset = data.DataPointer;
                    }
                    else if (componentType != null)
                    {
                        if (!componentType.IsObjectReference)
                            _baseArrayOffset = IntPtr.Size * 2;
                    }
                    else
                    {
                        _baseArrayOffset = int.MinValue;
                    }
                }
                else
                {
                    _baseArrayOffset = int.MinValue;
                }
            }

            return objRef + (ulong)(_baseArrayOffset + index * ComponentSize);
        }

        public override T[]? ReadArrayElements<T>(ulong objRef, int start, int count)
        {
            if (ComponentSize == 0)
                throw new InvalidOperationException($"{Name} is not an array.");

            ulong address = GetArrayElementAddress(objRef, start);
            ClrType? componentType = ComponentType;
            ClrElementType cet;
            if (componentType != null)
            {
                cet = componentType.ElementType;
            }
            else
            {
                // Slow path, we need to get the element type of the array.
                if (!Helpers.GetObjectArrayInformation(objRef, out ObjectArrayInformation data))
                    return null;

                cet = data.ComponentElementType;
            }

            if (cet == ClrElementType.Unknown)
                return null;

            if (address == 0)
                return null;

            T[] values = new T[count];
            Span<byte> buffer = MemoryMarshal.Cast<T, byte>(values);

            if (Module.DataReader.Read(address, buffer) == buffer.Length)
                return values;

            return null;
        }

        // convenience function for testing
        public static string? FixGenerics(string? name) => DacNameParser.Parse(name);

        private void InitFlags()
        {
            if (_attributes != 0 || Module is null)
                return;

            IAbstractMetadataReader? import = Module.MetadataReader;
            if (import is not null && import.GetTypeDefInfo(MetadataToken, out TypeDefInfo info) && info.Attributes != 0)
                _attributes = info.Attributes;
            else
                _attributes = (TypeAttributes)0x70000000;
        }

        public override TypeAttributes TypeAttributes
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();
                return _attributes;
            }
        }
    }
}