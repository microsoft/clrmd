// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class FinalizationQueueTests
    {
        [Fact]
        public void TestAllFinalizableObjects()
        {
            using (DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                Stats stats = GetStats(runtime.Heap, runtime.Heap.EnumerateFinalizableObjectAddresses());
                
                Assert.Equal(0, stats.A);
                Assert.Equal(13, stats.B);
                Assert.Equal(25, stats.C);
            }
        }

        [Fact]
        public void TestFinalizerQueueObjects()
        {
            using (DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                Stats stats = GetStats(runtime.Heap, runtime.EnumerateFinalizerQueueObjectAddresses());
                
                Assert.Equal(42, stats.A);
                Assert.Equal(0, stats.B);
                Assert.Equal(0, stats.C);
            }
        }
        
        private static Stats GetStats(ClrHeap heap, IEnumerable<ulong> addresses)
        {
            var stats = new Stats();
            foreach (var address in addresses)
            {
                var type = heap.GetObjectType(address);
                if (type.Name == "SampleA")
                    stats.A++;
                else if (type.Name == "SampleB")
                    stats.B++;
                else if (type.Name == "SampleC")
                    stats.C++;
            }

            return stats;
        }
        
        private class Stats
        {
            public int A;
            public int B;
            public int C;
        }
    }
}