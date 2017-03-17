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
        public void GCStaticRoots()
        {
            using (DataTarget dataTarget = TestTargets.GCRoot.LoadFullDump())
            {
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                GCRoot gcroot = new GCRoot(runtime.Heap) { StackwalkPolicy = GCRootStackWalkPolicy.SkipStack };

                gcroot.ClearCache();
                Assert.IsFalse(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);

                gcroot.BuildCache(int.MaxValue, CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
                GCStaticRootsImpl(gcroot);
            }
        }

        private void GCStaticRootsImpl(GCRoot gcroot)
        {
            ulong target = gcroot.Heap.GetObjectsOfType("TargetType").Single();
            RootPath rootPath = gcroot.EnumerateGCRoots(target, false, CancellationToken.None).Single();

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
                GCRootsImpl(gcroot.Heap);

                gcroot.BuildCache(int.MaxValue, CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
                GCRootsImpl(gcroot.Heap);
            }
        }

        private void GCRootsImpl(ClrHeap heap)
        {
            GCRoot gcroot = new GCRoot(heap);
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
                FindPathImpl(gcroot.Heap);

                gcroot.BuildCache(int.MaxValue, CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
                FindPathImpl(gcroot.Heap);
            }
        }

        private void FindPathImpl(ClrHeap heap)
        {
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            GCRoot gcroot = new GCRoot(heap);
            LinkedList<ClrObject> path = gcroot.FindSinglePath(source, target);

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
                FindAllPathsImpl(gcroot.Heap);

                gcroot.BuildCache(int.MaxValue, CancellationToken.None);
                Assert.IsTrue(gcroot.IsFullyCached);
                FindAllPathsImpl(gcroot.Heap);
            }
        }

        private void FindAllPathsImpl(ClrHeap heap)
        {
            GetKnownSourceAndTarget(heap, out ulong source, out ulong target);

            GCRoot gcroot = new GCRoot(heap);
            LinkedList<ClrObject>[] paths = gcroot.EnumerateAllPaths(source, target, false).ToArray();

            // There are exactly three paths to the object in the test target
            Assert.AreEqual(3, paths.Length);

            foreach (LinkedList<ClrObject> path in paths)
                AssertPathIsCorrect(heap, path.ToArray(), source, target);
        }

        private static void GetKnownSourceAndTarget(ClrHeap heap, out ulong source, out ulong target)
        {
            ClrModule module = heap.Runtime.GetMainModule();
            ClrType mainType = module.GetTypeByName("GCRootTarget");

            source = mainType.GetStaticObjectValue("TheRoot");
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
