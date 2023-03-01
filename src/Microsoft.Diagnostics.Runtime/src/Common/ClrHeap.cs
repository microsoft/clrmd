// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of the CLR heap.
    /// </summary>
    public sealed class ClrHeap
    {
        private const int MaxGen2ObjectSize = 85000;

        private readonly IClrTypeFactory _typeFactory;
        private readonly IMemoryReader _memoryReader;
        private readonly IClrHeapHelpers _helpers;
        private volatile Dictionary<ulong, ulong>? _allocationContexts;
        private volatile (ulong Source, ulong Target)[]? _dependentHandles;
        private volatile SyncBlockContainer? _syncBlocks;
        private volatile ClrSegment? _currSegment;
        private volatile SubHeapData? _subHeapData;
        private ulong _lastComFlags;

        public ClrHeap(ClrRuntime runtime, IMemoryReader memoryReader, IClrHeapHelpers helpers)
        {
            Runtime = runtime;
            _memoryReader = memoryReader;
            _helpers = helpers;

            _typeFactory = helpers.CreateTypeFactory(this);
            FreeType = _typeFactory.FreeType;
            ObjectType = _typeFactory.ObjectType;
            StringType = _typeFactory.StringType;
            ExceptionType = _typeFactory.ExceptionType;
        }

        /// <summary>
        /// Gets the runtime associated with this heap.
        /// </summary>
        public ClrRuntime Runtime { get; }

        /// <summary>
        /// Returns true if the GC heap is in a consistent state for heap enumeration.  This will return false
        /// if the process was stopped in the middle of a GC, which can cause the GC heap to be unwalkable.
        /// Note, you may still attempt to walk the heap if this function returns false, but you will likely
        /// only be able to partially walk each segment.
        /// </summary>
        public bool CanWalkHeap => _helpers.AreGCStructuresValid;

        /// <summary>
        /// Returns the number of logical heaps in the process.
        /// </summary>
        public ImmutableArray<ClrSubHeap> SubHeaps => GetSubHeapData().SubHeaps;

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.
        /// </summary>
        public ImmutableArray<ClrSegment> Segments => GetSubHeapData().Segments;

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing free space on the GC heap.
        /// </summary>
        public ClrType FreeType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="string"/>.
        /// </summary>
        public ClrType StringType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="object"/>.
        /// </summary>
        public ClrType ObjectType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="System.Exception"/>.
        /// </summary>
        public ClrType ExceptionType { get; }

        /// <summary>
        /// Gets a value indicating whether the GC heap is in Server mode.
        /// </summary>
        public bool IsServer => _helpers.IsServerMode;

        /// <summary>
        /// Gets a <see cref="ClrObject"/> for the given address on this heap.
        /// </summary>
        /// <remarks>
        /// The returned object will have a <see langword="null"/> <see cref="ClrObject.Type"/> if objRef does not point to
        /// a valid managed object.
        /// </remarks>
        /// <param name="objRef"></param>
        /// <returns></returns>
        public ClrObject GetObject(ulong objRef) => new(objRef, GetObjectType(objRef));

        /// <summary>
        /// Obtains the type of an object at the given address.  Returns <see langword="null"/> if objRef does not point to
        /// a valid managed object.
        /// </summary>
        public ClrType? GetObjectType(ulong objRef)
        {
            ulong mt = _memoryReader.ReadPointer(objRef);

            if (mt == 0)
                return null;

            return _typeFactory.GetOrCreateType(mt, objRef);
        }

        /// <summary>
        /// Enumerates all objects on the heap.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public IEnumerable<ClrObject> EnumerateObjects()
        {
            foreach (ClrSegment segment in Segments)
                foreach (ClrObject obj in EnumerateObjects(segment))
                    yield return obj;
        }

        /// <summary>
        /// Enumerates all objects on the given segment.
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public IEnumerable<ClrObject> EnumerateObjects(ClrSegment segment)
        {
            bool isLargeOrPinned = segment.Kind == SegmentKind.Large || segment.Kind == SegmentKind.Pinned;
            uint minObjSize = (uint)IntPtr.Size * 3;
            ulong obj = segment.FirstObjectAddress;

            // C# isn't smart enough to understand that !large means memoryReader is non-null.  We will just be
            // careful here.
            using MemoryReader memoryReader = (!isLargeOrPinned ? new MemoryReader(_memoryReader, 0x10000) : null)!;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(IntPtr.Size * 2 + sizeof(uint));

            // The large object heap
            if (!isLargeOrPinned)
                memoryReader.EnsureRangeInCache(obj);

            int lastMarker = -1;
            while (segment.ObjectRange.Contains(obj))
            {
                ulong mt;
                if (isLargeOrPinned)
                {
                    if (_memoryReader.Read(obj, buffer) != buffer.Length)
                        break;

                    mt = Unsafe.As<byte, nuint>(ref buffer[0]);
                }
                else
                {
                    if (!memoryReader.ReadPtr(obj, out mt))
                        break;
                }

                ClrType? type = _typeFactory.GetOrCreateType(mt, obj);
                if (type is null)
                    break;

                int marker = segment.GetMarkerIndex(obj);
                if (marker != lastMarker)
                {
                    segment.SetMarkerIndex(marker, obj);
                    lastMarker = marker;
                }

                ClrObject result = new(obj, type);
                yield return result;

                ulong size;
                if (type.ComponentSize == 0)
                {
                    size = (uint)type.StaticSize;
                }
                else
                {
                    uint count;
                    if (isLargeOrPinned)
                        count = Unsafe.As<byte, uint>(ref buffer[IntPtr.Size]);
                    else
                        memoryReader.ReadDword(obj + (uint)IntPtr.Size, out count);

                    // Strings in v4+ contain a trailing null terminator not accounted for.
                    if (StringType == type)
                        count++;

                    size = count * (ulong)type.ComponentSize + (ulong)type.StaticSize;
                }

                size = Align(size, segment);
                if (size < minObjSize)
                    size = minObjSize;

                obj += size;
                obj = SkipAllocationContext(segment, obj);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }

        private ulong SkipAllocationContext(ClrSegment seg, ulong address)
        {
            if (seg.Kind == SegmentKind.Large || seg.Kind == SegmentKind.Frozen)
                return address;

            var allocationContexts = GetAllocationContexts();

            uint minObjSize = (uint)IntPtr.Size * 3;
            while (allocationContexts.TryGetValue(address, out ulong nextObj))
            {
                nextObj += Align(minObjSize, seg);

                if (address >= nextObj || address >= seg.End)
                    return 0;

                // Only if there's data corruption:
                if (address >= nextObj || address >= seg.End)
                    return 0;

                address = nextObj;
            }

            return address;
        }

        private static ulong Align(ulong size, ClrSegment seg)
        {
            ulong AlignConst;
            ulong AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (seg.Kind == SegmentKind.Large || seg.Kind == SegmentKind.Pinned)
                return (size + AlignLargeConst) & ~AlignLargeConst;

            return (size + AlignConst) & ~AlignConst;
        }

        /// <summary>
        /// Enumerates all roots in the process.  Equivalent to the combination of:
        ///     ClrRuntime.EnumerateHandles().Where(handle => handle.IsStrong)
        ///     ClrRuntime.EnumerateThreads().SelectMany(thread => thread.EnumerateStackRoots())
        ///     ClrHeap.EnumerateFinalizerRoots()
        /// </summary>
        public IEnumerable<IClrRoot> EnumerateRoots()
        {
            // Handle table
            foreach (ClrHandle handle in Runtime.EnumerateHandles())
            {
                if (handle.IsStrong)
                    yield return handle;

                if (handle.RootKind == ClrRootKind.AsyncPinnedHandle && handle.Object.IsValid)
                {
                    (ulong address, ClrObject m_userObject) = GetObjectAndAddress(handle.Object, "m_userObject");

                    if (address != 0 && m_userObject.IsValid)
                    {
                        yield return new ClrmdHandle(handle.AppDomain, address, m_userObject, handle.HandleKind);

                        ClrElementType? arrayElementType = m_userObject.Type?.ComponentType?.ElementType;
                        if (m_userObject.IsArray && arrayElementType.HasValue && arrayElementType.Value.IsObjectReference())
                        {
                            ClrArray array = m_userObject.AsArray();
                            for (int i = 0; i < array.Length; i++)
                            {
                                ulong innerAddress = m_userObject + (ulong)(2 * IntPtr.Size + i * IntPtr.Size);
                                ClrObject innerObj = array.GetObjectValue(i);

                                if (innerObj.IsValid)
                                    yield return new ClrmdHandle(handle.AppDomain, innerAddress, innerObj, handle.HandleKind);
                            }
                        }
                    }
                }
            }

            // Finalization Queue
            foreach (ClrFinalizerRoot root in EnumerateFinalizerRoots())
                yield return root;

            // Threads
            foreach (ClrThread thread in Runtime.Threads.Where(t => t.IsAlive))
                foreach (IClrRoot root in thread.EnumerateStackRoots())
                    yield return root;
        }

        private (ulong Address, ClrObject obj) GetObjectAndAddress(ClrObject containing, string fieldName)
        {
            if (containing.IsValid)
            {
                ClrInstanceField? field = containing.Type?.Fields.FirstOrDefault(f => f.Name == fieldName);
                if (field != null && field.Offset > 0)
                {
                    ulong address = field.GetAddress(containing.Address);
                    ulong objPtr = _memoryReader.ReadPointer(address);
                    ClrObject obj = GetObject(objPtr);

                    if (obj.IsValid)
                        return (address, obj);
                }
            }

            return (0ul, default);
        }

        /// <summary>
        /// Returns the GC segment which contains the given address.  This only searches ClrSegment.ObjectRange.
        /// </summary>
        public ClrSegment? GetSegmentByAddress(ulong address)
        {
            if (Segments.Length == 0)
                return null;

            ClrSegment? curr = _currSegment;
            if (curr is not null && curr.ObjectRange.Contains(address))
                return curr;

            if (Segments[0].FirstObjectAddress <= address && address < Segments[Segments.Length - 1].End)
            {
                int index = Segments.Search(address, (seg, value) => seg.ObjectRange.CompareTo(value));
                if (index == -1)
                    return null;

                curr = Segments[index];
                _currSegment = curr;
                return curr;
            }

            return null;
        }

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        public IEnumerable<ClrObject> EnumerateFinalizableObjects() => EnumerateFinalizers(SubHeaps.Select(heap => heap.FinalizerQueueObjects)).Select(f => f.Object);

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        public IEnumerable<ClrFinalizerRoot> EnumerateFinalizerRoots() => EnumerateFinalizers(SubHeaps.Select(heap => heap.FinalizerQueueRoots));

        /// <summary>
        /// Enumerates all AllocationContexts for all segments.  Allocation contexts are locations on the GC
        /// heap which the GC uses to allocate new objects.  These regions of memory do not contain objects.
        /// AllocationContexts are the reason that you cannot simply enumerate the heap by adding each object's
        /// size to itself to get the next object on the segment, since if the address is an allocation context
        /// you will have to skip past it to find the next valid object.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MemoryRange> EnumerateAllocationContexts()
        {
            var allocationContexts = GetAllocationContexts();
            if (allocationContexts is not null)
                foreach (var kv in allocationContexts)
                    yield return new(kv.Key, kv.Value);
        }

        /// <summary>
        /// Obtains the SyncBlock data for a given object, if the object has an associated SyncBlock.
        /// </summary>
        /// <param name="obj">The object to get SyncBlock data for.</param>
        /// <returns>The SyncBlock for the object, null if the object does not have one.</returns>
        public SyncBlock? GetSyncBlock(ulong obj)
        {
            SyncBlockContainer syncBlocks = GetSyncBlocks();

            if (syncBlocks.EmptySyncBlocks.Contains(obj))
                return new(obj);

            int index = syncBlocks.SyncBlocks.Search(obj, (x, y) => x.Object.CompareTo(y));
            if (index != -1)
                return syncBlocks.SyncBlocks[index];

            return null;
        }

        private SyncBlockContainer GetSyncBlocks()
        {
            SyncBlockContainer? container = _syncBlocks;
            if (container is null)
            {
                container = new SyncBlockContainer(_helpers.EnumerateSyncBlocks());
                Interlocked.CompareExchange(ref container, container, null);
            }

            return container;
        }

        /// <summary>
        /// Returns a string representation of this heap, including the size and number of segments.
        /// </summary>
        /// <returns>The string representation of this heap.</returns>
        public override string ToString()
        {
            long size = Segments.Sum(s => (long)s.Length);
            return $"GC Heap: {size.ConvertToHumanReadable()}, {Segments.Length} segments";
        }

        /// <summary>
        /// This is an implementation helper.  Use ClrObject.IsComCallWrapper and ClrObject.IsRuntimeCallWrapper instead.
        /// </summary>
        public SyncBlockComFlags GetComFlags(ulong obj)
        {
            if (obj == 0)
                return SyncBlockComFlags.None;

            const ulong mask = ~0xe000000000000000;
            ulong lastComFlags = _lastComFlags;
            if ((lastComFlags & mask) == obj)
                return (SyncBlockComFlags)(lastComFlags >> 61);

            SyncBlockComFlags flags = GetComFlags(obj);
            _lastComFlags = ((ulong)flags << 61) | (obj & mask);
            return flags;
        }

        /// <summary>
        /// This is an implementation helper.  Use ClrObject.Size instead.
        /// </summary>
        public ulong GetObjectSize(ulong objRef, ClrType type)
        {
            ulong size;
            if (type.ComponentSize == 0)
            {
                size = (uint)type.StaticSize;
            }
            else
            {
                uint countOffset = (uint)IntPtr.Size;
                ulong loc = objRef + countOffset;

                uint count = _memoryReader.Read<uint>(loc);

                // Strings in v4+ contain a trailing null terminator not accounted for.
                if (StringType == type)
                    count++;

                size = count * (ulong)type.ComponentSize + (ulong)type.StaticSize;
            }

            uint minSize = (uint)IntPtr.Size * 3;
            if (size < minSize)
                size = minSize;
            return size;
        }

        /// <summary>
        /// This is an implementation helper.  Use <see cref="ClrObject.EnumerateReferences(bool, bool)">ClrObject.EnumerateReferences</see> instead.
        /// Enumerates all objects that the given object references.  This method is meant for internal use to
        /// implement ClrObject.EnumerateReferences, which you should use instead of calling this directly.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="considerDependantHandles">Whether to consider dependant handle mappings.</param>
        /// <param name="carefully">
        /// Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)
        /// </param>
        public IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                var dependent = GetDependentHandles();
                
                if (dependent.Length > 0)
                {
                    int index = dependent.Search(obj, (x, y) => x.Source.CompareTo(y));
                    if (index != -1)
                    {
                        while (index >= 1 && dependent[index - 1].Source == obj)
                            index--;

                        while (index < dependent.Length && dependent[index].Source == obj)
                        {
                            ulong dependantObj = dependent[index++].Target;
                            yield return new(dependantObj, GetObjectType(dependantObj));
                        }
                    }
                }
            }

            if (type.IsCollectible)
            {
                ulong la = _memoryReader.ReadPointer(type.LoaderAllocatorHandle);
                if (la != 0)
                    yield return new(la, GetObjectType(la));
            }

            if (type.ContainsPointers)
            {
                GCDesc gcdesc = type.GCDesc;
                if (!gcdesc.IsEmpty)
                {
                    ulong size = GetObjectSize(obj, type);
                    if (carefully)
                    {
                        ClrSegment? seg = GetSegmentByAddress(obj);
                        if (seg is null)
                            yield break;

                        bool large = seg.Kind == SegmentKind.Large || seg.Kind == SegmentKind.Pinned;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                    if (read > IntPtr.Size)
                    {
                        foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                            yield return new(reference, GetObjectType(reference));
                    }
                    
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        /// <summary>
        /// This is an implementation helper.
        /// Enumerates all objects that the given object references.  This method is meant for internal use to
        /// implement ClrObject.EnumerateReferencesWithFields, which you should use instead of calling this directly.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="considerDependantHandles">Whether to consider dependant handle mappings.</param>
        /// <param name="carefully">
        /// Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)
        /// </param>
        public IEnumerable<ClrReference> EnumerateReferencesWithFields(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                var dependent = GetDependentHandles();

                if (dependent.Length > 0)
                {
                    int index = dependent.Search(obj, (x, y) => x.Source.CompareTo(y));
                    if (index != -1)
                    {
                        while (index >= 1 && dependent[index - 1].Source == obj)
                            index--;

                        while (index < dependent.Length && dependent[index].Source == obj)
                        {
                            ulong dependantObj = dependent[index++].Target;
                            ClrObject target = new(dependantObj, GetObjectType(dependantObj));
                            yield return ClrReference.CreateFromDependentHandle(target);
                        }
                    }
                }
            }

            if (type.ContainsPointers)
            {
                GCDesc gcdesc = type.GCDesc;
                if (!gcdesc.IsEmpty)
                {
                    ulong size = GetObjectSize(obj, type);
                    if (carefully)
                    {
                        ClrSegment? seg = GetSegmentByAddress(obj);
                        if (seg is null)
                            yield break;

                        bool large = seg.Kind == SegmentKind.Large || seg.Kind == SegmentKind.Pinned;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                    if (read > IntPtr.Size)
                    {
                        foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                        {
                            ClrObject target = new(reference, GetObjectType(reference));

                            DebugOnly.Assert(offset >= IntPtr.Size);
                            yield return ClrReference.CreateFromFieldOrArray(target, type, offset - IntPtr.Size);
                        }
                    }
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        /// <summary>
        /// This is an implementation helper.
        /// Enumerates all objects that the given object references.  This method is meant for internal use to
        /// implement ClrObject.EnumerateReferenceAddresses which you should use instead of calling this directly.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="considerDependantHandles">Whether to consider dependant handle mappings.</param>
        /// <param name="carefully">
        /// Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)
        /// </param>
        public IEnumerable<ulong> EnumerateReferenceAddresses(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {

            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                var dependent = GetDependentHandles();

                if (dependent.Length > 0)
                {
                    int index = dependent.Search(obj, (x, y) => x.Source.CompareTo(y));
                    if (index != -1)
                    {
                        while (index >= 1 && dependent[index - 1].Source == obj)
                            index--;

                        while (index < dependent.Length && dependent[index].Source == obj)
                            yield return dependent[index++].Target;
                    }
                }
            }

            if (type.ContainsPointers)
            {
                GCDesc gcdesc = type.GCDesc;
                if (!gcdesc.IsEmpty)
                {
                    ulong size = GetObjectSize(obj, type);
                    if (carefully)
                    {
                        ClrSegment? seg = GetSegmentByAddress(obj);
                        if (seg is null)
                            yield break;

                        bool large = seg.Kind == SegmentKind.Large || seg.Kind == SegmentKind.Pinned;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                    if (read > IntPtr.Size)
                    {
                        foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                        {
                            yield return reference;
                            DebugOnly.Assert(offset >= IntPtr.Size);
                        }
                    }
                }
            }
        }

        private Dictionary<ulong, ulong> GetAllocationContexts()
        {
            Dictionary<ulong, ulong>? result = _allocationContexts;
            if (result is not null)
                return result;

            result = new();
            foreach (MemoryRange allocContext in _helpers.EnumerateThreadAllocationContexts())
                result[allocContext.Start] = allocContext.End;

            foreach (ClrSubHeap subHeap in SubHeaps)
                if (subHeap.AllocationContext.Start < subHeap.AllocationContext.End)
                    result[subHeap.AllocationContext.Start] = subHeap.AllocationContext.End;

            Interlocked.CompareExchange(ref _allocationContexts, result, null);
            return result;
        }

        private (ulong Source, ulong Target)[] GetDependentHandles()
        {
            (ulong Source, ulong Target)[]? handles = _dependentHandles;
            if (handles is not null)
                return handles;

            handles = _helpers.EnumerateDependentHandles().OrderBy(r => r.Source).ToArray();

            Interlocked.CompareExchange(ref _dependentHandles, handles, null);
            return handles;
        }

        private IEnumerable<ClrFinalizerRoot> EnumerateFinalizers(IEnumerable<MemoryRange> memoryRanges)
        {
            foreach (MemoryRange seg in memoryRanges)
            {
                for (ulong ptr = seg.Start; ptr < seg.End; ptr += (uint)IntPtr.Size)
                {
                    ulong obj = _memoryReader.ReadPointer(ptr);
                    if (obj == 0)
                        continue;

                    ulong mt = _memoryReader.ReadPointer(obj);
                    ClrType? type = _typeFactory.GetOrCreateType(mt, obj);
                    if (type != null)
                        yield return new ClrFinalizerRoot(ptr, new ClrObject(obj, type));
                }
            }
        }

        private SubHeapData GetSubHeapData()
        {
            SubHeapData? data = _subHeapData;
            if (data is null)
            {
                data = new(_helpers.GetSubHeaps());
                Interlocked.CompareExchange(ref _subHeapData, data, null);
            }

            return data;
        }

        public ClrType? GetTypeByMethodTable(ulong methodTable) => _typeFactory.GetOrCreateType(methodTable, 0);

        class SubHeapData
        {
            public ImmutableArray<ClrSubHeap> SubHeaps { get; }
            public ImmutableArray<ClrSegment> Segments { get; }

            public SubHeapData(ImmutableArray<ClrSubHeap> subheaps)
            {
                SubHeaps = subheaps;
                Segments = subheaps.SelectMany(s => s.Segments).OrderBy(s => s.FirstObjectAddress).ToImmutableArray();
            }
        }

        private class SyncBlockContainer
        {
            public SyncBlockContainer()
            {
                SyncBlocks = Array.Empty<ComSyncBlock>();
            }

            public SyncBlockContainer(IEnumerable<SyncBlock> syncBlocks)
            {
                SyncBlocks = syncBlocks.Where(FilterEmpty).OrderBy(b => b.Object).ToArray();
            }

            private bool FilterEmpty(SyncBlock syncBlock)
            {
                if (syncBlock.GetType() == typeof(SyncBlock))
                {
                    EmptySyncBlocks.Add(syncBlock.Object);
                    return false;
                }

                return true;
            }

            public SyncBlock[] SyncBlocks { get; }
            public HashSet<ulong> EmptySyncBlocks { get; } = new();
        }
    }

    public interface IClrHeapHelpers
    {
        IClrTypeFactory CreateTypeFactory(ClrHeap heap);

        bool IsServerMode { get; }
        bool AreGCStructuresValid { get; }
        IEnumerable<MemoryRange> EnumerateThreadAllocationContexts();
        IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles();
        IEnumerable<SyncBlock> EnumerateSyncBlocks();
        ImmutableArray<ClrSubHeap> GetSubHeaps();
        IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap);
    }

    internal sealed class ClrHeapHelpers : IClrHeapHelpers
    {
        private readonly ClrDataProcess _clrDataProcess;
        private readonly SOSDac _sos;
        private readonly SOSDac8? _sos8;
        private readonly SOSDac12? _sos12;
        private readonly IMemoryReader _memoryReader;
        private readonly CacheOptions _cacheOptions;
        private readonly GCInfo _gcInfo;

        public bool IsServerMode => _gcInfo.ServerMode != 0;
        public bool AreGCStructuresValid => _gcInfo.GCStructuresValid != 0;
        public ulong SizeOfPlugAndGap { get; }

        public ClrHeapHelpers(ClrDataProcess clrDataProcess, SOSDac sos, SOSDac8? sos8, SOSDac12? sos12, IMemoryReader reader, CacheOptions cacheOptions)
        {
            _clrDataProcess = clrDataProcess;
            _sos = sos;
            _sos8 = sos8;
            _sos12 = sos12;
            _memoryReader = reader;
            _cacheOptions = cacheOptions;
            SizeOfPlugAndGap = (ulong)_memoryReader.PointerSize * 4;

            if (!_sos.GetGCHeapData(out _gcInfo))
                _gcInfo = default; // Ensure _gcInfo.GCStructuresValid == false.
        }

        public IClrTypeFactory CreateTypeFactory(ClrHeap heap) => new ClrTypeFactory(heap, _clrDataProcess, _sos, _cacheOptions);

        public IEnumerable<MemoryRange> EnumerateThreadAllocationContexts()
        {
            if (_sos12 is not null && _sos12.GetGlobalAllocationContext(out ulong allocPointer, out ulong allocLimit))
            {
                if (allocPointer < allocLimit)
                    yield return new(allocPointer, allocLimit);
            }

            if (!_sos.GetThreadStoreData(out ThreadStoreData threadStore))
                yield break;

            ulong address = threadStore.FirstThread;
            for (int i = 0; i < threadStore.ThreadCount && address != 0; i++)
            {
                if (!_sos.GetThreadData(address, out ThreadData thread))
                    break;

                if (thread.AllocationContextPointer < thread.AllocationContextLimit)
                    yield return new(thread.AllocationContextPointer, thread.AllocationContextLimit);

                address = thread.NextThread;
            }
        }

        public IEnumerable<(ulong Source, ulong Target)> EnumerateDependentHandles()
        {
            using SOSHandleEnum? handleEnum = _sos.EnumerateHandles(ClrHandleKind.Dependent);
            if (handleEnum is null)
                yield break;

            HandleData[] handles;
            try
            {
                // Yes this is a huge array.  Older versions of ISOSHandleEnum have a memory leak when
                // we loop below.  If we can fill the array without having to call back into
                // SOSHandleEnum.ReadHandles then we avoid that leak entirely.
                handles = new HandleData[0x18000];
            }
            catch (OutOfMemoryException)
            {
                handles = new HandleData[256];
            }
            
            int fetched;
            while ((fetched = handleEnum.ReadHandles(handles)) != 0)
            {
                for (int i = 0; i < fetched; i++)
                {
                    if (handles[i].Type == (int)ClrHandleKind.Dependent)
                    {
                        ulong obj = _memoryReader.ReadPointer(handles[i].Handle);
                        if (obj != 0)
                            yield return (obj, handles[i].Secondary);
                    }
                }
            }
        }

        public IEnumerable<SyncBlock> EnumerateSyncBlocks()
        {
            HResult hr = _sos.GetSyncBlockData(1, out SyncBlockData data);
            if (!hr || data.TotalSyncBlockCount == 0)
                yield break;

            int max = data.TotalSyncBlockCount >= int.MaxValue ? int.MaxValue : (int)data.TotalSyncBlockCount;

            int curr = 1;
            do
            {
                if (data.Free == 0)
                {
                    if (data.MonitorHeld != 0 || data.HoldingThread != 0 || data.Recursion != 0 || data.AdditionalThreadCount != 0)
                        yield return new FullSyncBlock(data);
                    else if (data.COMFlags != 0)
                        yield return new ComSyncBlock(data.Object, data.COMFlags);
                    else
                        yield return new SyncBlock(data.Object);
                }

                curr++;
                if (curr > max)
                    break;

                hr = _sos.GetSyncBlockData(curr, out data);
            } while (hr);
        }

        public ImmutableArray<ClrSubHeap> GetSubHeaps()
        {
            if (IsServerMode)
            {
                ClrDataAddress[] heapAddresses = _sos.GetHeapList(_gcInfo.HeapCount);
                var heapsBuilder = ImmutableArray.CreateBuilder<ClrSubHeap>(heapAddresses.Length);
                for (int i = 0; i < heapAddresses.Length; i++)
                {
                    if (_sos.GetServerHeapDetails(heapAddresses[i], out HeapDetails heapData))
                    {
                        GenerationData[] genData = heapData.GenerationTable;
                        ClrDataAddress[] finalization = heapData.FinalizationFillPointers;

                        if (_sos8 is not null)
                        {
                            genData = _sos8.GetGenerationTable(heapAddresses[i]) ?? genData;
                            finalization = _sos8.GetFinalizationFillPointers(heapAddresses[i]) ?? finalization;
                        }

                        heapsBuilder.Add(new(this, i, heapAddresses[i], heapData, genData, finalization.Select(addr => (ulong)addr)));
                    }
                }

                return heapsBuilder.MoveToImmutable();
            }
            else
            {
                if (_sos.GetWksHeapDetails(out HeapDetails heapData))
                {
                    GenerationData[] genData = heapData.GenerationTable;
                    ClrDataAddress[] finalization = heapData.FinalizationFillPointers;

                    if (_sos8 is not null)
                    {
                        genData = _sos8.GetGenerationTable() ?? genData;
                        finalization = _sos8.GetFinalizationFillPointers() ?? finalization;
                    }

                    return ImmutableArray.Create(new ClrSubHeap(this, 0, 0, heapData, genData, finalization.Select(addr => (ulong)addr)));
                }
            }

            return ImmutableArray<ClrSubHeap>.Empty;
        }

        public IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap)
        {
            HashSet<ulong> seen = new() { 0 };
            IEnumerable<ClrSegment> segments = EnumerateSegments(heap, 3, seen);
            segments = segments.Concat(EnumerateSegments(heap, 2, seen));
            if (heap.HasRegions)
            {
                segments = segments.Concat(EnumerateSegments(heap, 1, seen));
                segments = segments.Concat(EnumerateSegments(heap, 0, seen));
            }

            if (heap.GenerationTable.Length > 4)
                segments = segments.Concat(EnumerateSegments(heap, 4, seen));

            return segments;
        }

        private IEnumerable<ClrSegment> EnumerateSegments(ClrSubHeap heap, int generation, HashSet<ulong> seen)
        {
            ulong address = heap.GenerationTable[generation].StartSegment;

            while (address != 0 && seen.Add(address))
            {
                ClrSegment? segment = CreateSegment(heap, address, generation);

                if (segment is null)
                    break;

                yield return segment;
                address = segment.Next;
            }
        }

        private ClrSegment? CreateSegment(ClrSubHeap subHeap, ulong address, int generation)
        {
            const nint heap_segment_flags_readonly = 1;

            if (!_sos.GetSegmentData(address, out SegmentData data))
                return null;

            bool ro = (data.Flags & heap_segment_flags_readonly) == heap_segment_flags_readonly;

            SegmentKind kind = SegmentKind.Generation2;
            if (ro)
            {
                kind = SegmentKind.Frozen;
            }
            else if (generation == 3)
            {
                kind = SegmentKind.Large;
            }
            else if (generation == 4)
            {
                kind = SegmentKind.Pinned;
            }
            else
            {
                // We are not a Frozen, Large, or Pinned segment/region:
                if (subHeap.HasRegions)
                {
                    if (generation == 0)
                        kind = SegmentKind.Generation0;
                    else if (generation == 1)
                        kind = SegmentKind.Generation1;
                    else if (generation == 2)
                        kind = SegmentKind.Generation2;
                }
                else
                {
                    if (subHeap.EphemeralHeapSegment == address)
                        kind = SegmentKind.Ephemeral;
                    else
                        kind = SegmentKind.Generation2;
                }
            }

            // The range of memory occupied by allocated objects
            MemoryRange allocated = new(data.Start, subHeap.EphemeralHeapSegment == address ? subHeap.Allocated : (ulong)data.Allocated);

            MemoryRange committed, gen0, gen1, gen2;
            if (subHeap.HasRegions)
            {
                committed = new(allocated.Start - SizeOfPlugAndGap, data.Committed);
                gen0 = default;
                gen1 = default;
                gen2 = default;

                switch (generation)
                {
                    case 0:
                        gen0 = new(allocated.Start, allocated.End);
                        break;

                    case 1:
                        gen1 = new(allocated.Start, allocated.End);
                        break;

                    default:
                        gen2 = new(allocated.Start, allocated.End);
                        break;
                }
            }
            else
            {
                committed = new(allocated.Start, data.Committed);
                if (kind == SegmentKind.Ephemeral)
                {
                    gen0 = new(subHeap.GenerationTable[0].AllocationStart, allocated.End);
                    gen1 = new(subHeap.GenerationTable[1].AllocationStart, gen0.Start);
                    gen2 = new(allocated.Start, gen1.Start);
                }
                else
                {
                    gen0 = default;
                    gen1 = default;
                    gen2 = allocated;
                }
            }

            // The range of memory reserved
            MemoryRange reserved = new(committed.End, data.Reserved);

            return new ClrSegment(_memoryReader)
            {
                Address = data.Address,
                SubHeap = subHeap,
                Kind = kind,
                ObjectRange = allocated,
                CommittedMemory = committed,
                ReservedMemory = reserved,
                Generation0 = gen0,
                Generation1 = gen1,
                Generation2 = gen2,
                Next = data.Next,
            };
        }
    }
}
