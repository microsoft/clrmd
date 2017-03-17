using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Returns the current state of what GC root is doing.
    /// </summary>
    public enum GCRootPhase
    {
        /// <summary>
        /// When enumerating the GC heap to build an object graph.
        /// </summary>
        BuildingGCCache,

        /// <summary>
        /// When building the thread cache.
        /// </summary>
        BuildingThreadCache,

        /// <summary>
        /// When building the handle table cache.
        /// </summary>
        BuildingHandleCache,

        /// <summary>
        /// During the GC root algorithm, searching through handle roots to find the target object.
        /// </summary>
        SearchingHandleRoots,

        /// <summary>
        /// During the GC root algorithm, searching through thread roots to find the target object.
        /// </summary>
        SearchingThreadRoots,
    }

    /// <summary>
    /// This sets the policy for how GCRoot walks the stack.  There is a choice here because the 'Exact' stack walking
    /// gives the correct answer (without overreporting), but unfortunately is poorly implemented in CLR's debugging layer.
    /// This means it could take 10-30 minutes (!) to enumerate roots on crash dumps with 4000+ threads.
    /// </summary>
    public enum GCRootStackWalkPolicy
    {
        /// <summary>
        /// The GCRoot class will attempt to select a policy for you based on the number of threads in the process.
        /// </summary>
        Heuristic,

        /// <summary>
        /// Use real stack roots.  This is much slower than 'Fast', but provides no false-positives for more accurate
        /// results.  Note that this can actually take 10s of minutes to enumerate the roots themselves in the worst
        /// case scenario.
        /// </summary>
        Exact,

        /// <summary>
        /// Walks each pointer alighed address on all stacks and if it points to an object it treats that location
        /// as a real root.  This can over-report roots when a value is left on the stack, but the GC does not
        /// consider it a real root.
        /// </summary>
        Fast,

        /// <summary>
        /// Do not walk stack roots.
        /// </summary>
        SkipStack,
    }

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
    /// <param name="phase">The current GCRoot phase.</param>
    /// <param name="current">The current progress.  This number will be 0&lt;= current &lt;= total.</param>
    /// <param name="total">The total value current is ticking up to.</param>
    public delegate void GCRootProgressEvent(GCRoot source, GCRootPhase phase, long current, long total);

    /// <summary>
    /// A helper class to find the GC rooting chain for a particular object.
    /// </summary>
    public class GCRoot
    {
        private static readonly Stack<ulong> s_emptyStack = new Stack<ulong>();
        private readonly ClrHeap _heap;
        private GCCache _cache;

        private ClrHandle[] _handles;
        private Dictionary<ulong, List<ulong>> _dependentMap;
        private int _maxTasks;

        /// <summary>
        /// Since GCRoot can be long running, this event attempts to provide progress updates on the phases of GC root.
        /// </summary>
        private event GCRootProgressEvent ProgressUpdate;

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
        /// The policy for walking the stack (see the GCRootStackWalkPolicy enum for more details).
        /// </summary>
        public GCRootStackWalkPolicy StackwalkPolicy { get; set; }
        
        /// <summary>
        /// Returns true if all relevant heap and root data is locally cached in this process for fast GCRoot processing.
        /// </summary>
        public bool IsFullyCached { get { return _cache != null && _cache.Complete && (StackwalkPolicy == GCRootStackWalkPolicy.SkipStack || _cache.StackRoots != null) && _handles != null; } }

        /// <summary>
        /// Creates a GCRoot helper object for the given heap.
        /// </summary>
        /// <param name="heap">The heap the object in question is on.</param>
        public GCRoot(ClrHeap heap)
        {
            _heap = heap ?? throw new ArgumentNullException(nameof(heap));
            _maxTasks = Environment.ProcessorCount * 2;
        }

        internal static bool TranslatePolicyToExact(ClrThread[] threads, GCRootStackWalkPolicy stackPolicy)
        {
            Debug.Assert(stackPolicy != GCRootStackWalkPolicy.SkipStack);
            return stackPolicy == GCRootStackWalkPolicy.Exact || (stackPolicy == GCRootStackWalkPolicy.Heuristic && threads.Length < 512);
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
            bool parallel = AllowParallelSearch && IsFullyCached && _maxTasks > 0;
            if (parallel)
            {
                ParallelObjectSet processedObjects = new ParallelObjectSet(_heap);
                Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints = new Dictionary<ulong, LinkedListNode<ClrObject>>();

                int initial = 0;
                Task<Tuple<LinkedList<ClrObject>, ClrRoot>>[] tasks = new Task<Tuple<LinkedList<ClrObject>, ClrRoot>>[_maxTasks];

                foreach (ClrHandle handle in EnumerateStrongHandles(cancelToken))
                {
                    Debug.Assert(handle.HandleType != HandleType.Dependent);
                    Debug.Assert(handle.Object != 0);

                    if (processedObjects.Contains(handle.Object))
                        continue;

                    Debug.Assert(_heap.GetObjectType(handle.Object) == handle.Type);

                    var task = PathToParaellel(processedObjects, knownEndPoints, handle, target, unique, cancelToken);
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
                
                foreach (ClrRoot root in EnumerateStackHandles())
                {
                    if (!processedObjects.Contains(root.Object))
                    {
                        Debug.Assert(_heap.GetObjectType(root.Object) == root.Type);

                        var task = PathToParaellel(processedObjects, knownEndPoints, root, target, unique, cancelToken);
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
                    
                    foreach (Tuple<LinkedList<ClrObject>, ClrRoot> result in WhenEach(tasks))
                        yield return new RootPath() { Root = result.Item2, Path = result.Item1.ToArray() };
                }

                if (initial < tasks.Length)
                    Array.Resize(ref tasks, initial);

                Task.WaitAll(tasks);
                foreach (var t in tasks)
                    if (t.Result.Item1 != null)
                        yield return new RootPath() { Root = t.Result.Item2, Path = t.Result.Item1.ToArray() };
            }
            else
            {
                ObjectSet processedObjects = new ObjectSet(_heap);
                Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints = new Dictionary<ulong, LinkedListNode<ClrObject>>();

                foreach (ClrHandle handle in EnumerateStrongHandles(cancelToken))
                {
                    ulong obj = handle.Object;
                    Debug.Assert(handle.HandleType != HandleType.Dependent);
                    Debug.Assert(obj != 0);
                    Debug.Assert(_heap.GetObjectType(obj) == handle.Type);
                    
                    if (processedObjects.Contains(obj))
                        continue;
                    
                    var path = PathTo(processedObjects, knownEndPoints, new ClrObject(obj, handle.Type), target, unique, false, cancelToken).FirstOrDefault();
                    if (path != null)
                        yield return new RootPath() { Root = GetHandleRoot(handle), Path = path.ToArray() };
                }

                foreach (ClrRoot root in EnumerateStackHandles())
                {
                    if (!processedObjects.Contains(root.Object))
                    {
                        Debug.Assert(_heap.GetObjectType(root.Object) == root.Type);
                        var path = PathTo(processedObjects, knownEndPoints, new ClrObject(root.Object, root.Type), target, unique, false, cancelToken).FirstOrDefault();
                        if (path != null)
                            yield return new RootPath() { Root = root, Path = path.ToArray() };
                    }
                }
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
            return PathTo(new ObjectSet(_heap), new Dictionary<ulong, LinkedListNode<ClrObject>>(), new ClrObject(source, _heap.GetObjectType(source)), target, unique, false, cancelToken);
        }

        /// <summary>
        /// Builds a cache of the GC heap and roots.  This will consume a LOT of memory, so when calling it you must wrap this in
        /// a try/catch for either OutOfMemoryException or GCRootCacheException (derives from OutOfMemoryException).  You may
        /// attempt to retry building the cache but with a lower number of objects (setting maxObjects to something like
        /// "GCRootCacheException.CompletedObjects / 3", for example).
        /// 
        /// Note that this function allows you to choose whether we have exact thread callstacks or not.  Exact thread callstacks
        /// will essentially force ClrMD to walk the stack as a real GC would, but this can take 10s of minutes when the thread count gets
        /// into the 1000s.
        /// </summary>
        /// <param name="maxObjects">The maxium number of objects to inspect.  int.MaxValue is reasonable.</param>
        /// <param name="cancelToken">The cancellation token used to cancel the operation if it's taking too long.</param>
        public void BuildCache(int maxObjects, CancellationToken cancelToken)
        {
            if (_cache == null)
                _cache = new GCCache();

            if (!_cache.Complete)
                _cache.BuildHeapCache(_heap, maxObjects, (curr, total) => ProgressUpdate?.Invoke(this, GCRootPhase.BuildingGCCache, curr, total), cancelToken);

            if (_cache.StackRoots == null || StackwalkPolicy != _cache.StackwalkPolicy)
                _cache.BuildThreadCache(_heap, StackwalkPolicy, (curr, total) => ProgressUpdate?.Invoke(this, GCRootPhase.BuildingThreadCache, curr, total), cancelToken);

            BuildHandleCache(cancelToken);
        }

        /// <summary>
        /// Builds a cache of the GC heap and roots.  This will consume a LOT of memory, so when calling it you must wrap this in
        /// a try/catch for either OutOfMemoryException or GCRootCacheException (derives from OutOfMemoryException).  You may
        /// attempt to retry building the cache but with a lower number of objects using the overload with that parameter.
        /// (That is, setting maxObjects to something like "GCRootCacheException.CompletedObjects / 3", for example.)
        /// 
        /// Note that this function allows you to choose whether we have exact thread callstacks or not.  Exact thread callstacks
        /// will essentially force ClrMD to walk the stack as a real GC would, but this can take 10s of minutes when the thread count gets
        /// into the 1000s.
        /// </summary>
        /// <param name="cancelToken">The cancellation token used to cancel the operation if it's taking too long.</param>
        public void BuildCache(CancellationToken cancelToken)
        {
            BuildCache(int.MaxValue, cancelToken);
        }

        /// <summary>
        /// Clears all caches, reclaiming most memory held by this GCRoot object.
        /// </summary>
        public void ClearCache()
        {
            _cache = null;
            _handles = null;
            _dependentMap = null;
        }

        private Task<Tuple<LinkedList<ClrObject>, ClrRoot>> PathToParaellel(ParallelObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrHandle handle, ulong target, bool unique, CancellationToken cancelToken)
        {
            Task<Tuple<LinkedList<ClrObject>, ClrRoot>> t = new Task<Tuple<LinkedList<ClrObject>, ClrRoot>>(() =>
            {
                LinkedList<ClrObject> path = PathTo(seen, knownEndPoints, ClrObject.Create(handle.Object, handle.Type), target, unique, true, cancelToken).FirstOrDefault();
                return new Tuple<LinkedList<ClrObject>, ClrRoot>(path, path != null ? GetHandleRoot(handle) : null);
            });

            t.Start();
            return t;
        }


        private Task<Tuple<LinkedList<ClrObject>, ClrRoot>> PathToParaellel(ParallelObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ClrRoot root, ulong target, bool unique, CancellationToken cancelToken)
        {
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
                path.AddLast(new PathEntry() { Object = source.Address });
                yield return GetResult(knownEndPoints, path, null, target);
                yield break;
            }

            path.AddLast(new PathEntry()
            {
                Object = source.Address,
                Todo = GetRefs(seen, knownEndPoints, source.Address, target, unique, parallel, cancelToken, out bool foundTarget, out LinkedListNode<ClrObject> foundEnding)
            });

            // Did the 'start' object point directly to 'end'?  If so, early out.
            if (foundTarget)
            {
                path.AddLast(new PathEntry() { Object = target });
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
                        ulong next = last.Todo.Pop();

                        // Now that we are in the process of adding 'next' to the path, don't ever consider
                        // this object in the future.
                        if (!seen.Add(next))
                            continue;

                        // We should never reach the 'end' here, as we always check if we found the target
                        // value when adding refs below.
                        Debug.Assert(next != target);

                        PathEntry nextPathEntry = new PathEntry()
                        {
                            Object = next,
                            Todo = GetRefs(seen, knownEndPoints, next, target, unique, parallel, cancelToken, out foundTarget, out foundEnding)
                        };

                        path.AddLast(nextPathEntry);
                        TraceCurrent(next, nextPathEntry.Todo);

                        // If we found the target object while enumerating refs of the current object, we are done.
                        if (foundTarget)
                        {
                            path.AddLast(new PathEntry() { Object = target });
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

        private Stack<ulong> GetRefs(ObjectSet seen, Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, ulong obj, ulong target, bool unique, bool parallel, CancellationToken cancelToken, out bool foundTarget, out LinkedListNode<ClrObject> foundEnding)
        {
            // These asserts slow debug down by a lot, but it's important to ensure consistency in retail.
            //Debug.Assert(obj.Type != null);
            //Debug.Assert(obj.Type == _heap.GetObjectType(obj.Address));

            Stack<ulong> result = s_emptyStack;

            bool found = false;
            LinkedListNode<ClrObject> ending = null;

            if (_cache?.HasMap ?? false)
            {
                if (TryGetObjectInfo(obj, out ObjectInfo objInfo))
                {
                    if (objInfo.RefCount > 0)
                    {
                        for (uint i = 0; i < objInfo.RefCount; i++)
                        {
                            cancelToken.ThrowIfCancellationRequested();
                            ulong refAddr = _cache.GCRefs[checked((int)objInfo.RefOffset) + i];
                            if (ending == null && knownEndPoints != null)
                            {
                                if (parallel)
                                {
                                    lock (knownEndPoints)
                                    {
                                        if (unique)
                                        {
                                            if (knownEndPoints.ContainsKey(refAddr))
                                                continue;
                                        }
                                        else
                                        {
                                            knownEndPoints.TryGetValue(refAddr, out ending);
                                        }
                                    }
                                }
                                else
                                {
                                    if (unique)
                                    {
                                        if (knownEndPoints.ContainsKey(refAddr))
                                            continue;
                                    }
                                    else
                                    {
                                        knownEndPoints.TryGetValue(refAddr, out ending);
                                    }
                                }
                            }

                            if (result == s_emptyStack)
                                result = new Stack<ulong>();

                            result.Push(refAddr);
                            if (refAddr == target)
                                found = true;
                        }
                    }

                    foundTarget = found;
                    foundEnding = ending;

                    return result;
                }

                if (_cache.Complete)
                {
                    foundTarget = false;
                    foundEnding = null;
                    return s_emptyStack;
                }
            }

            if (parallel)
                throw new InvalidOperationException("Attempted to access debuggee on an incorrect thread!");

            ClrType type = _heap.GetObjectType(obj);
            if (type != null && type.ContainsPointers && !IsTooLarge(obj, type, type.Heap.GetSegmentByAddress(obj)))
            {
                type.EnumerateRefsOfObject(obj, (refAddr, _) =>
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (ending == null && knownEndPoints != null)
                        knownEndPoints.TryGetValue(refAddr, out ending);

                    if (!seen.Contains(refAddr))
                    {
                        if (result == s_emptyStack)
                            result = new Stack<ulong>();

                        result.Push(refAddr);
                        if (refAddr == target)
                            found = true;
                    }
                });
            }

            foundTarget = found;
            foundEnding = ending;

            return result;
        }



        private LinkedList<ClrObject> GetResult(Dictionary<ulong, LinkedListNode<ClrObject>> knownEndPoints, LinkedList<PathEntry> path, LinkedListNode<ClrObject> ending, ulong target)
        {
            var result = new LinkedList<ClrObject>(path.Select(p => GetType(_heap, p.Object)).ToArray());

            for (; ending != null; ending = ending.Next)
                result.AddLast(ending.Value);

            if (knownEndPoints != null)
                lock (knownEndPoints)
                    for (LinkedListNode<ClrObject> node = result.First; node != null; node = node.Next)
                        if (node.Value.Address != target)
                            knownEndPoints[node.Value.Address] = node;

            return result;
        }

        private ClrObject GetType(ClrHeap heap, ulong obj)
        {
            ClrType type;
            if (TryGetObjectInfo(obj, out ObjectInfo info))
                type = info.Type;
            else
                type = _heap.GetObjectType(obj);

            return ClrObject.Create(obj, type);
        }


        private bool TryGetObjectInfo(ulong obj, out ObjectInfo info)
        {
            int index = 0;
            if (_cache?.ObjectMap?.TryGetValue(obj, out index) ?? false)
            {
                info = _cache.ObjectInfo[index];
                return true;
            }

            info = new ObjectInfo();
            return false;
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

        private void BuildHandleCache(CancellationToken cancelToken)
        {
            if (_handles != null)
            {
                Debug.Assert(_dependentMap != null);
                return;
            }

            // Unfortunately we do not have any idea how many handles there are total, so we can't give good progress updates.
            // Thankfully this tends to be a very fast enumeration, even with a lot of handles.
            ProgressUpdate?.Invoke(this, GCRootPhase.BuildingHandleCache, 0, 1);
            
            Dictionary<ulong, List<ulong>> dependentMap = new Dictionary<ulong, List<ulong>>();
            _handles = EnumerateStrongHandles(_heap, dependentMap, true, cancelToken).ToArray();
            _dependentMap = dependentMap;
            ProgressUpdate?.Invoke(this, GCRootPhase.BuildingHandleCache, 1, 1);
        }

        private static IEnumerable<ClrHandle> EnumerateStrongHandles(ClrHeap heap, Dictionary<ulong, List<ulong>> dependentMap, bool sort, CancellationToken cancelToken)
        {
            // For most operations we want to prioritize AsyncPinned and Pinned handles, followed by strong handles.
            // We do NOT guarantee order of handles walked, but ordering this here will give us a better shot at producing good results.
            // Also, handle enumeration tends to be fast (compared to everything else), so we can spend a little time getting it right since
            // we are caching the result.
            IEnumerable<ClrHandle> handles = heap.Runtime.EnumerateHandles();
            if (sort)
                handles = handles.OrderBy(h => GetOrder(h.HandleType));

            foreach (ClrHandle handle in handles)
            {
                cancelToken.ThrowIfCancellationRequested();

                if (handle.Object != 0)
                {
                    switch (handle.HandleType)
                    {
                        case HandleType.RefCount:
                            if (handle.RefCount > 0)
                                yield return handle;

                            break;

                        case HandleType.AsyncPinned:
                        case HandleType.Pinned:
                        case HandleType.SizedRef:
                        case HandleType.Strong:
                            yield return handle;
                            break;

                        case HandleType.Dependent:
                            if (dependentMap != null)
                            {
                                if (!dependentMap.TryGetValue(handle.Object, out List<ulong> list))
                                    dependentMap[handle.Object] = list = new List<ulong>();

                                list.Add(handle.DependentTarget);
                            }

                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private static int GetOrder(HandleType handleType)
        {
            switch (handleType)
            {
                case HandleType.AsyncPinned:
                    return 0;

                case HandleType.Pinned:
                    return 1;

                case HandleType.Strong:
                    return 2;

                case HandleType.RefCount:
                    return 3;

                default:
                    return 4;
            }
        }

        private IEnumerable<ClrHandle> EnumerateStrongHandles(CancellationToken cancelToken)
        {
            if (_handles != null)
                return _handles;

            return EnumerateStrongHandles(_heap, null, false, cancelToken);
        }

        private IEnumerable<ClrRoot> EnumerateStackHandles()
        {
            if (StackwalkPolicy == GCRootStackWalkPolicy.SkipStack)
                return new ClrRoot[0];

            if (_cache != null && _cache.StackRoots != null && _cache.StackwalkPolicy == StackwalkPolicy)
                return _cache.StackRoots;

            ClrThread[] threads = _heap.Runtime.Threads.Where(t => t.IsAlive).ToArray();
            bool exact = TranslatePolicyToExact(threads, StackwalkPolicy);
            return threads.SelectMany(t => t.EnumerateStackObjects(exact)).Where(r => !r.IsInterior && r.Object != 0);
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
            public ulong Object;
            public Stack<ulong> Todo;
            public override string ToString()
            {
                return Object.ToString();
            }
        }
    }
}
