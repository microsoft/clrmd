// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class HeapTests
    {
        [Fact]
        public void HeapEnumeration()
        {
            // Simply test that we can enumerate the heap.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            bool encounteredFoo = false;
            int count = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                ClrType type = heap.GetObjectType(obj);
                Assert.NotNull(type);
                if (type.Name == "Foo")
                    encounteredFoo = true;

                count++;
            }

            Assert.True(encounteredFoo);
            Assert.True(count > 0);
        }


        [Fact]
        public void ServerSegmentTests()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server);
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Assert.True(heap.IsServer);

            CheckSegments(heap);
        }

        [Fact]
        public void WorkstationSegmentTests()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Workstation);
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Assert.False(heap.IsServer);

            CheckSegments(heap);
        }

        private static void CheckSegments(ClrHeap heap)
        {
            foreach (ClrSegment seg in heap.Segments)
            {
                Assert.NotEqual(0ul, seg.Start);
                Assert.NotEqual(0ul, seg.End);
                Assert.True(seg.Start <= seg.End);

                Assert.True(seg.Start < seg.CommittedEnd);
                Assert.True(seg.CommittedEnd < seg.ReservedEnd);

                if (!seg.IsEphemeralSegment)
                {
                    Assert.Equal(0ul, seg.Gen0Length);
                    Assert.Equal(0ul, seg.Gen1Length);
                }

                int count = 0;
                foreach (ulong obj in seg.EnumerateObjects())
                {
                    ClrSegment curr = heap.GetSegmentByAddress(obj);
                    Assert.Same(seg, curr);
                    count++;
                }

                Assert.True(count >= 1);
            }
        }
    }
}