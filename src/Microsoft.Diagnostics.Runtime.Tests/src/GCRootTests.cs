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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var obj = heap.GetObjectsOfType("DoubleRef").Single();
                var type = heap.GetObjectType(obj);

                var refs = type.EnumerateObjectReferences(obj).ToArray();
                ValidateRefs(refs);
            }
        }

        private void ValidateRefs(ClrObject[] refs)
        {
            // Should contain one SingleRef and one TripleRef object.
            Assert.Equal(2, refs.Length);

            Assert.Equal(1, refs.Count(r => r.Type.Name == "SingleRef"));
            Assert.Equal(1, refs.Count(r => r.Type.Name == "TripleRef"));

            foreach (var obj in refs)
            {
                Assert.NotEqual(0ul, obj.Address);
                Assert.Equal(obj.Type.Heap.GetObjectType(obj.Address), obj.Type);
            }
        }

        [Fact]
        public void EnumerateGCRefsArray()
        {
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var module = heap.Runtime.GetMainModule();
                var mainType = module.GetTypeByName("GCRootTarget");

                var obj = mainType.GetStaticObjectValue("TheRoot");
                obj = obj.GetObjectField("Item1");

                Assert.Equal("System.Object[]", obj.Type.Name);

                var refs = obj.EnumerateObjectReferences(false).ToArray();
                Assert.Single(refs);
                Assert.Equal("DoubleRef", refs[0].Type.Name);
            }
        }

        [Fact]
        public void ObjectSetAddRemove()
        {
            using (var dataTarget = TestTargets.Types.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var hash = new ObjectSet(heap);
                foreach (var obj in heap.EnumerateObjectAddresses())
                {
                    Assert.False(hash.Contains(obj));
                    hash.Add(obj);
                    Assert.True(hash.Contains(obj));
                }

                foreach (var obj in heap.EnumerateObjectAddresses())
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
            using (var dataTarget = TestTargets.Types.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;

                var hash = new ObjectSet(heap);
                foreach (var obj in heap.EnumerateObjectAddresses())
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;

                var gcroot = new GCRoot(heap);
                var target = gcroot.Heap.GetObjectsOfType("TargetType").Single();

                var source = new CancellationTokenSource();
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                var gcroot = new GCRoot(runtime.Heap);

                var target = gcroot.Heap.GetObjectsOfType("TargetType").Single();

                var source = new CancellationTokenSource();
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                var gcroot = new GCRoot(runtime.Heap);

                var cancelSource = new CancellationTokenSource();
                cancelSource.Cancel();

                GetKnownSourceAndTarget(runtime.Heap, out var source, out var target);
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                var gcroot = new GCRoot(runtime.Heap);

                var cancelSource = new CancellationTokenSource();
                cancelSource.Cancel();

                GetKnownSourceAndTarget(runtime.Heap, out var source, out var target);
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                var gcroot = new GCRoot(runtime.Heap);

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
            var target = gcroot.Heap.GetObjectsOfType("TargetType").Single();
            var paths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();
            Assert.Single(paths);
            var rootPath = paths[0];

            AssertPathIsCorrect(gcroot.Heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);
        }

        [Fact]
        public void GCRoots()
        {
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var gcroot = new GCRoot(runtime.Heap);

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
            var heap = gcroot.Heap;
            var target = heap.GetObjectsOfType("TargetType").Single();
            var rootPaths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();

            Assert.True(rootPaths.Length >= 2);

            foreach (var rootPath in rootPaths)
                AssertPathIsCorrect(heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);

            bool hasThread = false, hasStatic = false;
            foreach (var rootPath in rootPaths)
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
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var gcroot = new GCRoot(runtime.Heap);

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
            var heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out var source, out var target);

            var path = gcroot.FindSinglePath(source, target, CancellationToken.None);

            AssertPathIsCorrect(heap, path.ToArray(), source, target);
        }

        [Fact]
        public void FindAllPaths()
        {
            using (var dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                var gcroot = new GCRoot(runtime.Heap);

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
            var heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out var source, out var target);

            var paths = gcroot.EnumerateAllPaths(source, target, false, CancellationToken.None).ToArray();

            // There are exactly three paths to the object in the test target
            Assert.Equal(3, paths.Length);

            foreach (var path in paths)
                AssertPathIsCorrect(heap, path.ToArray(), source, target);
        }

        private static void GetKnownSourceAndTarget(ClrHeap heap, out ulong source, out ulong target)
        {
            var module = heap.Runtime.GetMainModule();
            var mainType = module.GetTypeByName("GCRootTarget");

            source = mainType.GetStaticObjectValue("TheRoot").Address;
            target = heap.GetObjectsOfType("TargetType").Single();
        }

        private void AssertPathIsCorrect(ClrHeap heap, ClrObject[] path, ulong source, ulong target)
        {
            Assert.NotNull(path);
            Assert.True(path.Length > 0);

            var first = path.First();
            Assert.Equal(source, first.Address);

            for (var i = 0; i < path.Length - 1; i++)
            {
                var curr = path[i];
                Assert.Equal(curr.Type, heap.GetObjectType(curr.Address));

                var refs = new List<ulong>();
                curr.Type.EnumerateRefsOfObject(curr.Address, (obj, offs) => refs.Add(obj));

                var next = path[i + 1].Address;
                Assert.Contains(next, refs);
            }

            var last = path.Last();
            Assert.Equal(last.Type, heap.GetObjectType(last.Address));
            Assert.Equal(target, last.Address);
        }
    }
}