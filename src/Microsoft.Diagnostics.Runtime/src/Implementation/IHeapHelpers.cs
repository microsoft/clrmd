// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IHeapHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }

        IEnumerable<(ulong, ulong)> EnumerateDependentHandleLinks();
        bool CreateSegments(
            ClrHeap clrHeap,
            out IReadOnlyList<ClrSegment> segemnts,
            out IReadOnlyList<AllocationContext> allocationContexts,
            out IReadOnlyList<FinalizerQueueSegment> fqRoots,
            out IReadOnlyList<FinalizerQueueSegment> fqObjects);
    }
}