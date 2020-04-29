// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdPrimitiveType : ClrType
    {
        public ClrmdPrimitiveType(ITypeHelpers helpers, ClrModule module, ClrHeap heap, ClrElementType type)
        {
            if (helpers is null)
                throw new ArgumentNullException(nameof(helpers));

            ClrObjectHelpers = helpers.ClrObjectHelpers;
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Heap = heap ?? throw new ArgumentNullException(nameof(heap));
            ElementType = type;
        }

        public override bool IsEnum => false;
        public override ClrEnum AsEnum() => throw new InvalidOperationException();
        public override ClrModule Module { get; }
        public override IClrObjectHelpers ClrObjectHelpers { get; }
        public override ClrElementType ElementType { get; }
        public override bool IsShared => false;
        public override int StaticSize => ClrmdField.GetSize(this, ElementType);
        public override ClrType? BaseType => null; // todo;
        public override ClrHeap Heap { get; }
        public override IEnumerable<ClrGenericParameter> EnumerateGenericParameters() => Enumerable.Empty<ClrGenericParameter>();
        public override IEnumerable<ClrInterface> EnumerateInterfaces() => Enumerable.Empty<ClrInterface>();
        public override bool IsAbstract => false;
        public override bool IsFinalizable => false;
        public override bool IsInterface => false;
        public override bool IsInternal => false;
        public override bool IsPrivate => false;
        public override bool IsProtected => false;
        public override bool IsPublic => false;
        public override bool IsSealed => false;
        public override int MetadataToken => 0;
        public override ulong MethodTable => 0;

        public override string Name => ElementType switch
        {
            ClrElementType.Boolean => "System.Boolean",
            ClrElementType.Char => "System.Char",
            ClrElementType.Int8 => "System.SByte",
            ClrElementType.UInt8 => "System.Byte",
            ClrElementType.Int16 => "System.Int16",
            ClrElementType.UInt16 => "System.UInt16",
            ClrElementType.Int32 => "System.Int32",
            ClrElementType.UInt32 => "System.UInt32",
            ClrElementType.Int64 => "System.Int64",
            ClrElementType.UInt64 => "System.UInt64",
            ClrElementType.Float => "System.Single",
            ClrElementType.Double => "System.Double",
            ClrElementType.NativeInt => "System.IntPtr",
            ClrElementType.NativeUInt => "System.UIntPtr",
            ClrElementType.Struct => "Sytem.ValueType",
            _ => ElementType.ToString(),
        };

        public override ulong GetArrayElementAddress(ulong objRef, int index) => 0;
        public override T[]? ReadArrayElements<T>(ulong objRef, int start, int count) => null;

        public override ClrInstanceField? GetInstanceFieldByName(string name) => null;

        public override ClrStaticField? GetStaticFieldByName(string name) => null;

        public override bool IsFinalizeSuppressed(ulong obj) => false;

        public override ImmutableArray<ClrInstanceField> Fields => ImmutableArray<ClrInstanceField>.Empty;

        public override GCDesc GCDesc => default;

        public override ImmutableArray<ClrStaticField> StaticFields => ImmutableArray<ClrStaticField>.Empty;

        public override ImmutableArray<ClrMethod> Methods => ImmutableArray<ClrMethod>.Empty;

        public override ClrType? ComponentType => null;

        public override bool IsArray => false;

        public override int ComponentSize => 0;
    }
}