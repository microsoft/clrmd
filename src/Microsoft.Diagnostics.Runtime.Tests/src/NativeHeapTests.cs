// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class NativeHeapTests
    {
        [Fact]
        public void TestHeapKinds()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrNativeHeapInfo[] heaps = runtime.EnumerateClrNativeHeaps().ToArray();
            Assert.Contains(NativeHeapKind.LoaderCodeHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.StubHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.HighFrequencyHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.LowFrequencyHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.IndirectionCellHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.LookupHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.ResolveHeap, heaps.Select(r => r.Kind));
            Assert.Contains(NativeHeapKind.DispatchHeap, heaps.Select(r => r.Kind));
        }

        [Fact]
        public void TestNativeHeapLengths()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            foreach (ClrNativeHeapInfo heap in runtime.EnumerateClrNativeHeaps())
                Assert.NotEqual(0ul, heap.MemoryRange.Length);
        }
    }
}
