// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrSegment
    {
        ulong Address { get; }
        MemoryRange CommittedMemory { get; }
        ulong End { get; }
        ulong FirstObjectAddress { get; }
        MemoryRange Generation0 { get; }
        MemoryRange Generation1 { get; }
        MemoryRange Generation2 { get; }
        bool IsPinned { get; }
        GCSegmentKind Kind { get; }
        ulong Length { get; }
        MemoryRange ObjectRange { get; }
        MemoryRange ReservedMemory { get; }
        ulong Start { get; }
        IClrSubHeap SubHeap { get; }

        IEnumerable<IClrValue> EnumerateObjects();
        int GetGeneration(ulong obj);
    }
}