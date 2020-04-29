// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A delegate for reporting GCRoot progress.
    /// </summary>
    /// <param name="source">The GCRoot sending the event.</param>
    /// <param name="processed">The total number of objects processed.</param>
    public delegate void GCRootProgressUpdatedEventHandler(GCRoot source, int processed);

    /// <summary>
    /// A helper class to find the GC rooting chain for a particular object.
    /// </summary>
    public sealed class GCRoot
    {
        private static readonly Stack<ClrObject> s_emptyStack = new Stack<ClrObject>();

        /// <summary>
        /// Since GCRoot can be long running, this event will provide periodic updates to how many objects the algorithm
        /// has processed.  Note that in the case where we search all objects and do not find a path, it's unlikely that
        /// the number of objects processed will ever reach the total number of objects on the heap.  That's because there
        /// will be garbage objects on the heap we can't reach.
        /// </summary>
        public event GCRootProgressUpdatedEventHandler? ProgressUpdated;

        /// <summary>
        /// Gets the heap that's associated with this GCRoot instance.
        /// </summary>
        public ClrHeap Heap { get; }

        /// <summary>
        /// Creates a GCRoot helper object for the given heap.
        /// </summary>
        /// <param name="heap">The heap the object in question is on.</param>
        public GCRoot(ClrHeap heap)
        {
            Heap = heap ?? throw new ArgumentNullException(nameof(heap));
        }

        /// <summary>
        /// Enumerates GCRoots of a given object.  Similar to !gcroot.  Note this function only returns paths that are fully unique.
        /// </summary>
        /// <param name="target">The target object to search for GC rooting.</param>
        /// <param name="cancellationToken">A cancellation token to stop enumeration.</param>
        /// <returns>An enumeration of all GC roots found for target.</returns>
        public IEnumerable<GCRootPath> EnumerateGCRoots(ulong target, CancellationToken cancellationToken = default)
        {
            return EnumerateGCRoots(target, true, cancellationToken);
        }

        public IEnumerable<GCRootPath> EnumerateGCRoots(ulong target, bool unique, CancellationToken cancellationToken = default)
        {
            return EnumerateGCRoots(target, unique, Environment.ProcessorCount, cancellationToken);
        }

        public IEnumerable<GCRootPath> EnumerateGCRoots(ulong target, bool unique, int maxDegreeOfParallelism, CancellationToken cancellationToken = default)
        {
            return EnumerateGCRoots(target, unique, maxDegreeOfParallelism, Heap.EnumerateRoots(), cancellationToken);
        }

        /// <summary>
        /// Enumerates GCRoots of a given object.  Similar to !gcroot.
        /// </summary>
        /// <param name="target">The target object to search for GC rooting.</param>
        /// <param name="unique">Whether to only return fully unique paths.</param>
        /// <param name="maxDegreeOfParallelism">The number of threads this class is allowed to use to calculate the result.
        /// Setting this to 1 will cause the algorithm to run on the current thread.</param>
        /// <param name="roots">The roots to consider.  You can pass ClrMD.</param>
        /// <param name="cancellationToken">A cancellation token to stop enumeration.</param>
        /// <returns>An enumeration of all GC roots found for target.</returns>
        public IEnumerable<GCRootPath> EnumerateGCRoots(ulong target, bool unique, int maxDegreeOfParallelism, IEnumerable<IClrRoot> roots, CancellationToken cancellationToken = default)
        {
            if (roots is null)
                throw new ArgumentNullException(nameof(roots));

            bool parallel = Heap.Runtime.IsThreadSafe && maxDegreeOfParallelism > 1;

            Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints = new Dictionary<ulong, LinkedListNode<ClrObject>>()
            {
                { target, new LinkedListNode<ClrObject>(Heap.GetObject(target)) }
            };

            if (!parallel)
            {
                int count = 0;

                ObjectSet processedObjects = new ObjectSet(Heap);
                foreach (IClrRoot root in roots)
                {
                    LinkedList<ClrObject> path = PathsTo(processedObjects, knownEndPoints, root.Object, target, unique, cancellationToken).FirstOrDefault();
                    if (path != null)
                        yield return new GCRootPath(root, path.ToImmutableArray());

                    if (count != processedObjects.Count)
                    {
                        count = processedObjects.Count;
                        ProgressUpdated?.Invoke(this, count);
                    }
                }
            }
            else
            {
                ParallelObjectSet processedObjects = new ParallelObjectSet(Heap);
                ConcurrentQueue<GCRootPath> results = new ConcurrentQueue<GCRootPath>();
                using BlockingCollection<IClrRoot?> queue = new BlockingCollection<IClrRoot?>();

                Thread[] threads = new Thread[Math.Min(maxDegreeOfParallelism, Environment.ProcessorCount)];
                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(() => WorkerThread(queue, results, processedObjects, knownEndPoints, target, all: true, unique, cancellationToken)) { Name = "GCRoot Worker Thread" };
                    threads[i].Start();
                }

                foreach (IClrRoot root in roots)
                    queue.Add(root);

                // Add one sentinal value for every thread
                for (int i = 0; i < threads.Length; i++)
                    queue.Add(null);

                int count = 0;

                // Worker threads end when they have run out of roots to process.  While we are waiting for them to exit, yield return
                // any results they've found.  We'll use a 100 msec timeout because processing roots is slooooow and finding a root is
                // rare.  There's no reason to check these results super quickly and starve worker threads.
                for (int i = 0; i < threads.Length; i++)
                {
                    while (!threads[i].Join(100))
                    {
                        while (results.TryDequeue(out GCRootPath result))
                            yield return result;
                    }

                    if (count != processedObjects.Count)
                    {
                        count = processedObjects.Count;
                        ProgressUpdated?.Invoke(this, count);
                    }
                }

                // We could have raced to put an object in the results queue while joining the last thread, so we need to drain the
                // results queue one last time.
                while (results.TryDequeue(out GCRootPath result))
                    yield return result;

                if (count != processedObjects.Count)
                {
                    count = processedObjects.Count;
                    ProgressUpdated?.Invoke(this, count);
                }
            }
        }

        private void WorkerThread(
            BlockingCollection<IClrRoot?> queue,
            ConcurrentQueue<GCRootPath> results,
            ObjectSet seen,
            Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints,
            ulong target,
            bool all,
            bool unique,
            CancellationToken cancellationToken)
        {
            IClrRoot? root;
            while ((root = queue.Take()) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (LinkedList<ClrObject> path in PathsTo(seen, knownEndPoints, root.Object, target, unique, cancellationToken))
                {
                    if (path != null)
                    {
                        results.Enqueue(new GCRootPath(root, path.ToImmutableArray()));

                        if (!all)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the path from the start object to the end object (or <see langword="null"/> if no such path exists).
        /// </summary>
        /// <param name="source">The initial object to start the search from.</param>
        /// <param name="target">The object we are searching for.</param>
        /// <param name="cancellationToken">A cancellation token to stop searching.</param>
        /// <returns>A path from 'source' to 'target' if one exists, <see langword="null"/> if one does not.</returns>
        public LinkedList<ClrObject> FindSinglePath(ulong source, ulong target, CancellationToken cancellationToken = default)
        {
            return PathsTo(new ObjectSet(Heap), null, new ClrObject(source, Heap.GetObjectType(source)), target, false, cancellationToken).FirstOrDefault();
        }

        /// <summary>
        /// Returns the path from the start object to the end object (or <see langword="null"/> if no such path exists).
        /// </summary>
        /// <param name="source">The initial object to start the search from.</param>
        /// <param name="target">The object we are searching for.</param>
        /// <param name="unique">Whether to only enumerate fully unique paths.</param>
        /// <param name="cancellationToken">A cancellation token to stop enumeration.</param>
        /// <returns>A path from 'source' to 'target' if one exists, <see langword="null"/> if one does not.</returns>
        public IEnumerable<LinkedList<ClrObject>> EnumerateAllPaths(ulong source, ulong target, bool unique, CancellationToken cancellationToken = default)
        {
            return PathsTo(
                new ObjectSet(Heap),
                new Dictionary<ulong, LinkedListNode<ClrObject>>(),
                new ClrObject(source, Heap.GetObjectType(source)),
                target,
                unique,
                cancellationToken);
        }

        private IEnumerable<LinkedList<ClrObject>> PathsTo(
            ObjectSet deadEnds,
            Dictionary<ulong, LinkedListNode<ClrObject>>? knownEndPoints,
            ClrObject source,
            ulong target,
            bool unique,
            CancellationToken cancellationToken)
        {
            /* knownEndPoints: A set of objects that are known to point to the target, once we see one of
                               these, we can end the search early and return the current path plus the
                               nodes there.

               deadEnds  When unique == false, these are Objects we've fully processed and know doesn't
                         point to the target object.  When unique == true, deadEnds are updated to include
                         objects that other threads are processing, ensuring we don't yield duplicate
                         roots. Note that we must be careful to ONLY add objects to this list when we are
                         sure we've exhausted all potential paths because parallel threads use this same
                         set.

               path: The current path we are considering.  As we walk through objects in the rooting chain,
                     we will add a new node to this list with the object in question and create a stack of
                     all objects it points to.  This is a depth-first search, so we will search as deeply
                     as we can, and pop off objects from path as soon as the stack of references is empty.
                     If we find that the reference stack is empty and we didn't find the target/known end
                     point, then we'll mark the object in deadEnds.

               processing: The list of objects we are currently considering on this thread.  Since objects
                           can contain circular references, we need to know we shouldn't re-consider objects
                           we encounter while walking objects.

               It's important to note that multiple threads may race and consider the same path at the same
               time.  This is expected (and similar to how the GC operates).  The thing we have to make sure
               of is that we only add objects to knownEndPoints/deadEnds when we are 100% sure of the result
               because it will directly affect other threads walking these paths.
            */

            HashSet<ulong> processing = new HashSet<ulong>() { source.Address };
            LinkedList<PathEntry> path = new LinkedList<PathEntry>();

            if (knownEndPoints != null)
            {
                lock (knownEndPoints)
                {
                    if (knownEndPoints.TryGetValue(source.Address, out LinkedListNode<ClrObject>? ending))
                    {
                        if (!unique || ending.Value.Address == target)
                        {
                            yield return GetResult(ending);
                        }

                        yield break;
                    }
                }
            }

            if (unique && !deadEnds.Add(source.Address))
                yield break;

            if (source.Type is null)
                yield break;

            if (source.Address == target)
            {
                path.AddLast(new PathEntry { Object = source });
                yield return GetResult();
                yield break;
            }

            path.AddLast(
                new PathEntry
                {
                    Object = source,
                    Todo = GetRefs(source, out bool foundTarget, out LinkedListNode<ClrObject>? foundEnding)
                });

            // Did the 'start' object point directly to 'end'?  If so, early out.
            if (foundTarget)
            {
                path.AddLast(new PathEntry { Object = Heap.GetObject(target), Todo = s_emptyStack });
                yield return GetResult();
                yield break;
            }
            else if (foundEnding != null)
            {
                if (!unique || foundEnding.Value.Address == target)
                {
                    yield return GetResult(foundEnding);
                    yield break;
                }
            }

            while (path.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                TraceFullPath(null, path);
                PathEntry last = path.Last!.Value;

                if (last.Todo.Count == 0)
                {
                    // We've exhausted all children and didn't find the target.  Remove this node
                    // and continue.
                    path.RemoveLast();
                    deadEnds.Add(last.Object.Address);
                    processing.Remove(last.Object.Address);
                }
                else
                {
                    // We loop here in case we encounter an object we've already processed (or if
                    // we can't get an object's type...inconsistent heap happens sometimes).
                    do
                    {
                        if (cancellationToken.IsCancellationRequested)
                            yield break;

                        ClrObject next = last.Todo.Pop();

                        // Now that we are in the process of adding 'next' to the path, don't ever consider
                        // this object in the future.
                        if (unique ? deadEnds.Add(next.Address) : deadEnds.Contains(next.Address))
                            continue;

                        if (knownEndPoints != null)
                            lock (knownEndPoints)
                                if (knownEndPoints.TryGetValue(next.Address, out LinkedListNode<ClrObject>? end))
                                {
                                    TraceFullPath(path, end);
                                    yield return GetResult(end);

                                    path.RemoveLast();
                                }

                        if (!processing.Add(next.Address))
                            continue;

                        // We should never reach the 'end' here, as we always check if we found the target
                        // value when adding refs below.
                        Debug.Assert(next.Address != target);

                        PathEntry nextPathEntry = new PathEntry
                        {
                            Object = next,
                            Todo = GetRefs(next, out foundTarget, out foundEnding)
                        };

                        path.AddLast(nextPathEntry);

                        // If we found the target object while enumerating refs of the current object, we are done.
                        if (foundTarget)
                        {
                            path.AddLast(new PathEntry { Object = Heap.GetObject(target) });
                            TraceFullPath("FoundTarget", path);

                            yield return GetResult();

                            path.RemoveLast();
                            path.RemoveLast();
                        }
                        else if (foundEnding != null)
                        {
                            if (!unique || foundEnding.Value.Address == target)
                            {
                                TraceFullPath(path, foundEnding);
                                yield return GetResult(foundEnding);
                            }

                            path.RemoveLast();
                        }

                        // Now that we've added a new entry to 'path', break out of the do/while that's looping through Todo.
                        break;
                    } while (last.Todo.Count > 0);
                }
            }

            Stack<ClrObject> GetRefs(
                ClrObject obj,
                out bool found,
                out LinkedListNode<ClrObject>? end)
            {
                // These asserts slow debug down by a lot, but it's important to ensure consistency in retail.
                // DebugOnly.Assert(obj.Type != null);
                // DebugOnly.Assert(obj.Type == _heap.GetObjectType(obj.Address));

                Stack<ClrObject>? result = null;

                found = false;
                end = null;
                if (obj.Type != null && (obj.Type.ContainsPointers || obj.Type.IsCollectible))
                {
                    foreach (ClrObject reference in obj.EnumerateReferences(true))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!unique && end == null && knownEndPoints != null)
                        {
                            lock (knownEndPoints)
                            {
                                knownEndPoints.TryGetValue(reference.Address, out end);
                            }
                        }

                        if (reference.Address == target)
                        {
                            found = true;
                        }

                        if (!deadEnds.Contains(reference.Address))
                        {
                            result ??= new Stack<ClrObject>();
                            result.Push(reference);
                        }
                    }
                }

                return result ?? s_emptyStack;
            }

            LinkedList<ClrObject> GetResult(LinkedListNode<ClrObject>? end = null)
            {
                LinkedList<ClrObject> result = new LinkedList<ClrObject>(path.Select(p => p.Object));

                for (; end != null; end = end.Next)
                    result.AddLast(end.Value);

                if (knownEndPoints != null)
                {
                    lock (knownEndPoints)
                    {
                        for (LinkedListNode<ClrObject>? node = result.First; node != null; node = node.Next)
                        {
                            ulong address = node.Value.Address;
                            if (knownEndPoints.ContainsKey(address))
                                break;

                            knownEndPoints[address] = node;
                        }
                    }
                }

                if (unique)
                {
                    foreach (ClrObject obj in result)
                    {
                        deadEnds.Add(obj);
                    }
                }

                return result;
            }
        }

        internal static bool IsTooLarge(ulong obj, ClrType type, ClrSegment seg)
        {
            ulong size = type.Heap.GetObjectSize(obj, type);
            if (!seg.IsLargeObjectSegment && size >= 85000)
                return true;

            return obj + size > seg.End;
        }

        [Conditional("GCROOTTRACE")]
        private static void TraceFullPath(LinkedList<PathEntry> path, LinkedListNode<ClrObject> foundEnding)
        {
            Debug.WriteLine($"FoundEnding: {string.Join(" ", path.Select(p => p.Object.ToString()))} {string.Join(" ", NodeToList(foundEnding))}");
        }

        private static List<string> NodeToList(LinkedListNode<ClrObject> tmp)
        {
            List<string> list = new List<string>();
            for (LinkedListNode<ClrObject>? node = tmp; node != null; node = node.Next)
                list.Add(node.Value.ToString());

            return list;
        }

        [Conditional("GCROOTTRACE")]
        private static void TraceFullPath(string? prefix, LinkedList<PathEntry> path)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
                prefix += ": ";
            else
                prefix = string.Empty;

            Debug.WriteLine(prefix + string.Join(" ", path.Select(p => p.Object.ToString())));
        }

        private struct PathEntry
        {
            public ClrObject Object;
            public Stack<ClrObject> Todo;

            public override string ToString()
            {
                return Object.ToString();
            }
        }
    }
}