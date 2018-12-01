// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class GCRootTests
    {
        [Fact]
        public void EnumerateGCRefs()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ulong obj = heap.GetObjectsOfType("DoubleRef").Single();
                ClrType type = heap.GetObjectType(obj);

                ClrObject[] refs = type.EnumerateObjectReferences(obj).ToArray();
                ValidateRefs(refs);
            }
        }

        private void ValidateRefs(ClrObject[] refs)
        {
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
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrModule module = heap.Runtime.GetMainModule();
                ClrType mainType = module.GetTypeByName("GCRootTarget");

                ClrObject obj = mainType.GetStaticObjectValue("TheRoot");
                obj = obj.GetObjectField("Item1");

                Assert.Equal("System.Object[]", obj.Type.Name);

                ClrObject[] refs = obj.EnumerateObjectReferences(false).ToArray();
                Assert.Single(refs);
                Assert.Equal("DoubleRef", refs[0].Type.Name);
            }
        }

        [Fact]
        public void ObjectSetAddRemove()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.False(hash.Contains(obj));
                    hash.Add(obj);
                    Assert.True(hash.Contains(obj));
                }

                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.True(hash.Contains(obj));
                    hash.Remove(obj);
                    Assert.False(hash.Contains(obj));
                }
            }
        }

        [Fact]
        public void ObjectSetTryAdd()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.False(hash.Contains(obj));
                    Assert.True(hash.Add(obj));
                    Assert.True(hash.Contains(obj));
                    Assert.False(hash.Add(obj));
                    Assert.True(hash.Contains(obj));
                }
            }
        }

        [Fact]
        public void BuildCacheCancel()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;

                GCRoot gcroot = new GCRoot(heap);
                ulong target = gcroot.Heap.GetObjectsOfType("TargetType").Single();

                CancellationTokenSource source = new CancellationTokenSource();
                source.Cancel();

                try
                {
                    gcroot.BuildCache(source.Token);
                    Assert.True(false, "Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [Fact]
        public void EnumerateGCRootsCancel()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                GCRoot gcroot = new GCRoot(runtime.Heap);

                ulong target = gcroot.Heap.GetObjectsOfType("TargetType").Single();

                CancellationTokenSource source = new CancellationTokenSource();
                source.Cancel();

                try
                {
                    gcroot.EnumerateGCRoots(target, false, source.Token).ToArray();
                    Assert.True(false, "Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [Fact]
        public void FindSinglePathCancel()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
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
        }

        [Fact]
        public void EnumerateAllPathshCancel()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
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
        }

        [Fact]
        public void GCStaticRoots()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.False(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);

                gcroot.AllowParallelSearch = false;
                Assert.True(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);

                gcroot.AllowParallelSearch = true;
                Assert.True(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);
            }
        }

        private void GCStaticRootsImpl(GCRoot gcroot)
        {
            ulong target = gcroot.Heap.GetObjectsOfType("TargetType").Single();
            GCRootPath[] paths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();
            Assert.Single(paths);
            GCRootPath rootPath = paths[0];

            AssertPathIsCorrect(gcroot.Heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);
        }

        [Fact]
        public void GCRoots()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.False(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);

                gcroot.AllowParallelSearch = false;
                Assert.True(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);

                gcroot.AllowParallelSearch = true;
                Assert.True(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);
            }
        }

        private void GCRootsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            ulong target = heap.GetObjectsOfType("TargetType").Single();
            GCRootPath[] rootPaths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();

            Assert.True(rootPaths.Length >= 2);

            foreach (GCRootPath rootPath in rootPaths)
                AssertPathIsCorrect(heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);

            bool hasThread = false, hasStatic = false;
            foreach (GCRootPath rootPath in rootPaths)
            {
                if (rootPath.Root.Kind == GCRootKind.Pinning)
                    hasStatic = true;
                else if (rootPath.Root.Kind == GCRootKind.LocalVar)
                    hasThread = true;
            }

            Assert.True(hasThread);
            Assert.True(hasStatic);
        }

        [Fact]
        public void FindPath()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.False(gcroot.IsFullyCached);
                FindPathImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);
                Assert.True(gcroot.IsFullyCached);
                FindPathImpl(gcroot);
            }
        }

        private void FindPathImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            LinkedList<ClrObject> path = gcroot.FindSinglePath(source, target, CancellationToken.None);

            AssertPathIsCorrect(heap, path.ToArray(), source, target);
        }

        [Fact]
        public void FindAllPaths()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.False(gcroot.IsFullyCached);
                FindAllPathsImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);
                Assert.True(gcroot.IsFullyCached);
                FindAllPathsImpl(gcroot);
            }
        }

        private void FindAllPathsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            LinkedList<ClrObject>[] paths = gcroot.EnumerateAllPaths(source, target, false, CancellationToken.None).ToArray();

            // There are exactly three paths to the object in the test target
            Assert.Equal(3, paths.Length);

            foreach (LinkedList<ClrObject> path in paths)
                AssertPathIsCorrect(heap, path.ToArray(), source, target);
        }

        private static void GetKnownSourceAndTarget(ClrHeap heap, out ulong source, out ulong target)
        {
            ClrModule module = heap.Runtime.GetMainModule();
            ClrType mainType = module.GetTypeByName("GCRootTarget");

            source = mainType.GetStaticObjectValue("TheRoot").Address;
            target = heap.GetObjectsOfType("TargetType").Single();
        }

        private void AssertPathIsCorrect(ClrHeap heap, ClrObject[] path, ulong source, ulong target)
        {
            Assert.NotNull(path);
            Assert.True(path.Length > 0);

            ClrObject first = path.First();
            Assert.Equal(source, first.Address);

            for (int i = 0; i < path.Length - 1; i++)
            {
                ClrObject curr = path[i];
                Assert.Equal(curr.Type, heap.GetObjectType(curr.Address));

                List<ulong> refs = new List<ulong>();
                curr.Type.EnumerateRefsOfObject(curr.Address, (obj, offs) => refs.Add(obj));

                ulong next = path[i + 1].Address;
                Assert.Contains(next, refs);
            }

            ClrObject last = path.Last();
            Assert.Equal(last.Type, heap.GetObjectType(last.Address));
            Assert.Equal(target, last.Address);
        }
    }
}