// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrmdStringType : ClrType
    {
        private IClrTypeHelpers Helpers { get; }
        private ImmutableArray<ClrMethod> _methods;
        private ImmutableArray<ClrInstanceField> _fields;
        private ImmutableArray<ClrStaticField> _statics;

        public ClrmdStringType(IClrTypeHelpers helpers, ClrHeap heap, ulong mt, int token)
        {
            Helpers = helpers;
            Heap = heap;
            MethodTable = mt;

            MetadataToken = token;
        }

        public override GCDesc GCDesc => default;

        public override ulong MethodTable { get; }

        public override int MetadataToken { get; }

        public override string? Name => "System.String";

        public override ClrHeap Heap { get; }

        public override ClrModule? Module => Heap.Runtime.BaseClassLibrary;

        public override ClrElementType ElementType => ClrElementType.String;

        public override bool ContainsPointers => false;

        public override bool IsFinalizable => true;

        public override bool IsPublic => true;

        public override bool IsPrivate => false;

        public override bool IsInternal => false;

        public override bool IsProtected => false;

        public override bool IsAbstract => false;

        public override bool IsSealed => true;

        public override bool IsInterface => false;

        public override ImmutableArray<ClrInstanceField> Fields
        {
            get
            {
                if (!_fields.IsDefault)
                    return _fields;

                if (Helpers.Factory.CreateFieldsForType(this, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> statics))
                {
                    _fields = fields;
                    _statics = statics;
                }

                return fields;
            }
        }

        public override ImmutableArray<ClrStaticField> StaticFields
        {
            get
            {
                if (!_statics.IsDefault)
                    return _statics;

                if (Helpers.Factory.CreateFieldsForType(this, out ImmutableArray<ClrInstanceField> fields, out ImmutableArray<ClrStaticField> statics))
                {
                    _fields = fields;
                    _statics = statics;
                }

                return statics;
            }
        }

        public override ImmutableArray<ClrMethod> Methods
        {
            get
            {
                if (!_methods.IsDefault)
                    return _methods;

                // Returns whether or not we should cache methods or not
                if (Helpers.Factory.CreateMethodsForType(this, out ImmutableArray<ClrMethod> methods))
                    _methods = methods;

                return methods;
            }
        }

        public override ClrType? BaseType => Heap.ObjectType;

        public override ClrType? ComponentType => null;

        public override bool IsArray => false;

        public override int StaticSize => IntPtr.Size + sizeof(int);

        public override int ComponentSize => sizeof(char);

        public override bool IsEnum => false;

        public override bool IsShared => true;

        public override bool IsString => true;

        internal override IClrTypeHelpers Helpers => Helpers.ClrObjectHelpers;

        public override ClrEnum AsEnum() => throw new InvalidOperationException($"{Name ?? nameof(ClrType)} is not an enum.  You must call {nameof(ClrType.IsEnum)} before using {nameof(AsEnum)}.");

        public override IEnumerable<ClrInterface> EnumerateInterfaces()
        {
            MetadataImport? import = Module?.MetadataImport;
            if (import is null)
                yield break;

            foreach (int token in import.EnumerateInterfaceImpls(MetadataToken))
            {
                if (import.GetInterfaceImplProps(token, out _, out int mdIFace))
                {
                    ClrInterface? result = GetInterface(import, mdIFace);
                    if (result != null)
                        yield return result;
                }
            }
        }

        private ClrInterface? GetInterface(MetadataImport import, int mdIFace)
        {
            ClrInterface? result = null;
            if (!import.GetTypeDefProperties(mdIFace, out string? name, out _, out int extends).IsOK)
            {
                name = import.GetTypeRefName(mdIFace);
            }

            // TODO:  Handle typespec case.
            if (name != null)
            {
                ClrInterface? type = null;
                if (extends != 0 && extends != 0x01000000)
                    type = GetInterface(import, extends);

                result = new ClrInterface(name, type);
            }

            return result;
        }

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
            uint value = Helpers.DataReader.Read<uint>(obj - 4);

            return (value & FinalizationSuppressedFlag) == FinalizationSuppressedFlag;
        }
    }
}
