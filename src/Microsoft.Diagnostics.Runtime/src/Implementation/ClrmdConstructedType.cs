// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdConstructedType : ClrType
    {
        private readonly int _ranks;
        public override ClrHeap Heap => ComponentType.Heap;

        public override ClrModule Module => Heap.Runtime.BaseClassLibrary;
        public override ClrType ComponentType { get; }
        public override string Name
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(ComponentType.Name);
                if (IsPointer)
                {
                    for (int i = 0; i < _ranks; i++)
                        sb.Append('*');
                }
                else
                {
                    sb.Append('[');
                    for (int i = 0; i < _ranks - 1; i++)
                        sb.Append(',');
                    sb.Append(']');
                }

                return sb.ToString();
            }
        }

        public ClrmdConstructedType(ClrType componentType, int ranks, bool pointer)
        {
            ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
            ElementType = pointer ? ClrElementType.Pointer : ClrElementType.SZArray;
            _ranks = ranks;

            if (ranks <= 0)
                throw new ArgumentException($"{nameof(ranks)} must be 1 or greater.");
        }

        public override IClrObjectHelpers ClrObjectHelpers => ComponentType.ClrObjectHelpers;

        public override bool IsEnum => false;
        public override ClrEnum AsEnum() => throw new InvalidOperationException();

        // We have no good way of finding this value, unfortunately
        public override ClrElementType ElementType { get; }
        public override ulong MethodTable => 0;
        public override bool IsFinalizeSuppressed(ulong obj) => false;
        public override bool IsPointer => true;
        public override ImmutableArray<ClrInstanceField> Fields => ImmutableArray<ClrInstanceField>.Empty;
        public override ImmutableArray<ClrStaticField> StaticFields => ImmutableArray<ClrStaticField>.Empty;
        public override ImmutableArray<ClrMethod> Methods => ImmutableArray<ClrMethod>.Empty;
        public override IEnumerable<ClrGenericParameter> EnumerateGenericParameters() => Enumerable.Empty<ClrGenericParameter>();
        public override IEnumerable<ClrInterface> EnumerateInterfaces() => Enumerable.Empty<ClrInterface>();
        public override bool IsFinalizable => false;
        public override bool IsPublic => true;
        public override bool IsPrivate => false;
        public override bool IsInternal => false;
        public override bool IsProtected => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => false;
        public override bool IsShared => false;
        public override bool IsInterface => false;
        public override ClrInstanceField? GetInstanceFieldByName(string name) => null;
        public override ClrStaticField? GetStaticFieldByName(string name) => null;
        public override ClrType? BaseType => null;
        public override ulong GetArrayElementAddress(ulong objRef, int index) => 0;
        public override T[]? ReadArrayElements<T>(ulong objRef, int start, int count) => null;
        public override int StaticSize => IntPtr.Size;
        public override GCDesc GCDesc => default;
        public override int MetadataToken => 0;
        public override bool IsArray => !IsPointer;
        public override int ComponentSize => IntPtr.Size;
    }
}