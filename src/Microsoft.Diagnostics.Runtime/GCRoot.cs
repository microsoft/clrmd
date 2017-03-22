using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a path of objects from a root to an object.
    /// </summary>
    public struct RootPath
    {
        /// <summary>
        /// The location that roots the object.
        /// </summary>
        public ClrRoot Root { get; set; }

        /// <summary>
        /// The path from Root to a given target object.
        /// </summary>
        public ClrObject[] Path { get; set; }
    }

    /// <summary>
    /// A delegate for reporting GCRoot progress.
    /// </summary>
    /// <param name="source">The GCRoot sending the event.</param>
    /// <param name="current">The total number of objects processed.</param>
    /// <param name="total">The total number of objects in the heap, if that number is known, otherwise -1.</param>
    public delegate void GCRootProgressEvent(GCRoot source, long current, long total);

    /// <summary>
    /// A helper class to find the GC rooting chain for a particular object.
    /// </summary>
    public class GCRoot
    {
        private static readonly Stack<ClrObject> s_emptyStack = new Stack<ClrObject>();
        private readonly ClrHeap _heap;
        private int _maxTasks;

        /// <summary>
        /// Since GCRoot can be long running, this event will provide periodic updates to how many objects the algorithm
        /// has processed.  Note that in the case where we search all objects and do not find a path, it's unlikely that
        /// the number of objects processed will ever reach the total number of objects on the heap.  That's because there
        /// will be garbage objects on the heap we can't reach.
        /// </summary>
        public event GCRootProgressEvent ProgressUpdate;

        /// <summary>
        /// Returns the heap that's associated with this GCRoot instance.
        /// </summary>
        public ClrHeap Heap { get { return _heap; } }

        /// <summary>
        /// Whether or not to allow GC root to search in parallel or not.  Note that GCRoot does not have to respect this
        /// flag.  Parallel searching of roots will only happen if a copy of the stack and heap were built using BuildCache,
        /// and if the entire heap was cached.  Note that ClrMD and underlying APIs do NOT support multithreading, so this
        /// is only used when we can ensure all relevant data is local memory and we do not need to touch the debuggee.
        /// </summary>
        public bool AllowParallelSearch { get; set; } = true;

        /// <summary>
        /// The maximum number of tasks allowed to run in parallel, if GCRoot does a parallel search.
        /// </summary>
        public int MaximumTasksAllowed
        {
            get
            {
                return _maxTasks;
            }
            set
            {
                if (_maxTasks < 0)
                    throw new InvalidOperationException($"{nameof(MaximumTasksAllowed)} cannot be less than 0!");

                _maxTasks = value;
            }
        }
        
        /// <summary>
        /// Returns true if all relevant heap and root data is locally cached in this process for fast GCRoot processing.
        /// </summary>
        public bool IsFullyCached { get { return _heap.AreRootsCached; } }

        /// <summary>
        /// Creates a GCRoot helper object for the given heap.
        /// </summary>
        /// <param name="heap">The heap the object in question is on.</param>
        public GCRoot(ClrHeap heap)
        {
            _heap = heap ?? throw new ArgumentNullException(nameof(heap));
            _maxTasks = Environment.ProcessorCount * 2;
        }
        

        /// <summary>
        /// Enumerates GCRoots of a given object.  Similar to !gcroot.  Note this function only returns paths that are fully unique.
        /// </summary>
        /// <param name="target">The target object to search for GC rooting.</param>
        /// <param name="cancelToken">A cancellation token to stop enumeration.</param>
        /// <returns>An enumeration of all GC roots found for target.</returns>
        public IEnumerable<RootPath> EnumerateGCRoots(ulong target, CancellationToken cancelToken)
        {
            return EnumerateGCRoots(target, true, cancelToken);
        }

        /// <summary>
        /// Enumerates GCRoots of a given object.  Similar to !gcroot.
        /// </summary>
        /// <param name="target">The target object to search for GC rooting.</param>
        /// <param name="unique">Whether to only return fully unique paths.</param>
        /// <param name="cancelToken">A cancellation token to stop enumeration.</param>
        /// <returns>An enumeration of all GC roots found for target.</returns>
        public IEnumerable<RootPath> EnumerateGCRoots(ulong target, bool unique, CancellationToken cancelToken)
        {
            _heap.BuildDependentHandleMap(cancelToken);
            long totalObjects = _heap.TotalObjects;
            long lastObjectReported = 0;
            
            bool parallel = AllowParallelSearch && IsFullyCached && _maxTasks > 0;

            Task<Tuple<LinkedList<ClrObject>, ClrRoot>>[] tasks;
            ObjectSet processedObjects;
            Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints = new Dictionary<ulong, LinkedListNode<ClrObject>>();

            if (parallel)
                processedObjects = new ParallelObjectSet(_heap);

            else
                processedObjects = new ObjectSet(_heap);

            int initial = 0;
            tasks = new Task<Tuple<LinkedList<ClrObject>, ClrRoot>>[_maxTasks];

            foreach (ClrHandle handle in _heap.EnumerateStrongHandles())
            {
                Debug.Assert(handle.HandleType != HandleType.Dependent);
                Debug.Assert(handle.Object != 0);

                if (processedObjects.Contains(handle.Object))
                    continue;

                Debug.Assert(_heap.GetObjectType(handle.Object) == handle.Type);

                if (parallel)
                {
                    var task = PathToParallel(processedObjects, knownEndPoints, handle, target, unique, cancelToken);
                    if (initial < tasks.Length)
                    {
                        tasks[initial++] = task;
                    }
                    else
                    {
                        int i = Task.WaitAny(tasks);
                        var completed = tasks[i];
                        tasks[i] = task;

                        if (completed.Result.Item1 != null)
                            yield return new RootPath() { Root = completed.Result.Item2, Path = completed.Result.Item1.ToArray() };

                    }
                }
                else
                {
                    var path = PathTo(processedObjects, knownEndPoints, new ClrObject(handle.Object, handle.Type), target, unique, false, cancelToken).FirstOrDefault();
                    if (path != null)
                        yield return new RootPath() { Root = GetHandleRoot(handle), Path = path.ToArray() };
                }

                ReportObjectCount(processedObjects.Count, ref lastObjectReported, totalObjects);
            }
                
            foreach (ClrRoot root in _heap.EnumerateStackRoots())
            {
                if (!processedObjects.Contains(root.Object))
                {
                    Debug.Assert(_heap.GetObjectType(root.Object) == root.Type);

                    if (parallel)
                    {
                        var task = PathToParallel(processedObjects, knownEndPoints, root, target, unique, cancelToken);
                        if (initial < tasks.Length)
                        {
                            tasks[initial++] = task;
                        }
                        else
                        {
                            int i = Task.WaitAny(tasks);
                            var completed = tasks[i];
                            tasks[i] = task;

                            if (completed.Result.Item1 != null)
                                yield return new RootPath() { Root = completed.Result.Item2, Path = completed.Result.Item1.ToArray() };
                        }
                    }
                    else
                    {
                        var path = PathTo(processedObjects, knownEndPoints, new ClrObject(root.Object, root.Type), target, unique, false, cancelToken).FirstOrDefault();
                        if (path != null)
                            yield return new RootPath() { Root = root, Path = path.ToArray() };
                    }

                    ReportObjectCount(processedObjects.Count, ref lastObjectReported, totalObjects);
                }
            }

            foreach (Tuple<LinkedList<ClrObject>, ClrRoot> result in WhenEach(tasks))
            {
                ReportObjectCount(processedObjects.Count, ref lastObjectReported, totalObjects);
                yield return new RootPath() { Root = result.Item2, Path = result.Item1.ToArray() };
            }

            ReportObjectCount(totalObjects, ref lastObjectReported, totalObjects);
        }

        private void ReportObjectCount(long curr, ref long lastObjectReported, long totalObjects)
        {
            if (curr != lastObjectReported)
            {
                lastObjectReported = curr;
                ProgressUpdate?.Invoke(this, lastObjectReported, totalObjects);
            }
        }

        private static IEnumerable<Tuple<LinkedList<ClrObject>, ClrRoot>> WhenEach(Task<Tuple<LinkedList<ClrObject>, ClrRoot>>[] tasks)
        {
            var taskList = tasks.Where(t => t != null).ToList();

            while (taskList.Count > 0)
            {
                var task = Task.WhenAny(taskList).Result;
                if (task.Result.Item1 != null)
                    yield return task.Result;

                bool removed = taskList.Remove(task);
                Debug.Assert(removed);
            }
        }


        /// <summary>
        /// Returns the path from the start object to the end object (or null if no such path exists).
        /// </summary>
        /// <param name="source">The initial object to start the search from.</param>
        /// <param name="target">The object we are searching for.</param>
        /// <param name="cancelToken">A cancellation token to stop searching.</param>
        /// <returns>A path from 'source' to 'target' if one exists, null if one does not.</returns>
        public LinkedList<ClrObject> FindSinglePath(ulong source, ulong target, CancellationToken cancelToken)
        {
            _heap.BuildDependentHandleMap(cancelToken);
            return PathTo(new ObjectSet(_heap), null, new ClrObject(source, _heap.GetObjectType(source)), target, false, false, cancelToken).FirstOrDefault();
        }

        /// <summary>
        /// Returns the path from the start object to the end object (or null if no such path exists).
        /// </summary>
        /// <param name="source">The initial object to start the search from.</param>
        /// <param name="target">The object we are searching for.</param>
        /// <param name="unique">Whether to only enumerate fully unique paths.</param>
        /// <param name="cancelToken">A cancellation token to stop enumeration.</param>
        /// <returns>A path from 'source' to 'target' if one exists, null if one does not.</returns>
        public IEnumerable<LinkedList<ClrObject>> EnumerateAllPaths(ulong source, ulong target, bool unique, CancellationToken cancelToken)
        {
            _heap.BuildDependentHandleMap(cancelToken);
            return PathTo(new ObjectSet(_heap), new Dictionary<ulong, LinkedListNode<ClrObject>>(), new ClrObject(source, _heap.GetObjectType(source)), target, unique, false, cancelToken);
        }
        

        /// <summary>
        /// Builds a cache of the GC heap and roots.  This will consume a LOT of memory, so when calling it you must wrap this in
        /// a try/catch for OutOfMemoryException.
        /// 
        /// Note that this function allows you to choose whether we have exact thread callstacks or not.  Exact thread callstacks
        /// will essentially force ClrMD to walk the stack as a real GC would, but this can take 10s of minutes when the thread count gets
        /// into the 1000s.
        /// </summary>
        /// <param name="cancelToken">The cancellation token used to cancel the operation if it's taking too long.</param>
        public void BuildCache(CancellationToken cancelToken)
        {
            _heap.CacheRoots(cancelToken);
            _heap.CacheHeap(cancelToken);
        }

        /// <summary>
        /// Clears all caches, reclaiming most memory held by this GCRoot object.
        /// </summary>
        public void ClearCache()
        {
            _heap.ClearHeapCache();
            _heap.ClearRootCache();
        }

        private Task<Tuple<LinkedList<ClrObject>, ClrRoot>> PathToParallel(ObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrHandle handle, ulong target, bool unique, CancellationToken cancelToken)
        {
            Debug.Assert(IsFullyCached);

            Task<Tuple<LinkedList<ClrObject>, ClrRoot>> t = new Task<Tuple<LinkedList<ClrObject>, ClrRoot>>(() =>
            {
                LinkedList<ClrObject> path = PathTo(seen, knownEndPoints, ClrObject.Create(handle.Object, handle.Type), target, unique, true, cancelToken).FirstOrDefault();
                return new Tuple<LinkedList<ClrObject>, ClrRoot>(path, path != null ? GetHandleRoot(handle) : null);
            });

            t.Start();
            return t;
        }


        private Task<Tuple<LinkedList<ClrObject>, ClrRoot>> PathToParallel(ObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrRoot root, ulong target, bool unique, CancellationToken cancelToken)
        {
            Debug.Assert(IsFullyCached);

            Task<Tuple<LinkedList<ClrObject>, ClrRoot>> t = new Task<Tuple<LinkedList<ClrObject>, ClrRoot>>(() => new Tuple<LinkedList<ClrObject>, ClrRoot>(PathTo(seen, knownEndPoints, ClrObject.Create(root.Object, root.Type), target, unique, true, cancelToken).FirstOrDefault(), root));
            t.Start();
            return t;
        }

        private IEnumerable<LinkedList<ClrObject>> PathTo(ObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrObject source, ulong target, bool unique, bool parallel, CancellationToken cancelToken)
        {
            seen.Add(source.Address);
            
            if (source.Type == null)
                yield break;

            LinkedList<PathEntry> path = new LinkedList<PathEntry>();

            if (source.Address == target)
            {
                path.AddLast(new PathEntry() { Object = source });
                yield return GetResult(knownEndPoints, path, null, target);
                yield break;
            }

            path.AddLast(new PathEntry()
            {
                Object = source,
                Todo = GetRefs(seen, knownEndPoints, source, target, unique, parallel, cancelToken, out bool foundTarget, out LinkedListNode<ClrObject> foundEnding)
            });

            // Did the 'start' object point directly to 'end'?  If so, early out.
            if (foundTarget)
            {
                path.AddLast(new PathEntry() { Object = ClrObject.Create(target, _heap.GetObjectType(target)) });
                yield return GetResult(knownEndPoints, path, null, target);
            }
            else if (foundEnding != null)
            {
                yield return GetResult(knownEndPoints, path, foundEnding, target);
            }

            while (path.Count > 0)
            {
                cancelToken.ThrowIfCancellationRequested();

                TraceFullPath(null, path);
                var last = path.Last.Value;

                if (last.Todo.Count == 0)
                {
                    // We've exhausted all children and didn't find the target.  Remove this node
                    // and continue.
                    path.RemoveLast();
                }
                else
                {
                    // We loop here in case we encounter an object we've already processed (or if
                    // we can't get an object's type...inconsistent heap happens sometimes).
                    do
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        ClrObject next = last.Todo.Pop();

                        // Now that we are in the process of adding 'next' to the path, don't ever consider
                        // this object in the future.
                        if (!seen.Add(next.Address))
                            continue;

                        // We should never reach the 'end' here, as we always check if we found the target
                        // value when adding refs below.
                        Debug.Assert(next.Address != target);

                        PathEntry nextPathEntry = new PathEntry()
                        {
                            Object = next,
                            Todo = GetRefs(seen, knownEndPoints, next, target, unique, parallel, cancelToken, out foundTarget, out foundEnding)
                        };

                        path.AddLast(nextPathEntry);

                        // If we found the target object while enumerating refs of the current object, we are done.
                        if (foundTarget)
                        {
                            path.AddLast(new PathEntry() { Object = ClrObject.Create(target, _heap.GetObjectType(target)) });
                            TraceFullPath("FoundTarget", path);

                            yield return GetResult(knownEndPoints, path, null, target);
                            path.RemoveLast();
                            path.RemoveLast();
                        }
                        else if (foundEnding != null)
                        {
                            TraceFullPath(path, foundEnding);
                            yield return GetResult(knownEndPoints, path, foundEnding, target);
                            path.RemoveLast();
                        }

                        // Now that we've added a new entry to 'path', break out of the do/while that's looping through Todo.
                        break;
                    }
                    while (last.Todo.Count > 0);
                }
            }
        }

        private Stack<ClrObject> GetRefs(ObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrObject obj, ulong target, bool unique, bool parallel, CancellationToken cancelToken, out bool foundTarget, out LinkedListNode<ClrObject> foundEnding)
        {
            // These asserts slow debug down by a lot, but it's important to ensure consistency in retail.
            //Debug.Assert(obj.Type != null);
            //Debug.Assert(obj.Type == _heap.GetObjectType(obj.Address));

            Stack<ClrObject> result = s_emptyStack;

            bool found = false;
            LinkedListNode<ClrObject> ending = null;
            if (obj.ContainsPointers)
            {
                foreach (ClrObject reference in obj.EnumerateObjectReferences(true))
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (ending == null && knownEndPoints != null)
                    {
                        lock (knownEndPoints)
                        {
                            if (unique)
                            {
                                if (knownEndPoints.ContainsKey(reference.Address))
                                    continue;
                            }
                            else
                            {
                                knownEndPoints.TryGetValue(reference.Address, out ending);
                            }
                        }
                    }

                    if (!seen.Contains(reference.Address))
                    {
                        if (result == s_emptyStack)
                            result = new Stack<ClrObject>();

                        result.Push(reference);
                        if (reference.Address == target)
                            found = true;
                    }
                }
            }

            foundTarget = found;
            foundEnding = ending;

            return result;
        }
        
        private LinkedList<ClrObject> GetResult(Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, LinkedList<PathEntry> path, LinkedListNode<ClrObject> ending, ulong target)
        {
            var result = new LinkedList<ClrObject>(path.Select(p => p.Object).ToArray());

            for (; ending != null; ending = ending.Next)
                result.AddLast(ending.Value);

            if (knownEndPoints != null)
                lock (knownEndPoints)
                    for (LinkedListNode<ClrObject> node = result.First; node != null; node = node.Next)
                        if (node.Value.Address != target)
                            knownEndPoints[node.Value.Address] = node;

            return result;
        }
        
        private ClrRoot GetHandleRoot(ClrHandle handle)
        {
            GCRootKind kind = GCRootKind.Strong;

            switch (handle.HandleType)
            {
                case HandleType.Pinned:
                    kind = GCRootKind.Pinning;
                    break;

                case HandleType.AsyncPinned:
                    kind = GCRootKind.AsyncPinning;
                    break;
            }

            return new HandleRoot(handle.Address, handle.Object, handle.Type, handle.HandleType, kind, handle.AppDomain);
        }

        internal static bool IsTooLarge(ulong obj, ClrType type, ClrSegment seg)
        {
            ulong size = type.GetSize(obj);
            if (!seg.IsLarge && size >= 85000)
                return true;

            return obj + size > seg.End;
        }

        #region Debug Tracing
        [Conditional("GCROOTTRACE")]
        private void TraceFullPath(LinkedList<PathEntry> path, LinkedListNode<ClrObject> foundEnding)
        {
            Debug.WriteLine($"FoundEnding: {string.Join(" ", path.Select(p => p.Object.ToString()))} {string.Join(" ", NodeToList(foundEnding))}");
        }
        
        private List<string> NodeToList(LinkedListNode<ClrObject> tmp)
        {
            List<string> list = new List<string>();
            for (; tmp != null; tmp = tmp.Next)
                list.Add(tmp.Value.ToString());

            return list;
        }

        [Conditional("GCROOTTRACE")]
        private void TraceCurrent(ulong next, IEnumerable<ulong> refs)
        {
            Debug.WriteLine($"Considering: {new ClrObject(next, _heap.GetObjectType(next))} Refs:{string.Join(" ", refs)}");
        }

        [Conditional("GCROOTTRACE")]
        private void TraceFullPath(string prefix, LinkedList<PathEntry> path)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
                prefix += ": ";
            else
                prefix = "";

            Debug.WriteLine(prefix + string.Join(" ", path.Select(p => p.Object.ToString())));
        }
        #endregion

        struct PathEntry
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
