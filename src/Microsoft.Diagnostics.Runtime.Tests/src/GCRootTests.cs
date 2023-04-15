// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class GCRootTests
    {
        [Fact]
        public void TestEnumerateRefsWithFieldsArrayFieldValues()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
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

        [Fact]
        public void TestEnumerateRefsWithFields()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
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

        [Fact]
        public void EnumerateGCRefs()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
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

        [Fact]
        public void EnumerateGCRefsArray()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
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

        [Fact]
        public void GCRoots()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("TargetType").Single();
            ContainsPathsToTarget(heap, 0, target);
        }

        [Fact]
        public void GCRootsPredicate()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ContainsPathsToTarget(heap, 0, (obj) => obj.Type?.Name == "TargetType");
        }

        [Fact]
        public void GCRootsDirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("DirectTarget").Single();
            GCRoot gcroot = new(heap, new ulong[] { target });
            ContainsPathsToTarget(heap, 0, target);
        }

        [Fact]
        public void GCRootsIndirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ulong target = heap.GetObjectsOfType("IndirectTarget").Single();
            ContainsPathsToTarget(heap, 0, target);
        }

        [Fact]
        public void FindAllPaths()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);
            int totalPath = ContainsPathsToTarget(heap, source, target);
            Assert.True(totalPath >= 3);
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
                if (item.Path.Object == source)
                    source = 0;

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
