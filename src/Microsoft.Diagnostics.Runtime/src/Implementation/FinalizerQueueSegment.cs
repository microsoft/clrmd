// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public readonly struct FinalizerQueueSegment
    {
        public ulong Start { get; }
        public ulong End { get; }

        public FinalizerQueueSegment(ulong start, ulong end)
        {
            Start = start;
            End = end;

            DebugOnly.Assert(Start <= End);
            DebugOnly.Assert(End != 0);
        }
    }
}