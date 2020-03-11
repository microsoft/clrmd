// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public class ClrmdSegment : ClrSegment
    {
        private readonly ClrmdHeap _clrmdHeap;

        public ClrmdSegment(ClrmdHeap heap, ISegmentData data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _clrmdHeap = heap;

            LogicalHeap = data.LogicalHeap;
            Start = data.Start;
            End = data.End;

            IsLargeObjectSegment = data.IsLargeObjectSegment;
            IsEphemeralSegment = data.IsEphemeralSegment;

            ReservedEnd = data.ReservedEnd;
            CommittedEnd = data.CommitedEnd;

            Gen0Start = data.Gen0Start;
            Gen0Length = data.Gen0Length;
            Gen1Start = data.Gen1Start;
            Gen1Length = data.Gen1Length;
            Gen2Start = data.Gen2Start;
            Gen2Length = data.Gen2Length;
        }

        public override ClrHeap Heap => _clrmdHeap;

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

        public override IEnumerable<ClrObject> EnumerateObjects() => _clrmdHeap.EnumerateObjects(this);

        public override ulong NextObject(ulong obj) => _clrmdHeap.NextObject(this, obj);

        public override ulong FirstObjectAddress => Gen2Start < End ? Gen2Start : 0;
    }
}