// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                int targetObjectsCount = 0;

                foreach (ulong address in runtime.Heap.EnumerateFinalizableObjectAddresses())
                {
                    ClrType type = runtime.Heap.GetObjectType(address);
                    if (type.Name == "DieFastA")
                        targetObjectsCount++;
                }

                Assert.Equal(42, targetObjectsCount);
            }
        }

        [Fact]
        public void TestFinalizerQueueObjects()
        {
            using (DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                int targetObjectsCount = 0;

                foreach (ulong address in runtime.EnumerateFinalizerQueueObjectAddresses())
                {
                    ClrType type = runtime.Heap.GetObjectType(address);
                    if (type.Name == "DieFastB")
                        targetObjectsCount++;
                }

                Assert.Equal(13, targetObjectsCount);
            }
        }
    }
}