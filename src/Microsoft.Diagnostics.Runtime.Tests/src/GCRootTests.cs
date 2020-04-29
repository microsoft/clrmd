// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
            Assert.Single(refs);
            Assert.Equal("DoubleRef", refs[0].Type.Name);
        }

        [Fact]
        public void ObjectSetAddRemove()
        {
            using DataTarget dataTarget = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ObjectSet hash = new ObjectSet(heap);
            foreach (ulong obj in heap.EnumerateObjects())
            {
                Assert.False(hash.Contains(obj));
                hash.Add(obj);
                Assert.True(hash.Contains(obj));
            }

            foreach (ulong obj in heap.EnumerateObjects())
            {
                Assert.True(hash.Contains(obj));
                hash.Remove(obj);
                Assert.False(hash.Contains(obj));
            }
        }

        [Fact]
        public void ObjectSetTryAdd()
        {
            using DataTarget dataTarget = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ObjectSet hash = new ObjectSet(heap);
            foreach (ulong obj in heap.EnumerateObjects())
            {
                Assert.False(hash.Contains(obj));
                Assert.True(hash.Add(obj));
                Assert.True(hash.Contains(obj));
                Assert.False(hash.Add(obj));
                Assert.True(hash.Contains(obj));
            }
        }

        [Fact]
        public void FindSinglePathCancel()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            GCRoot gcroot = new GCRoot(runtime.Heap);

            CancellationTokenSource cancelSource = new CancellationTokenSource();
            cancelSource.Cancel();

            GetKnownSourceAndTarget(runtime.Heap, out ulong source, out ulong target);
            try
            {
                gcroot.FindSinglePath(source, target, cancelSource.Token);
                Assert.True(false, "Should have been cancelled!");
            }
            catch (OperationCanceledException)
            {
            }
        }

        [Fact]
        public void EnumerateAllPathshCancel()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            GCRoot gcroot = new GCRoot(runtime.Heap);

            CancellationTokenSource cancelSource = new CancellationTokenSource();
            cancelSource.Cancel();

            GetKnownSourceAndTarget(runtime.Heap, out ulong source, out ulong target);
            try
            {
                gcroot.EnumerateAllPaths(source, target, false, cancelSource.Token).ToArray();
                Assert.True(false, "Should have been cancelled!");
            }
            catch (OperationCanceledException)
            {
            }
        }

        [Fact]
        public void GCRoots()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            GCRoot gcroot = new GCRoot(heap);
            GCRootsImpl(gcroot);
        }

        private void GCRootsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            ulong target = heap.GetObjectsOfType("TargetType").Single();

            GCRootsImpl(gcroot, heap, target, parallelism: 1, unique: false);
            GCRootsImpl(gcroot, heap, target, parallelism: 16, unique: false);

            GCRootsImpl(gcroot, heap, target, parallelism: 1, unique: true);
            GCRootsImpl(gcroot, heap, target, parallelism: 16, unique: true);
        }

        private void GCRootsImpl(GCRoot gcroot, ClrHeap heap, ulong target, int parallelism, bool unique)
        {
            GCRootPath[] rootPaths = gcroot.EnumerateGCRoots(target, unique: unique, parallelism, CancellationToken.None).ToArray();

            // In the case where we say we only want unique rooting chains AND we want to look in parallel,
            // we cannot guarantee that we will pick the static roots over the stack ones.  Hence we don't
            // ensure that the static variable is enumerated when unique == true.
            CheckRootPaths(heap, target, rootPaths, mustContainStatic: !unique);
        }

        private void CheckRootPaths(ClrHeap heap, ulong target, GCRootPath[] rootPaths, bool mustContainStatic)
        {
            Assert.True(rootPaths.Length >= 2);

            foreach (GCRootPath rootPath in rootPaths)
                AssertPathIsCorrect(heap, rootPath.Path, rootPath.Path.First().Address, target);

            bool hasThread = false, hasStatic = false;

            foreach (GCRootPath rootPath in rootPaths)
            {
                if (rootPath.Root.RootKind == ClrRootKind.PinnedHandle)
                    hasStatic = true;
                else if (rootPath.Root.RootKind == ClrRootKind.Stack)
                    hasThread = true;
            }

            Assert.True(hasThread);

            if (mustContainStatic)
                Assert.True(hasStatic);
        }

        [Fact]
        public void GCRootsDirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();

            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            GCRoot gcroot = new GCRoot(heap);

            ulong target = heap.GetObjectsOfType("DirectTarget").Single();

            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: true, 1, CancellationToken.None).Count());
            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: false, 16, CancellationToken.None).Count());

            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: true, 16, CancellationToken.None).Count());
            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: false, 1, CancellationToken.None).Count());
        }

        [Fact]
        public void GCRootsIndirectHandles()
        {
            using DataTarget dataTarget = TestTargets.GCRoot2.LoadFullDump();

            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            GCRoot gcroot = new GCRoot(heap);

            ulong target = heap.GetObjectsOfType("IndirectTarget").Single();

            _ = Assert.Single(gcroot.EnumerateGCRoots(target, unique: true, 8, CancellationToken.None));
            GCRootPath path = Assert.Single(gcroot.EnumerateGCRoots(target, unique: true, 1, CancellationToken.None));

            var paths = gcroot.EnumerateGCRoots(target, unique: false, 1, CancellationToken.None).ToArray();
            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: false, 1, CancellationToken.None).Count());
            Assert.Equal(2, gcroot.EnumerateGCRoots(target, unique: false, 8, CancellationToken.None).Count());
        }

        [Fact]
        public void FindSinglePath()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            GCRoot gcroot = new GCRoot(runtime.Heap);

            FindSinglePathImpl(gcroot);
        }

        private void FindSinglePathImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            LinkedList<ClrObject> path = gcroot.FindSinglePath(source, target, CancellationToken.None);

            AssertPathIsCorrect(heap, path.ToImmutableArray(), source, target);
        }

        [Fact]
        public void FindAllPaths()
        {
            using DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump();
            using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            GCRoot gcroot = new GCRoot(runtime.Heap);

            FindAllPathsImpl(gcroot);
        }

        private void FindAllPathsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            LinkedList<ClrObject>[] paths = gcroot.EnumerateAllPaths(source, target, false, CancellationToken.None).ToArray();

            // There are exactly three paths to the object in the test target
            Assert.Equal(3, paths.Length);

            foreach (LinkedList<ClrObject> path in paths)
                AssertPathIsCorrect(heap, path.ToImmutableArray(), source, target);
        }

        private static void GetKnownSourceAndTarget(ClrHeap heap, out ulong source, out ulong target)
        {
            ClrModule module = heap.Runtime.GetMainModule();
            ClrType mainType = module.GetTypeByName("GCRootTarget");

            source = mainType.GetStaticObjectValue("TheRoot").Address;
            target = heap.GetObjectsOfType("TargetType").Single();
        }

        private void AssertPathIsCorrect(ClrHeap heap, ImmutableArray<ClrObject> path, ulong source, ulong target)
        {
            Assert.NotEqual(default, path);
            Assert.True(path.Length > 0);

            ClrObject first = path.First();
            Assert.Equal(source, first.Address);

            for (int i = 0; i < path.Length - 1; i++)
            {
                ClrObject curr = path[i];
                Assert.Equal(curr.Type, heap.GetObjectType(curr.Address));

                IEnumerable<ClrObject> refs = curr.EnumerateReferences();

                ClrObject next = path[i + 1];
                Assert.Contains(next, refs);
            }

            ClrObject last = path.Last();
            Assert.Equal(last.Type, heap.GetObjectType(last.Address));
            Assert.Equal(target, last.Address);
        }
    }
}
