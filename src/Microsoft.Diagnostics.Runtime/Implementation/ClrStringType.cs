// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using TypeInfo = Microsoft.Diagnostics.Runtime.AbstractDac.TypeInfo;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrStringType : ClrType
    {
        public ClrStringType(ClrModule module, IAbstractTypeHelpers helpers, ClrHeap heap, in TypeInfo typeInfo)
            : base(module, typeInfo, helpers)
        {
            Heap = heap;
        }

        public override GCDesc GCDesc => default;

        public override string? Name => "System.String";

        public override ClrHeap Heap { get; }

        public override ClrElementType ElementType => ClrElementType.String;

        public override bool IsFinalizable => true;

        public override TypeAttributes TypeAttributes => TypeAttributes.Public;

        public override ClrType? BaseType => Heap.ObjectType;

        public override ClrType? ComponentType => null;

        public override bool IsArray => false;

        public override bool IsEnum => false;

        public override bool IsString => true;

        public override ClrEnum AsEnum() => throw new InvalidOperationException($"{Name ?? nameof(ClrType)} is not an enum.  You must call {nameof(ClrType.IsEnum)} before using {nameof(AsEnum)}.");

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override T[]? ReadArrayElements<T>(ulong objRef, int start, int count)
        {
            throw new NotImplementedException();
        }

        // TODO: remove
        public override ClrStaticField? GetStaticFieldByName(string name) => StaticFields.FirstOrDefault(f => f.Name == name);

        // TODO: remove
        public override ClrInstanceField? GetFieldByName(string name) => Fields.FirstOrDefault(f => f.Name == name);

        private const uint FinalizationSuppressedFlag = 0x40000000;
        public override bool IsFinalizeSuppressed(ulong obj)
        {
            // TODO move to ClrObject?
            uint value = Module.DataReader.Read<uint>(obj - 4);

            return (value & FinalizationSuppressedFlag) == FinalizationSuppressedFlag;
        }
    }
}