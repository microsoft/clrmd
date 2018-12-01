// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeObjectData
    {
        public readonly ulong MethodTable;
        public readonly DacpObjectType ObjectType;
        public readonly uint Size;
        public readonly ulong ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint dwRank;
        public readonly uint dwNumComponents;
        public readonly uint dwComponentSize;
        public readonly ulong ArrayDataPointer;
        public readonly ulong ArrayBoundsPointer;
        public readonly ulong ArrayLowerBoundsPointer;
    }
}