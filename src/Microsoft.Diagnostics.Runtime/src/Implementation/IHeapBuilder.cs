// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IHeapBuilder
    {
        IHeapHelpers HeapHelpers { get; }
        public bool IsServer { get; }
        int LogicalHeapCount { get; }

        ulong ArrayMethodTable { get; }
        ulong StringMethodTable { get; }
        ulong ObjectMethodTable { get; }
        ulong ExceptionMethodTable { get; }
        ulong FreeMethodTable { get; }

        bool CanWalkHeap { get; }

        IReadOnlyList<ClrSegment> CreateSegments(ClrHeap clrHeap, out IReadOnlyList<AllocationContext> allocationContexts,
                                                 out IReadOnlyList<FinalizerQueueSegment> fqRoots, out IReadOnlyList<FinalizerQueueSegment> fqObjects);
    }
}