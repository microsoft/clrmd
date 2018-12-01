// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeSegementData
    {
        public readonly ulong Address;
        public readonly ulong Allocated;
        public readonly ulong Committed;
        public readonly ulong Reserved;
        public readonly ulong Used;
        public readonly ulong Mem;
        public readonly ulong Next;
        public readonly ulong Heap;
        public readonly ulong HighAllocMark;
        public readonly uint IsReadOnly;
    }
}