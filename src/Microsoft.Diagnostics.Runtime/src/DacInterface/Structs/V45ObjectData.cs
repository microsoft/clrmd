// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct V45ObjectData : IObjectData
    {
        public readonly ulong MethodTable;
        public readonly uint ObjectType;
        public readonly ulong Size;
        public readonly ulong ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint Rank;
        public readonly ulong NumComponents;
        public readonly ulong ComponentSize;
        public readonly ulong ArrayDataPointer;
        public readonly ulong ArrayBoundsPointer;
        public readonly ulong ArrayLowerBoundsPointer;
        public readonly ulong RCW;
        public readonly ulong CCW;

        ClrElementType IObjectData.ElementType => (ClrElementType)ElementType;
        ulong IObjectData.ElementTypeHandle => ElementTypeHandle;
        ulong IObjectData.RCW => RCW;
        ulong IObjectData.CCW => CCW;
        ulong IObjectData.DataPointer => ArrayDataPointer;
    }
}