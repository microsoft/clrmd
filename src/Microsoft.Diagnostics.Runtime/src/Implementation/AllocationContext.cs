// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public struct AllocationContext
    {
        public ulong Pointer { get; }
        public ulong Limit { get; }

        public AllocationContext(ulong pointer, ulong limit)
        {
            Pointer = pointer;
            Limit = limit;

            DebugOnly.Assert(Pointer < Limit);
            DebugOnly.Assert(Limit != 0);
        }
    }
}