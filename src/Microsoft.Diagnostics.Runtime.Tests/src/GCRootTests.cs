// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class GCRootTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestEnumerateRefsWithFieldsArrayFieldValues(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                foreach (ClrReference reference in obj.EnumerateReferencesWithFields(carefully: false, considerDependantHandles: false))
                {
                    if (obj.IsArray)
                    {
                        // Ensure we didn't try to set .Field if it's an array reference
                        Assert.True(reference.IsArrayElement);
                        Assert.False(reference.IsDependentHandle);
                        Assert.False(reference.IsField);
                        Assert.Null(reference.Field);
                    }
                    else
                    {
                        // Ensure that we always have a .Field when it's a field reference
                        Assert.False(reference.IsArrayElement);
                        Assert.False(reference.IsDependentHandle);
                        Assert.True(reference.IsField);
                        Assert.NotNull(reference.Field);
                    }
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestEnumerateRefsWithFields(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrObject singleRef = FindSingleRefPointingToTarget(heap);
            ClrReference fieldRef = singleRef.EnumerateReferencesWithFields(considerDependantHandles: true).Single();

            Assert.True(fieldRef.IsDependentHandle);
            Assert.False(fieldRef.IsField);

            singleRef = FindSingleRefPointingToType(heap, "TripleRef");
            fieldRef = singleRef.EnumerateReferencesWithFields(considerDependantHandles: false).Single();

            Assert.False(fieldRef.IsDependentHandle);
            Assert.True(fieldRef.IsField);
            Assert.NotNull(fieldRef.Field);
            Assert.Equal("Item1", fieldRef.Field.Name);
        }

        private ClrObject FindSingleRefPointingToTarget(ClrHeap heap)
        {
            foreach (ClrObject obj in heap.EnumerateObjects().Where(o => o.Type.Name == "SingleRef"))
            {
                foreach (ClrObject reference in obj.EnumerateReferences(considerDependantHandles: true))
                    if (reference.Type.Name == "TargetType")
                        return obj;
            }

            throw new InvalidOperationException("Did not find a SingleRef pointing to a TargetType");
        }

        private ClrObject FindSingleRefPointingToType(ClrHeap heap, string targetTypeName)
        {
            foreach (ClrObject obj in heap.EnumerateObjects().Where(o => o.Type.Name == "SingleRef"))
            {
                ClrObject item1 = obj.ReadObjectField("Item1");
                if (item1.Type?.Name == targetTypeName)
                    return obj;
            }

            throw new InvalidOperationException($"Did not find a SingleRef pointing to a {targetTypeName}.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateGCRefs(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrObject doubleRef = heap.GetObjectsOfType("DoubleRef").Single();
            Assert.False(doubleRef.IsNull);

            ClrObject[] refs = doubleRef.EnumerateReferences().ToArray();

            // Should contain one SingleRef and one TripleRef object.
            Assert.Equal(2, refs.Length);

            Assert.Equal(1, refs.Count(r => r.Type.Name == "SingleRef"));
            Assert.Equal(1, refs.Count(r => r.Type.Name == "TripleRef"));

            foreach (ClrObject obj in refs)
            {
                Assert.NotEqual(0ul, obj.Address);
                Assert.Equal(obj.Type.Heap.GetObjectType(obj.Address), obj.Type);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateGCRefsArray(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrModule module = heap.Runtime.GetMainModule();
            ClrType mainType = module.GetTypeByName("GCRootTarget");

            ClrObject obj = mainType.GetStaticObjectValue("TheRoot");
            obj = obj.ReadObjectField("Item1");

            Assert.Equal("System.Object[]", obj.Type.Name);

            ClrObject[] refs = obj.EnumerateReferences(false).ToArray();
            obj = Assert.Single(refs);
            Assert.Equal("DoubleRef", obj.Type.Name);
        }

        [WindowsOrNet11Fact]
        public void GCRoots()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("TargetType").Single();
            ContainsPathsToTarget(heap, 0, target);
        }

        [WindowsOrNet11Fact]
        public void GCRootsPredicate()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ContainsPathsToTarget(heap, 0, (obj) => obj.Type?.Name == "TargetType");
        }

        [WindowsOrNet11Fact]
        public void GCRootsDirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("DirectTarget").Single();
            GCRoot gcroot = new(heap, new ulong[] { target });
            ContainsPathsToTarget(heap, 0, target);
        }

        [WindowsOrNet11Fact]
        public void GCRootsIndirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("IndirectTarget").Single();
            ContainsPathsToTarget(heap, 0, target);
        }

        [WindowsOrNet11Fact]
        public void FindAllPaths()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);
            int totalPath = ContainsPathsToTarget(heap, source, target);
            Assert.True(totalPath >= 3, $"Expected at least 3 paths, got {totalPath}");
        }

        /// <summary>
        /// Regression test: verifies that a shared GCRoot instance finds the same set of
        /// paths as individual fresh instances.  Before the fix, successful searches left
        /// unwalked children in _seen, blocking later FindPathFrom calls.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindPathFromDoesNotPoisonSeenAcrossCalls(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("TargetType").Single();

            // Collect test-graph objects that can individually reach the target.
            string[] relevantTypes = { "TripleRef", "DoubleRef", "SingleRef" };
            List<ulong> candidates = heap.EnumerateObjects()
                .Where(o => o.IsValid && o.Type is not null && relevantTypes.Contains(o.Type.Name))
                .Select(o => o.Address)
                .ToList();

            Assert.True(candidates.Count >= 3, $"Expected at least 3 test objects, found {candidates.Count}");

            // Ground truth: each object gets a fresh GCRoot (clean _seen).
            List<ulong> objectsWithPaths = new();
            foreach (ulong addr in candidates)
            {
                GCRoot fresh = new(heap, new ulong[] { target });
                var path = fresh.FindPathFrom(heap.GetObject(addr));
                if (path is not null)
                    objectsWithPaths.Add(addr);
            }

            Assert.NotEmpty(objectsWithPaths);

            // Shared GCRoot: every object that found a path individually
            // must also find one when _seen state is shared across calls.
            GCRoot shared = new(heap, new ulong[] { target });
            foreach (ulong addr in objectsWithPaths)
            {
                var path = shared.FindPathFrom(heap.GetObject(addr));
                Assert.True(path is not null,
                    $"FindPathFrom({addr:x}) returned null with shared GCRoot but succeeded with a fresh one");
                VerifyPath(heap, (obj) => target == obj, path);
            }
        }

        /// <summary>
        /// Stress test: repeats the shared-vs-fresh GCRoot comparison many times
        /// to catch order-dependent flakiness.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindPathFromSeenStressTest(bool singleFile)
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump(singleFile);
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("TargetType").Single();

            string[] relevantTypes = { "TripleRef", "DoubleRef", "SingleRef" };
            List<ulong> candidates = heap.EnumerateObjects()
                .Where(o => o.IsValid && o.Type is not null && relevantTypes.Contains(o.Type.Name))
                .Select(o => o.Address)
                .ToList();

            // Build ground truth once.
            List<ulong> objectsWithPaths = new();
            foreach (ulong addr in candidates)
            {
                GCRoot fresh = new(heap, new ulong[] { target });
                if (fresh.FindPathFrom(heap.GetObject(addr)) is not null)
                    objectsWithPaths.Add(addr);
            }

            Assert.NotEmpty(objectsWithPaths);

            // Run 100 iterations with different traversal orders.
            Random rng = new(42);
            for (int iteration = 0; iteration < 100; iteration++)
            {
                // Shuffle to vary which search poisons _seen first.
                List<ulong> shuffled = objectsWithPaths.OrderBy(_ => rng.Next()).ToList();

                GCRoot shared = new(heap, new ulong[] { target });
                foreach (ulong addr in shuffled)
                {
                    var path = shared.FindPathFrom(heap.GetObject(addr));
                    Assert.True(path is not null,
                        $"Iteration {iteration}: FindPathFrom({addr:x}) returned null with shared GCRoot");
                }
            }
        }

        private static void GetKnownSourceAndTarget(ClrHeap heap, out ulong source, out ulong target)
        {
            ClrModule module = heap.Runtime.GetMainModule();
            ClrType mainType = module.GetTypeByName("GCRootTarget");

            source = mainType.GetStaticObjectValue("TheRoot").Address;
            target = heap.GetObjectsOfType("TargetType").Single();
        }

        private int ContainsPathsToTarget(ClrHeap heap, ulong source, Predicate<ClrObject> matches)
        {
            GCRoot gcroot = new(heap, matches);
            return ContainsPathsToTarget(heap, source, gcroot, matches);
        }

        private int ContainsPathsToTarget(ClrHeap heap, ulong source, ulong target)
        {
            GCRoot gcroot = new(heap, new ulong[] { target });
            return ContainsPathsToTarget(heap, source, gcroot, (obj) => target == obj);
        }

        private static int ContainsPathsToTarget(ClrHeap heap, ulong source, GCRoot gcroot, Predicate<ClrObject> matches)
        {
            int count = 0;

            foreach (var item in gcroot.EnumerateRootPaths())
            {
                // Check if source appears anywhere in the chain, not just at
                // the start.  On .NET 10+ statics are stored in an Object[]
                // strong handle, so TheRoot may be one level deeper than the
                // actual GC root.
                for (GCRoot.ChainLink? link = item.Path; link is not null; link = link.Next)
                {
                    if (link.Object == source)
                    {
                        source = 0;
                        break;
                    }
                }

                Assert.Equal(item.Root.Object.Address, item.Path.Object);
                GCRoot.ChainLink curr = item.Path;
                VerifyPath(heap, matches, curr);
                count++;
            }

            // Ensure we found the source, or source was 0 to begin with.
            Assert.Equal(0ul, source);
            Assert.True(count != 0);
            return count;
        }

        private static void VerifyPath(ClrHeap heap, Predicate<ClrObject> matches, GCRoot.ChainLink curr)
        {
            while (curr.Next is not null)
            {
                ClrObject obj = heap.GetObject(curr.Object);
                Assert.True(obj.IsValid);

                Assert.Contains(curr.Next.Object, obj.EnumerateReferenceAddresses());

                curr = curr.Next;
            }

            ClrObject currObject = heap.GetObject(curr.Object);
            Assert.True(matches(currObject));
        }
    }
}
