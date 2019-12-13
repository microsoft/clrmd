// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct V45ObjectData : IObjectData
    {
        public readonly ClrDataAddress MethodTable;
        public readonly uint ObjectType;
        public readonly ulong Size;
        public readonly ClrDataAddress ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint Rank;
        public readonly ulong NumComponents;
        public readonly ulong ComponentSize;
        public readonly ClrDataAddress ArrayDataPointer;
        public readonly ClrDataAddress ArrayBoundsPointer;
        public readonly ClrDataAddress ArrayLowerBoundsPointer;
        public readonly ClrDataAddress RCW;
        public readonly ClrDataAddress CCW;

        ClrElementType IObjectData.ElementType => (ClrElementType)ElementType;
        ulong IObjectData.ElementTypeHandle => ElementTypeHandle;
        ulong IObjectData.RCW => RCW;
        ulong IObjectData.CCW => CCW;
        ulong IObjectData.DataPointer => ArrayDataPointer;
    }
}