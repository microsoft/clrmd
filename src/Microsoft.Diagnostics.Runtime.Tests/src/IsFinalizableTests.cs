// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class IsFinalizableTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsFinalizable_DoesNotOverReport(bool singleFile)
        {
            using DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // Types known to NOT have a finalizer.  Pre-fix, IsFinalizable was implemented by
            // walking Methods looking for any virtual method named "Finalize" — which always
            // matches the inherited slot from System.Object.Finalize, causing every type to
            // report true.  These assertions guard against regression.
            Assert.False(heap.StringType.IsFinalizable, "System.String should not be finalizable.");
            Assert.False(heap.ObjectType.IsFinalizable, "System.Object should not be finalizable.");

            ClrType? int32 = runtime.BaseClassLibrary.GetTypeByName("System.Int32");
            Assert.NotNull(int32);
            Assert.False(int32!.IsFinalizable, "System.Int32 should not be finalizable.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsFinalizable_TrueForUserFinalizers(bool singleFile)
        {
            using DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // The FinalizationQueue test target defines SampleA/B/C and DieHard, all with ~Finalize.
            HashSet<string> finalizableNames = new(heap.EnumerateObjects()
                                                      .Where(o => o.Type is not null && o.Type.IsFinalizable)
                                                      .Select(o => o.Type!.Name!));

            Assert.Contains("SampleA", finalizableNames);
            Assert.Contains("SampleB", finalizableNames);
            Assert.Contains("SampleC", finalizableNames);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsFinalizable_AgreesWithFinalizerQueue(bool singleFile)
        {
            using DataTarget dt = TestTargets.FinalizationQueue.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // Every distinct type that actually has objects on the FQ must report IsFinalizable.
            // (The reverse is not true: a type can have a finalizer with no live FQ instances.)
            foreach (ClrObject obj in heap.EnumerateFinalizableObjects())
            {
                if (obj.Type is null)
                    continue;

                Assert.True(obj.Type.IsFinalizable,
                    $"Type {obj.Type.Name} has objects on the finalizer queue but IsFinalizable is false.");
            }
        }
    }
}
