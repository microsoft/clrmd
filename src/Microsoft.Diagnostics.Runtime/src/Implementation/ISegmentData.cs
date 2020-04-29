// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface ISegmentData
    {
        int LogicalHeap { get; }
        ulong BaseAddress { get; }
        ulong Start { get; }
        ulong End { get; }
        ulong ReservedEnd { get; }
        ulong CommittedEnd { get; }
        ulong Gen0Start { get; }
        ulong Gen0Length { get; }
        ulong Gen1Start { get; }
        ulong Gen1Length { get; }
        ulong Gen2Start { get; }
        ulong Gen2Length { get; }

        bool IsLargeObjectSegment { get; }
        bool IsEphemeralSegment { get; }
    }
}