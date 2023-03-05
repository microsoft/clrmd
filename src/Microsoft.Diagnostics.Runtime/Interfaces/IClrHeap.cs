// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrHeap
    {
        bool CanWalkHeap { get; }
        IClrType ExceptionType { get; }
        IClrType FreeType { get; }
        bool IsServer { get; }
        IClrType ObjectType { get; }
        IClrRuntime Runtime { get; }
        ImmutableArray<IClrSegment> Segments { get; }
        IClrType StringType { get; }
        ImmutableArray<IClrSubHeap> SubHeaps { get; }

        IEnumerable<MemoryRange> EnumerateAllocationContexts();
        IEnumerable<IClrValue> EnumerateFinalizableObjects();
        IEnumerable<ClrFinalizerRoot> EnumerateFinalizerRoots();
        IEnumerable<IClrValue> EnumerateObjects();
        IEnumerable<IClrValue> EnumerateObjects(MemoryRange range);
        IEnumerable<IClrRoot> EnumerateRoots();
        IClrValue FindNextObjectOnSegment(ulong address);
        IClrValue FindPreviousObjectOnSegment(ulong address);
        IClrValue GetObject(ulong objRef);
        IClrType? GetObjectType(ulong objRef);
        IClrSegment? GetSegmentByAddress(ulong address);
        IClrType? GetTypeByMethodTable(ulong methodTable);
        IClrType? GetTypeByName(ClrModule module, string name);
        IClrType? GetTypeByName(string name);
    }
}