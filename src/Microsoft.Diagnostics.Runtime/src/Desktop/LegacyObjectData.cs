// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct LegacyObjectData : IObjectData
    {
        public readonly ulong EEClass;
        public readonly ulong MethodTable;
        public readonly uint ObjectType;
        public readonly uint Size;
        public readonly ulong ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint Rank;
        public readonly uint NumComponents;
        public readonly uint ComponentSize;
        public readonly ulong ArrayDataPtr;
        public readonly ulong ArrayBoundsPtr;
        public readonly ulong ArrayLowerBoundsPtr;

        ClrElementType IObjectData.ElementType => (ClrElementType)ElementType;
        ulong IObjectData.ElementTypeHandle => ElementTypeHandle;
        ulong IObjectData.RCW => 0;
        ulong IObjectData.CCW => 0;
        ulong IObjectData.DataPointer => ArrayDataPtr;
    }
}