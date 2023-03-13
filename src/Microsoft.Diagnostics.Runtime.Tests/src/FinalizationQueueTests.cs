﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            using DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            Stats stats = GetStats(runtime.Heap.EnumerateFinalizableObjects());

            Assert.Equal(0, stats.A);
            Assert.Equal(13, stats.B);
            Assert.Equal(25, stats.C);
        }

        [Fact]
        public void TestFinalizerQueueObjects()
        {
            using DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            Stats stats = GetStats(runtime.Heap.EnumerateFinalizerRoots().Select(r => r.Object));

            Assert.Equal(42, stats.A);
            Assert.Equal(0, stats.B);
            Assert.Equal(0, stats.C);
        }

        private static Stats GetStats(IEnumerable<ClrObject> objs)
        {
            Stats stats = new();
            foreach (ClrObject obj in objs)
            {
                ClrType type = obj.Type;
                if (type.Name == "SampleA")
                    stats.A++;
                else if (type.Name == "SampleB")
                    stats.B++;
                else if (type.Name == "SampleC")
                    stats.C++;
            }

            return stats;
        }

        private sealed class Stats
        {
            public int A;
            public int B;
            public int C;
        }
    }
}
