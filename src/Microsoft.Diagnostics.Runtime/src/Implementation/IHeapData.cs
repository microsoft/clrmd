// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IHeapData
    {
        IHeapHelpers HeapHelpers { get; }
        bool IsServer { get; }
        int LogicalHeapCount { get; }

        ulong ArrayMethodTable { get; }
        ulong StringMethodTable { get; }
        ulong ObjectMethodTable { get; }
        ulong ExceptionMethodTable { get; }
        ulong FreeMethodTable { get; }

        bool CanWalkHeap { get; }
    }
}