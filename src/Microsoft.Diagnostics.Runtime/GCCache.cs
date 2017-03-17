using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Thrown when GCRoot runs out of memory allocating the GC Cache.  Provides the object count we failed on
    /// so the operation can be re-run taking up less memory.
    /// </summary>
    public class GCRootCacheException : OutOfMemoryException
    {
        internal GCRootCacheException(int objectCount)
        {
            CompletedObjects = objectCount;
        }

        /// <summary>
        /// Number of objects successfully processed before we encountered an error.
        /// </summary>
        public int CompletedObjects { get; private set; }
    }

    internal class GCCache
    {
        public DictionaryList ObjectMap { get; private set; }
        public ExtendedArray<ObjectInfo> ObjectInfo { get; private set; }
        public ExtendedArray<ulong> GCRefs { get; private set; }

        public bool HasMap { get { return ObjectMap != null; } }
        public bool Complete { get; private set; }
        public GCRootStackWalkPolicy StackwalkPolicy { get; private set; }

        public ClrRoot[] StackRoots { get; private set; }
        public int Objects { get; private set; }
        public int MaxObjects { get; private set; }
        public int BadObjects { get; private set; }


        public void BuildHeapCache(ClrHeap heap, int max, Action<long, long> progressReport, CancellationToken cancelToken)
        {
            ClearObjects();
            MaxObjects = max;

            int count = 0, badObjects = 0;
            try
            {
                DictionaryList objmap = new DictionaryList();
                ExtendedArray<ulong> gcrefs = new ExtendedArray<ulong>();
                ExtendedArray<ObjectInfo> objInfo = new ExtendedArray<ObjectInfo>();

                long totalBytes = heap.Segments.Sum(s => (long)s.Length);
                long completed = 0;

                uint pointerSize = (uint)heap.PointerSize;

                foreach (ClrSegment seg in heap.Segments)
                {
                    progressReport?.Invoke(completed, totalBytes);
                    
                    if (count >= max)
                        break;

                    for (ulong obj = seg.FirstObject; obj < seg.End && obj != 0; obj = seg.NextObject(obj))
                    {
                        cancelToken.ThrowIfCancellationRequested();

                        if (count >= max)
                            break;

                        // We may
                        ClrType type = heap.GetObjectType(obj);
                        if (type == null || GCRoot.IsTooLarge(obj, type, seg))
                        {
                            AddObject(objmap, gcrefs, objInfo, obj, heap.Free);
                            do
                            {
                                cancelToken.ThrowIfCancellationRequested();

                                obj += pointerSize;
                                if (obj >= seg.End)
                                    break;

                                type = heap.GetObjectType(obj);
                            } while (type == null);

                            if (obj >= seg.End)
                                break;

                            badObjects++;
                        }

                        count++;

                        AddObject(objmap, gcrefs, objInfo, obj, type);
                    }

                    completed += (long)seg.Length;
                }

                progressReport?.Invoke(totalBytes, totalBytes);

                GCRefs = gcrefs;
                ObjectInfo = objInfo;
                ObjectMap = objmap;
                Complete = true;
            }
            catch (OutOfMemoryException)
            {
                // Just to be safe we'll clear everything out.
                ClearObjects();
                throw new GCRootCacheException(count);
            }
        }

        public void BuildThreadCache(ClrHeap heap, GCRootStackWalkPolicy stackPolicy, Action<long, long> progressReport, CancellationToken cancelToken)
        {
            ClearThreads();
            StackwalkPolicy = stackPolicy;

            if (stackPolicy == GCRootStackWalkPolicy.SkipStack)
                return;

            try
            {
                List<ClrRoot> result = new List<ClrRoot>();
                ClrThread[] threads = heap.Runtime.Threads.Where(t => t.IsAlive).ToArray();

                bool exact = GCRoot.TranslatePolicyToExact(threads, StackwalkPolicy);
                for (int i = 0; i < threads.Length; i++)
                {
                    progressReport?.Invoke(i, threads.Length);

                    ClrThread thread = threads[i];
                    foreach (ClrRoot root in thread.EnumerateStackObjects(!exact))
                    {
                        cancelToken.ThrowIfCancellationRequested();

                        if (root.IsInterior || root.Object == 0)
                            continue;

                        result.Add(root);
                    }
                }

                // There could be a LOT of roots.  Forcing this back down to an array clears wasted space...at the cost of time.
                StackRoots = result.ToArray();
                progressReport?.Invoke(threads.Length, threads.Length);
            }
            catch (OutOfMemoryException)
            {
                throw new GCRootCacheException(Objects);
            }
        }
        
        public void ClearObjects()
        {
            ObjectMap = null;
            ObjectInfo = null;
            GCRefs = null;
            Objects = 0;
            BadObjects = 0;
            MaxObjects = 0;
            Complete = false;
        }

        public void ClearThreads()
        {
            StackRoots = null;
        }
        
        private static void AddObject(DictionaryList objmap, ExtendedArray<ulong> gcrefs, ExtendedArray<ObjectInfo> objInfo, ulong obj, ClrType type)
        {
            uint offset = gcrefs.Count;

            if (type.ContainsPointers)
            {
                type.EnumerateRefsOfObject(obj, (addr, offs) =>
                {
                    gcrefs.Add(addr);
                });
            }

            uint refCount = gcrefs.Count - offset;
            objmap.Add(obj, checked((int)objInfo.Count));
            objInfo.Add(new ObjectInfo()
            {
                Type = type,
                RefOffset = refCount != 0 ? (uint)offset : uint.MaxValue,
                RefCount = refCount
            });
        }

        #region Large array/dictionary helpers
        internal class ExtendedArray<T>
        {
            private const int Initial = 0x100000; // 1 million-ish
            private const int Secondary = 0x1000000;
            private const int Complete = 0x4000000;

            private List<T[]> _lists = new List<T[]>();
            private int _curr = 0;

            public uint Count
            {
                get
                {
                    if (_lists.Count <= 0)
                        return 0;

                    uint total = (uint)(_lists.Count - 1) * Complete;
                    total += (uint)_curr;
                    return total;
                }
            }

            public T this[int index]
            {
                get
                {
                    int arrayIndex = index / Complete;
                    index %= Complete;

                    return _lists[arrayIndex][index];
                }
                set
                {
                    int arrayIndex = index / Complete;
                    index %= Complete;

                    _lists[arrayIndex][index] = value;
                }
            }


            public T this[long index]
            {
                get
                {
                    long arrayIndex = index / Complete;
                    index %= Complete;

                    return _lists[(int)arrayIndex][index];
                }
                set
                {
                    long arrayIndex = index / Complete;
                    index %= Complete;

                    _lists[(int)arrayIndex][index] = value;
                }
            }

            public void Add(T t)
            {
                T[] arr = _lists.LastOrDefault();
                if (arr == null || _curr == Complete)
                {
                    arr = new T[Initial];
                    _lists.Add(arr);
                    _curr = 0;
                }

                if (_curr >= arr.Length)
                {
                    if (arr.Length == Complete)
                    {
                        arr = new T[Initial];
                        _lists.Add(arr);
                        _curr = 0;
                    }
                    else
                    {
                        int newSize = arr.Length == Initial ? Secondary : Complete;

                        _lists.RemoveAt(_lists.Count - 1);
                        Array.Resize(ref arr, newSize);
                        _lists.Add(arr);
                    }
                }

                arr[_curr++] = t;
            }

            public void Condense()
            {
                T[] arr = _lists.LastOrDefault();
                if (arr != null && _curr < arr.Length)
                {
                    _lists.RemoveAt(_lists.Count - 1);
                    Array.Resize(ref arr, _curr);
                    _lists.Add(arr);
                }
            }
        }

        internal class DictionaryList
        {
            private const int MaxEntries = 40000000;
            private List<Entry> _entries = new List<Entry>();

            public void Add(ulong obj, int index)
            {
                Entry curr = GetOrCreateEntry(obj);
                curr.End = obj;
                curr.Dictionary.Add(obj, index);
            }

            public bool TryGetValue(ulong obj, out int index)
            {
                foreach (Entry entry in _entries)
                    if (entry.Start <= obj && obj <= entry.End)
                        return entry.Dictionary.TryGetValue(obj, out index);

                index = 0;
                return false;
            }

            private Entry GetOrCreateEntry(ulong obj)
            {
                if (_entries.Count == 0)
                {
                    return NewEntry(obj);
                }
                else
                {
                    Entry last = _entries.Last();
                    if (last.Dictionary.Count > MaxEntries)
                        return NewEntry(obj);

                    return last;
                }
            }

            private Entry NewEntry(ulong obj)
            {
                Entry result = new Entry() { Start = obj, End = obj, Dictionary = new Dictionary<ulong, int>() };
                _entries.Add(result);
                return result;
            }

            class Entry
            {
                public ulong Start;
                public ulong End;
                public Dictionary<ulong, int> Dictionary;
            }
        }
        #endregion
    }
    struct ObjectInfo
    {
        public ClrType Type;
        public uint RefOffset;
        public uint RefCount;

        public override string ToString()
        {
            return $"{Type.Name} refs: {RefCount:n0}";
        }
    }
}
