// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class PrimitiveType : ClrType
    {
        public PrimitiveType(ClrHeapImpl heap, ClrElementType type)
        {
            Heap = heap;
            ElementType = type;
        }

        public override int BaseSize => DesktopInstanceField.GetSize(this, ElementType);
        public override ClrType BaseType => null; // todo;
        public override int ElementSize => 0;
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
        public override string Name
        {
            get
            {
                return ElementType switch
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
            }
        }

        public override ulong GetArrayElementAddress(ulong objRef, int index) => 0;
        public override object GetArrayElementValue(ulong objRef, int index) => null;

        public override ClrInstanceField GetFieldByName(string name)
        {
            return null;
        }

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            childField = null;
            childFieldOffset = 0;
            return false;
        }

        // todo
        public override ulong GetSize(ulong objRef) => 0;

        public override ClrStaticField GetStaticFieldByName(string name) => null;
        public override IReadOnlyList<ClrInstanceField> Fields => Array.Empty<ClrInstanceField>();
    }
}