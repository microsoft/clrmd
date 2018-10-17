using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Summary description for GCRootTests
    /// </summary>
    [TestClass]
    public class GCRootTests
    {
        [TestMethod]
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
            Assert.AreEqual(2, refs.Length);

            Assert.AreEqual(1, refs.Count(r => r.Type.Name == "SingleRef"));
            Assert.AreEqual(1, refs.Count(r => r.Type.Name == "TripleRef"));

            foreach (ClrObject obj in refs)
            {
                Assert.AreNotEqual(0, obj.Address);
                Assert.AreEqual(obj.Type.Heap.GetObjectType(obj.Address), obj.Type);
            }
        }

        [TestMethod]
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

                Assert.AreEqual("System.Object[]", obj.Type.Name);

                ClrObject[] refs = obj.EnumerateObjectReferences(false).ToArray();
                Assert.AreEqual(1, refs.Length);
                Assert.AreEqual("DoubleRef", refs[0].Type.Name);
            }
        }

        [TestMethod]
        public void ObjectSetAddRemove()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsFalse(hash.Contains(obj));
                    hash.Add(obj);
                    Assert.IsTrue(hash.Contains(obj));
                }

                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsTrue(hash.Contains(obj));
                    hash.Remove(obj);
                    Assert.IsFalse(hash.Contains(obj));
                }
            }
        }

        [TestMethod]
        public void ObjectSetTryAdd()
        {
            using (DataTarget dataTarget = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ObjectSet hash = new ObjectSet(heap);
                foreach (ulong obj in heap.EnumerateObjectAddresses())
                {
                    Assert.IsFalse(hash.Contains(obj));
                    Assert.IsTrue(hash.Add(obj));
                    Assert.IsTrue(hash.Contains(obj));
                    Assert.IsFalse(hash.Add(obj));
                    Assert.IsTrue(hash.Contains(obj));
                }

            }
        }
        [TestMethod]
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
                    Assert.Fail("Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }


        [TestMethod]
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
                    Assert.Fail("Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [TestMethod]
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
                    Assert.Fail("Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }


        [TestMethod]
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
                    Assert.Fail("Should have been cancelled!");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [TestMethod]
        public void GCStaticRoots()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                heap.StackwalkPolicy = ClrRootStackwalkPolicy.SkipStack;
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.IsFalse(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);

                gcroot.AllowParallelSearch = false;
                Assert.IsTrue(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);

                gcroot.AllowParallelSearch = true;
                Assert.IsTrue(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);
            }
        }

        private void GCStaticRootsImpl(GCRoot gcroot)
        {
            ulong target = gcroot.Heap.GetObjectsOfType("TargetType").Single();
            RootPath[] paths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();
            Assert.AreEqual(1, paths.Length);
            RootPath rootPath = paths[0];

            AssertPathIsCorrect(gcroot.Heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);
        }

        [TestMethod]
        public void GCRoots()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.IsFalse(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);
                
                gcroot.BuildCache(CancellationToken.None);

                gcroot.AllowParallelSearch = false;
                Assert.IsTrue(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);

                gcroot.AllowParallelSearch = true;
                Assert.IsTrue(gcroot.IsFullyCached);
                GCRootsImpl(gcroot);
            }
        }

        private void GCRootsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            ulong target = heap.GetObjectsOfType("TargetType").Single();
            RootPath[] rootPaths = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).ToArray();

            Assert.IsTrue(rootPaths.Length >= 2);

            foreach (RootPath rootPath in rootPaths)
                AssertPathIsCorrect(heap, rootPath.Path.ToArray(), rootPath.Path.First().Address, target);

            bool hasThread = false, hasStatic = false;
            foreach (RootPath rootPath in rootPaths)
            {
                if (rootPath.Root.Kind == GCRootKind.Pinning)
                    hasStatic = true;
                else if (rootPath.Root.Kind == GCRootKind.LocalVar)
                    hasThread = true;
            }

            Assert.IsTrue(hasThread);
            Assert.IsTrue(hasStatic);
        }

        [TestMethod]
        public void FindPath()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.IsFalse(gcroot.IsFullyCached);
                FindPathImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
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

        [TestMethod]
        public void FindAllPaths()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap);

                gcroot.ClearCache();
                Assert.IsFalse(gcroot.IsFullyCached);
                FindAllPathsImpl(gcroot);

                gcroot.BuildCache(CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
                FindAllPathsImpl(gcroot);
            }
        }

        private void FindAllPathsImpl(GCRoot gcroot)
        {
            ClrHeap heap = gcroot.Heap;
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);
            
            LinkedList<ClrObject>[] paths = gcroot.EnumerateAllPaths(source, target, false, CancellationToken.None).ToArray();

            // There are exactly three paths to the object in the test target
            Assert.AreEqual(3, paths.Length);

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
            Assert.IsNotNull(path);
            Assert.IsTrue(path.Length > 0);

            ClrObject first = path.First();
            Assert.AreEqual(source, first.Address);

            for (int i = 0; i < path.Length - 1; i++)
            {
                ClrObject curr = path[i];
                Assert.AreEqual(curr.Type, heap.GetObjectType(curr.Address));

                List<ulong> refs = new List<ulong>();
                curr.Type.EnumerateRefsOfObject(curr.Address, (obj, offs) => refs.Add(obj));

                ulong next = path[i + 1].Address;
                Assert.IsTrue(refs.Contains(next));
            }

            ClrObject last = path.Last();
            Assert.AreEqual(last.Type, heap.GetObjectType(last.Address));
            Assert.AreEqual(target, last.Address);
        }
    }
}
