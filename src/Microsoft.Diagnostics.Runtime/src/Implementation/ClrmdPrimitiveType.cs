// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public class ClrmdPrimitiveType : ClrType
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
        public override IEnumerable<ClrInterface> EnumerateInterfaces() => Array.Empty<ClrInterface>();
        public override bool IsAbstract => false;
        public override bool IsFinalizable => false;
        public override bool IsInterface => false;
        public override bool IsInternal => false;
        public override bool IsPrivate => false;
        public override bool IsProtected => false;
        public override bool IsPublic => false;
        public override bool IsSealed => false;
        public override uint MetadataToken => 0;
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
        public override object? GetArrayElementValue(ulong objRef, int index) => null;

        public override ClrInstanceField? GetFieldByName(string name) => null;
        
        public override ClrStaticField? GetStaticFieldByName(string name) => null;
    
        public override bool IsFinalizeSuppressed(ulong obj) => false;

        public override IReadOnlyList<ClrInstanceField> Fields => Array.Empty<ClrInstanceField>();

        public override GCDesc GCDesc => default;

        public override IReadOnlyList<ClrStaticField> StaticFields => Array.Empty<ClrStaticField>();

        public override IReadOnlyList<ClrMethod> Methods => Array.Empty<ClrMethod>();

        public override ClrType? ComponentType => null;

        public override bool IsArray => false;

        public override int ComponentSize => 0;
    }
}