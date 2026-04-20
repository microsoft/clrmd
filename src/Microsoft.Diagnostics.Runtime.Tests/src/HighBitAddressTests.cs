// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Exercises ClrMD's 32-bit high-bit (>=0x80000000) address code paths by loading dumps
    /// captured under the HighBitHost harness. These tests validate that sign-extension fixups,
    /// minidump ULONG64 handling, and ClrDataAddress round-trips behave correctly when the CLR
    /// heap lives above the 2 GB line in a 32-bit target. Windows x86 only.
    /// </summary>
    public class HighBitAddressTests
    {
        private const ulong HighBitBoundary = 0x80000000UL;

        [HighBitFact]
        public void Types_HeapHasHighBitObjects()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int total = 0;
            int highBit = 0;
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                total++;
                if (obj.Address >= HighBitBoundary)
                {
                    highBit++;
                    // Round-trip: we must be able to resolve the type and read at least one byte.
                    ClrType type = obj.Type;
                    Assert.NotNull(type);
                    Assert.False(string.IsNullOrEmpty(type.Name));
                }
            }

            Assert.True(total > 0, "heap enumeration returned zero objects");
            Assert.True(
                highBit > 0,
                $"HighBitHost dump contained {total} objects but none had addresses >= 0x80000000. " +
                "The low-memory reservation may not have pushed the GC heap into the upper 2 GB.");
        }

        [HighBitFact]
        public void Types_SegmentsRoundTripThroughDataReader()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            bool sawHighBitSegment = false;
            foreach (ClrSegment seg in heap.Segments)
            {
                if (seg.Start >= HighBitBoundary || seg.End > HighBitBoundary)
                {
                    sawHighBitSegment = true;

                    // Read a single byte at the start of the segment; this exercises the
                    // DataReader/minidump ULONG64 translation on a high-bit address.
                    byte[] buffer = new byte[1];
                    int read = dt.DataReader.Read(seg.Start, buffer);
                    Assert.Equal(1, read);
                }
            }

            Assert.True(sawHighBitSegment, "no GC segment straddled the 0x80000000 boundary");
        }

        [HighBitFact]
        public void Types_RootsAndObjectsEnumerateWithoutException()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int roots = heap.EnumerateRoots().Count();
            int objects = heap.EnumerateObjects().Count();

            Assert.True(roots > 0);
            Assert.True(objects > 0);
        }

        [HighBitFact]
        public void GCHandles_HighBitSmokeTest()
        {
            using DataTarget dt = TestTargets.GCHandles.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int count = heap.EnumerateObjects().Count();
            Assert.True(count > 0);
        }

        [HighBitFact]
        public void NestedException_HighBitSmokeTest()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int count = heap.EnumerateObjects().Count();
            Assert.True(count > 0);
        }

        [HighBitFact]
        public void ClrObjects_HighBitSmokeTest()
        {
            using DataTarget dt = TestTargets.ClrObjects.LoadFullDumpHighBit();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            int count = heap.EnumerateObjects().Count();
            Assert.True(count > 0);
        }
    }
}
