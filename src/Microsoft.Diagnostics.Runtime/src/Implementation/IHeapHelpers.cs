// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IHeapHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }

        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandleLinks();
        bool CreateSegments(ClrHeap clrHeap, out ImmutableArray<ClrSegment> segemnts, out ImmutableArray<MemoryRange> allocationContexts,
                            out ImmutableArray<FinalizerQueueSegment> fqRoots, out ImmutableArray<FinalizerQueueSegment> fqObjects);
    }
}