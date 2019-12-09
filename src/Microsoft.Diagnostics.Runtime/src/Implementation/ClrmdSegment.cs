// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public class ClrmdSegment : ClrSegment
    {
        public ClrmdSegment(ClrHeap clrHeap, ISegmentBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            Heap = clrHeap;

            LogicalHeap = builder.LogicalHeap;
            Start = builder.Start;
            End = builder.End;

            IsLargeObjectSegment = builder.IsLargeObjectSegment;
            IsEphemeralSegment = builder.IsEphemeralSegment;

            ReservedEnd = builder.ReservedEnd;
            CommittedEnd = builder.CommitedEnd;

            Gen0Start = builder.Gen0Start;
            Gen0Length = builder.Gen0Length;
            Gen1Start = builder.Gen1Start;
            Gen1Length = builder.Gen1Length;
            Gen2Start = builder.Gen2Start;
            Gen2Length = builder.Gen2Length;
        }

        public override ClrHeap Heap { get; }

        public override int LogicalHeap { get; }
        public override ulong Start { get; }
        public override ulong End { get; }

        public override bool IsLargeObjectSegment { get; }
        public override bool IsEphemeralSegment { get; }

        public override ulong ReservedEnd { get; }
        public override ulong CommittedEnd { get; }

        public override ulong Gen0Start { get; }
        public override ulong Gen0Length { get; }
        public override ulong Gen1Start { get; }
        public override ulong Gen1Length { get; }
        public override ulong Gen2Start { get; }
        public override ulong Gen2Length { get; }

        public override IEnumerable<ClrObject> EnumerateObjects() => ((ClrmdHeap)Heap).EnumerateObjects(this);

        public override ulong FirstObject => Gen2Start < End ? Gen2Start : 0;
    }
}