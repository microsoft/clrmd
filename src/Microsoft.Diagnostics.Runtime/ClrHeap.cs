// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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

        internal ClrHeap(ClrRuntime runtime, IMemoryReader memoryReader, IClrHeapHelpers helpers)
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
        /// Enumerates objects within the given memory range.
        /// </summary>
        public IEnumerable<ClrObject> EnumerateObjects(MemoryRange range)
        {
            foreach (ClrSegment seg in Segments)
            {
                if (!range.Overlaps(seg.ObjectRange))
                    continue;

                ulong start = seg.FirstObjectAddress;
                if (seg.ObjectRange.Contains(range.Start))
                    start = range.Start;

                foreach (ClrObject obj in EnumerateObjects(seg, start))
                {
                    if (range.Contains(obj.Address))
                        yield return obj;
                    else if (range.End < obj)
                        break;
                }
            }
        }

        internal IEnumerable<ClrObject> EnumerateObjects(ClrSegment segment)
        {
            return EnumerateObjects(segment, segment.FirstObjectAddress);
        }

        internal IEnumerable<ClrObject> EnumerateObjects(ClrSegment segment, ulong startAddress)
        {
            if (!segment.ObjectRange.Contains(startAddress))
                yield break;

            ulong markerStep = GetMarkerStep(segment);

            uint currStep = (uint)((startAddress - segment.FirstObjectAddress) / markerStep);

            bool isLargeOrPinned = segment.Kind == GCSegmentKind.Large || segment.Kind == GCSegmentKind.Pinned;
            uint minObjSize = (uint)IntPtr.Size * 3;


            ulong obj;
            if (startAddress == segment.FirstObjectAddress)
                obj = segment.FirstObjectAddress;
            else
                obj = segment.ObjectMarkers.Select(m => segment.FirstObjectAddress + m).Where(m => m <= startAddress).Max();

            // C# isn't smart enough to understand that !large means memoryReader is non-null.  We will just be
            // careful here.
            using MemoryReader memoryReader = (!isLargeOrPinned ? new MemoryReader(_memoryReader, 0x10000) : null)!;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(IntPtr.Size * 2 + sizeof(uint));

            // The large object heap
            if (!isLargeOrPinned)
                memoryReader.EnsureRangeInCache(obj);

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
                ClrObject result = new(obj, type);
                yield return result;
                if (type is null)
                    break;

                ulong segmentOffset = obj - segment.ObjectRange.Start;
                if (segmentOffset >= (currStep + 1) * markerStep && currStep < segment.ObjectMarkers.Length && segmentOffset <= uint.MaxValue)
                    segment.ObjectMarkers[currStep++] = (uint)segmentOffset;

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

        private static ulong GetMarkerStep(ClrSegment segment)
        {
            ulong markerStep;
            if (segment.ObjectMarkers.Length == 0)
                markerStep = segment.ObjectRange.Length + 1;
            else
                markerStep = segment.ObjectRange.Length / ((uint)segment.ObjectMarkers.Length + 2);

            return markerStep;
        }

        /// <summary>
        /// Finds the next ClrObject on the given segment.
        /// </summary>
        /// <param name="address">An address on any ClrSegment.</param>
        /// <returns>An invalid ClrObject if address doesn't lie on any segment or if no objects exist after the given address on a segment.</returns>
        public ClrObject FindNextObjectOnSegment(ulong address)
        {
            ClrSegment? seg = GetSegmentByAddress(address);
            if (seg is null)
                return default;

            ulong start = seg.ObjectMarkers.Select(m => seg.FirstObjectAddress + m).Where(m => m <= address).Max();
            foreach (ClrObject obj in EnumerateObjects(seg, start))
                if (address < obj)
                    return obj;

            return default;
        }

        /// <summary>
        /// Finds the previous object on 
        /// </summary>
        /// <param name="address">An address on any ClrSegment.</param>
        /// <returns>An invalid ClrObject if address doesn't lie on any segment or if address is the first object on a segment.</returns>
        public ClrObject FindPreviousObjectOnSegment(ulong address)
        {
            // TODO: This is a temporary implementation.  ClrHeap needs to maintain a jumplist to help find addresses, but
            // that wasn't done in this checkin.
            ClrSegment? seg = GetSegmentByAddress(address);
            if (seg is null || address <= seg.FirstObjectAddress)
                return default;

            ulong start = seg.ObjectMarkers.Select(m => seg.FirstObjectAddress + m).Where(m => m < address).Max();

            ClrObject last = default;
            foreach (ClrObject obj in EnumerateObjects(seg, start))
            {
                if (obj >= address)
                    return last;

                last = obj;
            }

            return default;
        }

        private ulong SkipAllocationContext(ClrSegment seg, ulong address)
        {
            if (seg.Kind == GCSegmentKind.Large || seg.Kind == GCSegmentKind.Frozen)
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

            if (seg.Kind == GCSegmentKind.Large || seg.Kind == GCSegmentKind.Pinned)
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
                        yield return new ClrHandle(handle.AppDomain, address, m_userObject, handle.HandleKind);

                        ClrElementType? arrayElementType = m_userObject.Type?.ComponentType?.ElementType;
                        if (m_userObject.IsArray && arrayElementType.HasValue && arrayElementType.Value.IsObjectReference())
                        {
                            ClrArray array = m_userObject.AsArray();
                            for (int i = 0; i < array.Length; i++)
                            {
                                ulong innerAddress = m_userObject + (ulong)(2 * IntPtr.Size + i * IntPtr.Size);
                                ClrObject innerObj = array.GetObjectValue(i);

                                if (innerObj.IsValid)
                                    yield return new ClrHandle(handle.AppDomain, innerAddress, innerObj, handle.HandleKind);
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
                Interlocked.CompareExchange(ref _syncBlocks, container, null);
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

            SyncBlock? syncBlk = GetSyncBlock(obj);
            SyncBlockComFlags flags = syncBlk?.ComFlags ?? SyncBlockComFlags.None;
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

                        bool large = seg.Kind == GCSegmentKind.Large || seg.Kind == GCSegmentKind.Pinned;
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

                        bool large = seg.Kind == GCSegmentKind.Large || seg.Kind == GCSegmentKind.Pinned;
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

                        bool large = seg.Kind == GCSegmentKind.Large || seg.Kind == GCSegmentKind.Pinned;
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
                data = new(_helpers.GetSubHeaps(this));
                Interlocked.CompareExchange(ref _subHeapData, data, null);
            }

            return data;
        }

        public ClrType? GetTypeByMethodTable(ulong methodTable) => _typeFactory.GetOrCreateType(methodTable, 0);


        public ClrType? GetTypeByName(string name) => Runtime.EnumerateModules().OrderBy(m => m.Name ?? "").Select(m => GetTypeByName(m, name)).Where(r => r != null).FirstOrDefault();

        public ClrType? GetTypeByName(ClrModule module, string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw new ArgumentException($"{nameof(name)} cannot be empty");

            // First, look for already constructed types and see if their name matches.
            List<ulong> lookup = new(256);
            foreach ((ulong mt, _) in module.EnumerateTypeDefToMethodTableMap())
            {
                ClrType? type = _typeFactory.TryGetType(mt);
                if (type is null)
                    lookup.Add(mt);
                else if (type.Name == name)
                    return type;
            }

            // Since we didn't find pre-constructed types matching, look up the names for all
            // remaining types without constructing them until we find the right one.
            foreach (ulong mt in lookup)
            {
                string? typeName = _typeFactory.GetTypeName(mt);
                if (typeName == name)
                    return _typeFactory.GetOrCreateType(mt, 0);
            }

            return null;
        }

        internal ClrException? GetExceptionObject(ulong objAddress, ClrThread? thread)
        {
            if (objAddress == 0)
                return null;

            ClrObject obj = GetObject(objAddress);
            if (obj.IsValid && !obj.IsException)
                return null;
            return new ClrException(obj.Type?.Helpers ?? FreeType.Helpers, thread, obj);
        }

        private class SubHeapData
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
}
