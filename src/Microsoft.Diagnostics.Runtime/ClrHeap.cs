// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of the CLR heap.
    /// </summary>
    public sealed class ClrHeap : IClrHeap
    {
        private const uint SyncBlockHashOrSyncBlockIndex = 0x08000000;
        private const uint SyncBlockHashCodeIndex = 0x04000000;
        private const int EnumerateBufferSize = 0x10000;
        private const int MaxGen2ObjectSize = 85000;
        private const int SyncBlockIndexBits = 26;
        private const uint SyncBlockIndexMask = ((1u << SyncBlockIndexBits) - 1u);

        private readonly uint _firstChar = (uint)IntPtr.Size + 4;
        private readonly uint _stringLength = (uint)IntPtr.Size;

        private readonly ClrTypeFactory _typeFactory;
        private readonly IMemoryReader _memoryReader;
        private volatile Dictionary<ulong, ulong>? _allocationContexts;
        private volatile (ulong Source, ulong Target)[]? _dependentHandles;
        private volatile SyncBlockContainer? _syncBlocks;
        private volatile ClrSegment? _currSegment;
        private volatile ArrayPool<ObjectCorruption>? _objectCorruptionPool;
        private ulong _lastComFlags;

        internal ClrHeap(ClrRuntime runtime, IMemoryReader memoryReader, IAbstractHeap helpers, IAbstractTypeHelpers typeHelpers)
        {
            Runtime = runtime;
            _memoryReader = memoryReader;
            Helpers = helpers;

            GCState gcInfo = helpers.State;
            _typeFactory = new(this, typeHelpers, gcInfo);
            FreeType = _typeFactory.CreateFreeType();
            ObjectType = _typeFactory.ObjectType;
            StringType = _typeFactory.CreateStringType();
            ExceptionType = _typeFactory.CreateExceptionType();
            CanWalkHeap = gcInfo.AreGCStructuresValid;
            IsServer = gcInfo.Kind == AbstractDac.GCKind.Server;

            SubHeaps = Helpers.EnumerateSubHeaps().Select(r => new ClrSubHeap(this, r)).ToImmutableArray();
            Segments = SubHeaps.SelectMany(r => r.Segments).OrderBy(r => r.FirstObjectAddress).ToImmutableArray();
            DynamicAdaptationMode = Helpers.GetDynamicAdaptationMode();
        }

        /// <summary>
        /// The DynamicAdaptationMode
        /// </summary>
        public int? DynamicAdaptationMode
        {
            get;
        }

        /// <summary>
        /// An internal only instance of ClrType used to mark that we could not create a valid type...
        /// but we still need to access the properties off of ClrType, such IClrTypeHelpers, IDataReader, etc.
        /// </summary>
        internal ClrType ErrorType => _typeFactory.ErrorType;

        internal IAbstractHeap Helpers { get; }

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
        public bool CanWalkHeap { get; }

        /// <summary>
        /// Returns the number of logical heaps in the process.
        /// </summary>
        public ImmutableArray<ClrSubHeap> SubHeaps { get; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.
        /// </summary>
        public ImmutableArray<ClrSegment> Segments { get; }

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
        public bool IsServer { get; }

        IClrType IClrHeap.ExceptionType => ExceptionType;

        IClrType IClrHeap.FreeType => FreeType;

        IClrType IClrHeap.ObjectType => ObjectType;

        IClrRuntime IClrHeap.Runtime => Runtime;

        ImmutableArray<IClrSegment> IClrHeap.Segments => Segments.CastArray<IClrSegment>();

        IClrType IClrHeap.StringType => StringType;

        ImmutableArray<IClrSubHeap> IClrHeap.SubHeaps => SubHeaps.CastArray<IClrSubHeap>();

        /// <summary>
        /// Gets a <see cref="ClrObject"/> for the given address on this heap.
        /// </summary>
        /// <remarks>
        /// The returned object will have a <see langword="null"/> <see cref="ClrObject.Type"/> if objRef does not point to
        /// a valid managed object.
        /// </remarks>
        /// <param name="objRef">The address of an object.</param>
        /// <returns></returns>
        public ClrObject GetObject(ulong objRef) => new(objRef, GetObjectType(objRef) ?? ErrorType);

        /// <summary>
        /// Gets a <see cref="ClrObject"/> for the given address on this heap.
        /// </summary>
        /// <remarks>
        /// The returned object will have a <see langword="null"/> <see cref="ClrObject.Type"/> if objRef does not point to
        /// a valid managed object.
        /// </remarks>
        /// <param name="objRef">The address of an object.</param>
        /// <param name="type">The type of the object.</param>
        /// <returns></returns>
        public ClrObject GetObject(ulong objRef, ClrType type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return new(objRef, type);
        }

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
        public IEnumerable<ClrObject> EnumerateObjects() => EnumerateObjects(carefully: false);

        /// <summary>
        /// Enumerates all objects on the heap.
        /// </summary>
        /// <param name="carefully">Whether to continue walking objects on a segment where we've encountered
        /// a region of unwalkable memory.  Note that setting carefully = true may significantly increase the
        /// amount of time it takes to walk the heap if we encounter an error.</param>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public IEnumerable<ClrObject> EnumerateObjects(bool carefully)
        {
            foreach (ClrSegment segment in Segments)
                foreach (ClrObject obj in EnumerateObjects(segment, carefully))
                    yield return obj;
        }

        /// <summary>
        /// Enumerates objects within the given memory range.
        /// </summary>
        public IEnumerable<ClrObject> EnumerateObjects(MemoryRange range, bool carefully = false)
        {
            foreach (ClrSegment seg in Segments)
            {
                if (!range.Overlaps(seg.ObjectRange))
                    continue;

                ulong start = seg.FirstObjectAddress;
                if (seg.ObjectRange.Contains(range.Start))
                    start = range.Start;

                foreach (ClrObject obj in EnumerateObjects(seg, start, carefully))
                {
                    if (obj < range.Start)
                        continue;

                    if (obj < range.End)
                        yield return obj;
                    else
                        break;
                }
            }
        }

        internal IEnumerable<ClrObject> EnumerateObjects(ClrSegment segment, bool carefully)
        {
            return EnumerateObjects(segment, segment.FirstObjectAddress, carefully);
        }

        /// <summary>
        /// Deeply verifies an object on the heap.  This goes beyond just ClrObject.IsValid and will
        /// check the object's references as well as certain internal CLR data structures.  Please note,
        /// however, that it is possible to pause a process in a debugger at a point where the heap is
        /// NOT corrupted, but does look inconsistent to ClrMD.  For example, the GC might allocate
        /// an array by writing a method table but the process might be paused before it had the chance
        /// to write the array length onto the heap.  In this case, IsObjectCorrupted may return true
        /// even if the process would have continued on fine.  As a result, this function acts more
        /// like a warning signal that more investigation is needed, and not proof-positive that there
        /// is heap corruption.
        /// </summary>
        /// <param name="objAddr">The address of the object to deeply verify.</param>
        /// <param name="result">Only non-null if this function returns true.  An object which describes the
        /// kind of corruption found.</param>
        /// <returns>True if the object is corrupted in some way, false otherwise.</returns>
        public bool IsObjectCorrupted(ulong objAddr, [NotNullWhen(true)] out ObjectCorruption? result)
        {
            ClrObject obj = GetObject(objAddr);
            ClrSegment? seg = GetSegmentByAddress(objAddr);
            if (seg is null || !seg.ObjectRange.Contains(objAddr))
            {
                result = new(obj, 0, ObjectCorruptionKind.ObjectNotOnTheHeap);
                return true;
            }

            ObjectCorruption[] array = RentObjectCorruptionArray();
            int count = VerifyObject(GetSyncBlocks(), seg, obj, array.AsSpan(0, 1));
            result = count > 0 ? array[0] : null;

            ReturnObjectCorruptionArray(array);
            return count > 0;
        }

        /// <summary>
        /// Deeply verifies an object on the heap.  This goes beyond just ClrObject.IsValid and will
        /// check the object's references as well as certain internal CLR data structures.  Please note,
        /// however, that it is possible to pause a process in a debugger at a point where the heap is
        /// NOT corrupted, but does look inconsistent to ClrMD.  For example, the GC might allocate
        /// an array by writing a method table but the process might be paused before it had the chance
        /// to write the array length onto the heap.  In this case, IsObjectCorrupted may return true
        /// even if the process would have continued on fine.  As a result, this function acts more
        /// like a warning signal that more investigation is needed, and not proof-positive that there
        /// is heap corruption.
        /// </summary>
        /// <param name="objAddr">The address of the object to deeply verify.</param>
        /// <param name="detectedCorruption">An enumeration of all issues detected with this object.</param>
        /// <returns>True if the object is valid and fully verified, returns false if object corruption
        /// was detected.</returns>
        public bool FullyVerifyObject(ulong objAddr, out IEnumerable<ObjectCorruption> detectedCorruption)
        {
            ClrSegment? seg = GetSegmentByAddress(objAddr);
            ClrObject obj = GetObject(objAddr);
            if (seg is null || !seg.ObjectRange.Contains(objAddr))
            {
                detectedCorruption = new ObjectCorruption[] { new(obj, 0, ObjectCorruptionKind.ObjectNotOnTheHeap) };
                return false;
            }

            ObjectCorruption[] result = RentObjectCorruptionArray();
            int count = VerifyObject(GetSyncBlocks(), seg, obj, result);
            if (count == 0)
            {
                ReturnObjectCorruptionArray(result);
                detectedCorruption = Enumerable.Empty<ObjectCorruption>();
                return true;
            }

            detectedCorruption = result.Take(count);
            return false;
        }

        private void ReturnObjectCorruptionArray(ObjectCorruption[] result)
        {
            _objectCorruptionPool ??= ArrayPool<ObjectCorruption>.Create(64, 4);
            _objectCorruptionPool.Return(result);
        }

        private ObjectCorruption[] RentObjectCorruptionArray()
        {
            _objectCorruptionPool ??= ArrayPool<ObjectCorruption>.Create(64, 4);
            ObjectCorruption[] result = _objectCorruptionPool.Rent(64);
            return result;
        }

        bool IClrHeap.IsObjectCorrupted(ulong obj, [NotNullWhen(true)] out IObjectCorruption? result)
        {
            ObjectCorruption? corruption;
            bool r = IsObjectCorrupted(obj, out corruption);
            result = corruption;
            return r;
        }

        bool IClrHeap.IsObjectCorrupted(IClrValue obj, [NotNullWhen(true)] out IObjectCorruption? result)
        {
            ObjectCorruption? corruption;
            bool r = IsObjectCorrupted(obj.Address, out corruption);
            result = corruption;
            return r;
        }

        /// <summary>
        /// Verifies the GC Heap and returns an enumerator for any corrupted objects it finds.
        /// </summary>
        public IEnumerable<ObjectCorruption> VerifyHeap() => VerifyHeap(EnumerateObjects(carefully: true));

        IEnumerable<IObjectCorruption> IClrHeap.VerifyHeap() => VerifyHeap().Cast<IObjectCorruption>();

        /// <summary>
        /// Verifies the given objects and returns an enumerator for any corrupted objects it finds.
        /// </summary>
        public IEnumerable<ObjectCorruption> VerifyHeap(IEnumerable<ClrObject> objects)
        {
            foreach (ClrObject obj in objects)
                if (IsObjectCorrupted(obj, out ObjectCorruption? result))
                    yield return result;
        }

        IEnumerable<IObjectCorruption> IClrHeap.VerifyHeap(IEnumerable<IClrValue> objects)
        {
            foreach (IClrValue obj in objects)
                if (IsObjectCorrupted(obj.Address, out ObjectCorruption? result))
                    yield return result;
        }

        internal IEnumerable<ClrObject> EnumerateObjects(ClrSegment segment, ulong startAddress, bool carefully)
        {
            if (!segment.ObjectRange.Contains(startAddress))
                yield break;

            uint pointerSize = (uint)_memoryReader.PointerSize;
            uint minObjSize = pointerSize * 3;
            uint objSkip = segment.Kind != GCSegmentKind.Large ? minObjSize : 85000;
            using MemoryCache cache = new(_memoryReader, segment);

            ulong obj = GetValidObjectForAddress(segment, startAddress);
            while (segment.ObjectRange.Contains(obj))
            {
                if (!cache.ReadPointer(obj, out ulong mt))
                {
                    if (!carefully)
                        break;

                    obj = FindNextValidObject(segment, pointerSize, obj + objSkip, cache);
                    continue;
                }

                ClrType? type = _typeFactory.GetOrCreateType(mt, obj);
                ClrObject result = new(obj, type ?? ErrorType);
                yield return result;
                if (type is null)
                {
                    if (!carefully)
                        break;

                    obj = FindNextValidObject(segment, pointerSize, obj + objSkip, cache);
                    continue;
                }

                SetMarkerIndex(segment, obj);

                ulong size;
                if (type.ComponentSize == 0)
                {
                    size = (uint)type.StaticSize;
                }
                else
                {
                    if (!cache.ReadUInt32(obj + pointerSize, out uint count))
                    {
                        if (!carefully)
                            break;

                        obj = FindNextValidObject(segment, pointerSize, obj + objSkip, cache);
                        continue;
                    }

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
        }

        private static void SetMarkerIndex(ClrSegment segment, ulong obj)
        {
            ulong segmentOffset = obj - segment.ObjectRange.Start;
            int index = GetMarkerIndex(segment, obj);
            if (index != -1 && index < segment.ObjectMarkers.Length && segment.ObjectMarkers[index] == 0 && segmentOffset <= uint.MaxValue)
                segment.ObjectMarkers[index] = (uint)segmentOffset;
        }

        private static int GetMarkerIndex(ClrSegment segment, ulong startAddress)
        {
            if (segment.ObjectMarkers.Length == 0)
                return -1;

            ulong markerStep = segment.ObjectRange.Length / ((uint)segment.ObjectMarkers.Length + 2);
            int result = (int)((startAddress - segment.FirstObjectAddress) / markerStep);
            if (result >= segment.ObjectMarkers.Length)
                result = segment.ObjectMarkers.Length - 1;

            return result;
        }

        private static ulong GetValidObjectForAddress(ClrSegment segment, ulong address, bool previous = false)
        {
            if (address == segment.FirstObjectAddress || segment.ObjectMarkers.Length == 0)
                return segment.FirstObjectAddress;

            int index = GetMarkerIndex(segment, address);
            if (index >= segment.ObjectMarkers.Length)
                index = segment.ObjectMarkers.Length - 1;

            for (; index >= 0; index--)
            {
                uint marker = segment.ObjectMarkers[index];
                if (marker == 0)
                    continue;

                ulong validObject = segment.FirstObjectAddress + marker;
                if (previous)
                {
                    if (validObject < address)
                        return validObject;
                }
                else
                {
                    if (validObject <= address)
                        return validObject;
                }
            }

            return segment.FirstObjectAddress;
        }

        private class MemoryCache : IDisposable
        {
            private readonly IMemoryReader _memoryReader;
            private readonly int _pointerSize;
            private readonly uint _requiredSize;
            private readonly byte[]? _cache;

            public MemoryCache(IMemoryReader reader, ClrSegment segment)
            {
                _memoryReader = reader;
                _pointerSize = reader.PointerSize;
                _requiredSize = (uint)_pointerSize * 3;
                if (segment.Kind != GCSegmentKind.Large)
                    _cache = ArrayPool<byte>.Shared.Rent(EnumerateBufferSize);
            }

            public void Dispose()
            {
                if (_cache is not null)
                    ArrayPool<byte>.Shared.Return(_cache);
            }


            public ulong Base { get; private set; }
            public int Length { get; private set; }

            public bool ReadPointer(ulong address, out ulong value)
            {
                if (!EnsureInCache(address))
                    return _memoryReader.ReadPointer(address, out value);

                int offset = (int)(address - Base);
                value = _cache.AsSpan().AsPointer(offset);
                return true;
            }

            public bool ReadUInt32(ulong address, out uint value)
            {
                if (!EnsureInCache(address))
                    return _memoryReader.Read(address, out value);

                int offset = (int)(address - Base);
                value = _cache.AsSpan().AsUInt32(offset);
                return true;
            }


            private bool EnsureInCache(ulong address)
            {
                if (_cache is null)
                    return false;

                ulong end = Base + (uint)Length;
                if (Base <= address && address + _requiredSize < end)
                    return true;

                Base = address;
                Length = _memoryReader.Read(address, _cache);
                return Length >= _requiredSize;
            }
        }

        private ulong FindNextValidObject(ClrSegment segment, uint pointerSize, ulong address, MemoryCache cache)
        {
            ulong obj = address;
            while (segment.ObjectRange.Contains(obj))
            {
                ulong ctxObj = SkipAllocationContext(segment, obj);
                if (obj < ctxObj)
                {
                    obj = ctxObj;
                    continue;
                }

                obj += pointerSize;

                if (!cache.ReadPointer(obj, out ulong mt))
                    return 0;

                if (mt > 0x1000)
                {
                    if (Helpers.IsValidMethodTable(mt))
                        break;
                }
            }

            return obj;
        }


        /// <summary>
        /// Finds the next ClrObject on the given segment.
        /// </summary>
        /// <param name="address">An address on any ClrSegment.</param>
        /// <param name="carefully">Whether to continue walking objects on a segment where we've encountered
        /// a region of unwalkable memory.  Note that setting carefully = true may significantly increase the
        /// amount of time it takes to walk the heap if we encounter an error.</param>
        /// <returns>An invalid ClrObject if address doesn't lie on any segment or if no objects exist after the given address on a segment.</returns>
        public ClrObject FindNextObjectOnSegment(ulong address, bool carefully = false)
        {
            ClrSegment? seg = GetSegmentByAddress(address);
            if (seg is null)
                return default;

            foreach (ClrObject obj in EnumerateObjects(seg, address, carefully))
                if (address < obj)
                    return obj;

            return default;
        }

        /// <summary>
        /// Finds the previous object on the given segment.
        /// </summary>
        /// <param name="address">An address on any ClrSegment.</param>
        /// <param name="carefully">Whether to continue walking objects on a segment where we've encountered
        /// a region of unwalkable memory.  Note that setting carefully = true may significantly increase the
        /// amount of time it takes to walk the heap if we encounter an error.</param>
        /// <returns>An enumerator for all objects on the heap.</returns>
        /// <returns>An invalid ClrObject if address doesn't lie on any segment or if address is the first object on a segment.</returns>
        public ClrObject FindPreviousObjectOnSegment(ulong address, bool carefully = false)
        {
            ClrSegment? seg = GetSegmentByAddress(address);
            if (seg is null || address <= seg.FirstObjectAddress)
                return default;

            ulong start = GetValidObjectForAddress(seg, address, previous: true);
            DebugOnly.Assert(start < address);

            ClrObject last = default;
            foreach (ClrObject obj in EnumerateObjects(seg, start, carefully))
            {
                if (obj >= address)
                    return last;

                last = obj;
            }

            if (last < address)
                return last;

            return default;
        }

        private ulong SkipAllocationContext(ClrSegment seg, ulong address)
        {
            if (seg.Kind is GCSegmentKind.Large or GCSegmentKind.Frozen)
                return address;

            Dictionary<ulong, ulong> allocationContexts = GetAllocationContexts();

            uint minObjSize = (uint)IntPtr.Size * 3;
            while (allocationContexts.TryGetValue(address, out ulong nextObj))
            {
                nextObj += Align(minObjSize, seg);

                // Only if there's data corruption:
                if (address >= nextObj || nextObj >= seg.End)
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

            if (seg.Kind is GCSegmentKind.Large or GCSegmentKind.Pinned)
                return (size + AlignLargeConst) & ~AlignLargeConst;

            return (size + AlignConst) & ~AlignConst;
        }

        IEnumerable<IClrRoot> IClrHeap.EnumerateRoots() => EnumerateRoots().Cast<IClrRoot>();

        /// <summary>
        /// Enumerates all roots in the process.  Equivalent to the combination of:
        ///     ClrRuntime.EnumerateHandles().Where(handle => handle.IsStrong)
        ///     ClrRuntime.EnumerateThreads().SelectMany(thread => thread.EnumerateStackRoots())
        ///     ClrHeap.EnumerateFinalizerRoots()
        /// </summary>
        public IEnumerable<ClrRoot> EnumerateRoots()
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
            foreach (ClrRoot root in EnumerateFinalizerRoots())
                yield return root;

            // Threads
            foreach (ClrThread thread in Runtime.Threads.Where(t => t.IsAlive))
                foreach (ClrRoot root in thread.EnumerateStackRoots())
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
        public IEnumerable<ClrRoot> EnumerateFinalizerRoots() => EnumerateFinalizers(SubHeaps.Select(heap => heap.FinalizerQueueRoots));
        IEnumerable<IClrRoot> IClrHeap.EnumerateFinalizerRoots() => EnumerateFinalizerRoots().Cast<IClrRoot>();

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
            Dictionary<ulong, ulong>? allocationContexts = GetAllocationContexts();
            if (allocationContexts is not null)
                foreach (KeyValuePair<ulong, ulong> kv in allocationContexts)
                    yield return new(kv.Key, kv.Value);
        }

        public IEnumerable<SyncBlock> EnumerateSyncBlocks() => GetSyncBlocks();

        internal SyncBlock? GetSyncBlock(ulong obj) => GetSyncBlocks().TryGetSyncBlock(obj);

        private SyncBlockContainer GetSyncBlocks()
        {
            SyncBlockContainer? container = _syncBlocks;
            if (container is null)
            {
                container = new SyncBlockContainer(Helpers.EnumerateSyncBlocks());
                Interlocked.CompareExchange(ref _syncBlocks, container, null);
            }

            return container;
        }

        internal ClrThinLock? GetThinlock(ulong address)
        {
            uint header = _memoryReader.Read<uint>(address - 4);
            if (header == 0)
                return null;

            (ulong Thread, int Recursion) thinlock = Helpers.GetThinLock(header);
            if (thinlock != default)
                return new ClrThinLock(Runtime.Threads.FirstOrDefault(r => r.Address == thinlock.Thread), thinlock.Recursion);

            return null;
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
        internal SyncBlockComFlags GetComFlags(ulong obj)
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
        internal ulong GetObjectSize(ulong objRef, ClrType type)
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
        internal IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                (ulong Source, ulong Target)[] dependent = GetDependentHandles();

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
                            yield return new(dependantObj, GetObjectType(dependantObj) ?? ErrorType);
                        }
                    }
                }
            }

            if (type.IsCollectible)
            {
                ulong la = _memoryReader.ReadPointer(type.LoaderAllocatorHandle);
                if (la != 0)
                    yield return new(la, GetObjectType(la) ?? ErrorType);
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

                        bool large = seg.Kind is GCSegmentKind.Large or GCSegmentKind.Pinned;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                    if (read > IntPtr.Size)
                    {
                        foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                            yield return new(reference, GetObjectType(reference) ?? ErrorType);
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
        internal IEnumerable<ClrReference> EnumerateReferencesWithFields(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                (ulong Source, ulong Target)[] dependent = GetDependentHandles();

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
                            ClrObject target = new(dependantObj, GetObjectType(dependantObj) ?? ErrorType);
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

                        bool large = seg.Kind is GCSegmentKind.Large or GCSegmentKind.Pinned;
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
                            ClrObject target = new(reference, GetObjectType(reference) ?? ErrorType);

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
        internal IEnumerable<ulong> EnumerateReferenceAddresses(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                (ulong Source, ulong Target)[] dependent = GetDependentHandles();

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

                        bool large = seg.Kind is GCSegmentKind.Large or GCSegmentKind.Pinned;
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
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private Dictionary<ulong, ulong> GetAllocationContexts()
        {
            Dictionary<ulong, ulong>? result = _allocationContexts;
            if (result is not null)
                return result;

            result = new();
            foreach (MemoryRange allocContext in Helpers.EnumerateThreadAllocationContexts())
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

            handles = Helpers.EnumerateDependentHandles().OrderBy(r => r.Source).ToArray();

            Interlocked.CompareExchange(ref _dependentHandles, handles, null);
            return handles;
        }

        private IEnumerable<ClrRoot> EnumerateFinalizers(IEnumerable<MemoryRange> memoryRanges)
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
                        yield return new ClrRoot(ptr, new ClrObject(obj, type), ClrRootKind.FinalizerQueue, isInterior: false, isPinned: false);
                }
            }
        }

        internal ClrType? GetOrCreateTypeFromSignature(ClrModule module, SigParser sigParser, IEnumerable<ClrGenericParameter> typeParameters, IEnumerable<ClrGenericParameter> methodParameters)
        {
            return _typeFactory.GetOrCreateTypeFromSignature(module, sigParser, typeParameters, methodParameters);
        }
        public ClrType? GetTypeByMethodTable(ulong methodTable) => _typeFactory.GetOrCreateType(methodTable, 0);

        public ClrType? GetTypeByName(string name) => Runtime.EnumerateModules().OrderBy(m => m.Name ?? "").Select(m => GetTypeByName(m, name)).Where(r => r != null).FirstOrDefault();

        public ClrType? GetTypeByName(ClrModule module, string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw new ArgumentException($"{nameof(name)} cannot be empty");

            return FindTypeName(module.Address, module.EnumerateTypeDefToMethodTableMap(), name);
        }

        private ClrType? FindTypeName(ulong moduleAddress, IEnumerable<(ulong MethodTable, int Token)> map, string name)
        {
            // First, look for already constructed types and see if their name matches.
            List<(ulong MethodTable, int Token)> lookup = new(256);
            foreach ((ulong mt, int token) in map)
            {
                ClrType? type = _typeFactory.TryGetType(mt);
                if (type is null)
                    lookup.Add((mt, token));
                else if (type.Name == name)
                    return type;
            }

            // Since we didn't find pre-constructed types matching, look up the names for all
            // remaining types without constructing them until we find the right one.
            foreach ((ulong mt, int token) in lookup)
            {
                string? typeName = _typeFactory.GetTypeName(moduleAddress, mt, token);
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
            return new ClrException(thread, obj);
        }

        IEnumerable<IClrValue> IClrHeap.EnumerateFinalizableObjects() => EnumerateFinalizableObjects().Cast<IClrValue>();

        IEnumerable<IClrValue> IClrHeap.EnumerateObjects(bool carefully) => EnumerateObjects(carefully).Cast<IClrValue>();

        IEnumerable<IClrValue> IClrHeap.EnumerateObjects() => EnumerateObjects().Cast<IClrValue>();

        IEnumerable<IClrValue> IClrHeap.EnumerateObjects(MemoryRange range, bool carefully) => EnumerateObjects(range, carefully).Cast<IClrValue>();

        IClrValue IClrHeap.FindNextObjectOnSegment(ulong address, bool carefully) => FindNextObjectOnSegment(address, carefully);

        IClrValue IClrHeap.FindPreviousObjectOnSegment(ulong address, bool carefully) => FindPreviousObjectOnSegment(address, carefully);

        IClrValue IClrHeap.GetObject(ulong objRef) => GetObject(objRef);

        IClrType? IClrHeap.GetObjectType(ulong objRef) => GetObjectType(objRef);

        IClrSegment? IClrHeap.GetSegmentByAddress(ulong address) => GetSegmentByAddress(address);

        IClrType? IClrHeap.GetTypeByMethodTable(ulong methodTable) => GetTypeByMethodTable(methodTable);

        IClrType? IClrHeap.GetTypeByName(string name) => GetTypeByName(name);

        IClrType? IClrHeap.GetTypeByName(IClrModule module, string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw new ArgumentException($"{nameof(name)} cannot be empty");

            return FindTypeName(module.Address, module.EnumerateTypeDefToMethodTableMap(), name);
        }

        internal string? ReadString(ulong stringPtr, int maxLength)
        {
            if (stringPtr == 0)
                return null;

            int length = _memoryReader.Read<int>(stringPtr + _stringLength);
            length = Math.Min(length, maxLength);
            if (length == 0)
                return string.Empty;

            ulong data = stringPtr + _firstChar;
            char[] buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                Span<char> charSpan = new Span<char>(buffer).Slice(0, length);
                Span<byte> bytes = MemoryMarshal.AsBytes(charSpan);
                int read = _memoryReader.Read(data, bytes);
                if (read == 0)
                    return null;

                return new string(buffer, 0, read / sizeof(char));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private int VerifyObject(SyncBlockContainer syncBlocks, ClrSegment seg, ClrObject obj, Span<ObjectCorruption> result)
        {
            if (result.Length == 0)
                throw new ArgumentException($"{nameof(result)} must have at least one element.");


            // Is the object address pointer aligned?
            if ((obj.Address & ((uint)_memoryReader.PointerSize - 1)) != 0)
            {
                result[0] = new(obj, 0, ObjectCorruptionKind.ObjectNotPointerAligned);
                return 1;
            }

            if (!obj.IsFree)
            {
                // Can we read the method table?
                if (!_memoryReader.Read(obj.Address, out ulong mt))
                {
                    result[0] = new(obj, 0, ObjectCorruptionKind.CouldNotReadMethodTable);
                    return 1;
                }

                // Is the method table we read valid?
                if (!Helpers.IsValidMethodTable(mt))
                {
                    result[0] = new(obj, 0, ObjectCorruptionKind.InvalidMethodTable);
                    return 1;
                }
                else if (obj.Type is null)
                {
                    // This shouldn't happen if VerifyMethodTable above returns success, but we'll make sure.
                    result[0] = new(obj, 0, ObjectCorruptionKind.InvalidMethodTable);
                    return 1;
                }
            }

            // Any previous failures are fatal, we can't keep verifying the object.  From here, though, we'll
            // attempt to report any and all failures we encounter.
            int index = 0;

            // Check object size
            int intSize = obj.Size > int.MaxValue ? int.MaxValue : (int)obj.Size;
            if (obj + obj.Size > seg.ObjectRange.End || (!obj.IsFree && obj.Size > seg.MaxObjectSize))
                if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, _memoryReader.PointerSize, ObjectCorruptionKind.ObjectTooLarge)))
                    return index;

            // If we are inspecting a free object, the rest of this method is not needed.
            if (obj.IsFree)
                return index;

            // Validate members
            bool verifyMembers;
            try
            {
                // Type can't be null, we checked above.  The compiler just get lost in the IsFree checks.
                verifyMembers = obj.Type!.ContainsPointers && ShouldVerifyMembers(seg, obj);

                // If the object is an array and too large, it likely means someone wrote over the size
                // field of our array.  Trying to verify the members of the array will generate a ton
                // of noisy failures, so we'll avoid doing that.
                if (verifyMembers && obj.Type.IsArray)
                {
                    for (int i = 0; i < index; i++)
                    {
                        if (result[i].Kind == ObjectCorruptionKind.ObjectTooLarge)
                        {
                            verifyMembers = false;
                            break;
                        }
                    }
                }
            }
            catch (IOException)
            {
                if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, 0, ObjectCorruptionKind.CouldNotReadCardTable)))
                    return index;

                verifyMembers = false;
            }

            if (verifyMembers)
            {
                GCDesc gcdesc = obj.Type!.GCDesc;
                if (gcdesc.IsEmpty)
                    if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, 0, ObjectCorruptionKind.CouldNotReadGCDesc)))
                        return index;

                ulong freeMt = seg.SubHeap.Heap.FreeType.MethodTable;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                int read = _memoryReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                if (read != intSize)
                    if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, read >= 0 ? read : 0, ObjectCorruptionKind.CouldNotReadObject)))
                        return index;

                foreach ((ulong objRef, int offset) in gcdesc.WalkObject(buffer, intSize))
                {
                    if ((objRef & ((uint)_memoryReader.PointerSize - 1)) != 0)
                    {
                        if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, offset, ObjectCorruptionKind.ObjectReferenceNotPointerAligned)))
                            break;
                    }

                    if (!_memoryReader.Read(objRef, out ulong mt) || !Helpers.IsValidMethodTable(mt))
                    {
                        if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, offset, ObjectCorruptionKind.InvalidObjectReference)))
                            break;
                    }
                    else if ((mt & ~1ul) == freeMt)
                    {
                        if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, offset, ObjectCorruptionKind.FreeObjectReference)))
                            break;
                    }
                }

                ArrayPool<byte>.Shared.Return(buffer);
                if (index >= result.Length)
                    return index;
            }

            // Object header validation tests:
            uint objHeader = _memoryReader.Read<uint>(obj - sizeof(uint));

            // Validate SyncBlock
            SyncBlock? blk = syncBlocks.TryGetSyncBlock(obj);
            if ((objHeader & SyncBlockHashOrSyncBlockIndex) != 0 && (objHeader & SyncBlockHashCodeIndex) == 0)
            {
                uint sblkIndex = objHeader & SyncBlockIndexMask;
                int clrIndex = blk?.Index ?? -1;

                if (sblkIndex == 0)
                {
                    if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockZero, -1, clrIndex)))
                        return index;
                }
                else if (sblkIndex != clrIndex)
                {
                    if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockMismatch, (int)sblkIndex, clrIndex)))
                        return index;
                }
            }
            else if (blk is not null)
            {
                if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, -sizeof(uint), ObjectCorruptionKind.SyncBlockMismatch, -1, blk.Index)))
                    return index;
            }

            // Validate Thinlock
            (ulong Thread, int Recursion) thinlock = Helpers.GetThinLock(objHeader);
            if (thinlock.Thread != 0)
            {
                ClrRuntime runtime = seg.SubHeap.Heap.Runtime;
                if (!runtime.Threads.Any(th => th.Address == thinlock.Thread))
                {
                    if (!AddCorruptionAndContinue(result, ref index, new ObjectCorruption(obj, -4, ObjectCorruptionKind.InvalidThinlock)))
                        return index;
                }
            }

            return index;
        }

        private static bool AddCorruptionAndContinue(Span<ObjectCorruption> result, ref int curr, ObjectCorruption objectCorruption)
        {
            result[curr++] = objectCorruption;
            return curr < result.Length;
        }

        private bool ShouldVerifyMembers(ClrSegment seg, ClrObject obj)
        {
            ShouldCheckBgcMark(seg, out bool considerBgcMark, out bool checkCurrentSweep, out bool checkSavedSweep);
            return FgcShouldConsiderObject(seg, obj, considerBgcMark, checkCurrentSweep, checkSavedSweep);
        }

        private bool FgcShouldConsiderObject(ClrSegment seg, ClrObject obj, bool considerBgcMark, bool checkCurrentSweep, bool checkSavedSweep)
        {
            // fgc_should_consider_object in gc.cpp
            ClrSubHeap heap = seg.SubHeap;
            bool noBgcMark = false;
            if (considerBgcMark)
            {
                // gc.cpp:  if (check_current_sweep_p && (o < current_sweep_pos))
                if (checkCurrentSweep && obj < heap.CurrentSweepPosition)
                {
                    noBgcMark = true;
                }

                if (!noBgcMark)
                {
                    // gc.cpp:  if(check_saved_sweep_p && (o >= saved_sweep_ephemeral_start))
                    if (checkSavedSweep && obj >= heap.SavedSweepEphemeralStart)
                    {
                        noBgcMark = true;
                    }

                    // gc.cpp:  if (o >= background_allocated)
                    if (obj >= seg.BackgroundAllocated)
                        noBgcMark = true;
                }
            }
            else
            {
                noBgcMark = true;
            }

            // gc.cpp: return (no_bgc_mark_p ? TRUE : background_object_marked (o, FALSE))
            return noBgcMark || BackgroundObjectMarked(heap, obj);
        }

        private const uint MarkBitPitch = 8;
        private const uint MarkWordWidth = 32;
        private const uint MarkWordSize = MarkBitPitch * MarkWordWidth;

#pragma warning disable IDE0051 // Remove unused private members. This is information we'd like to keep.
        private const uint DtGcPageSize = 0x1000;
        private const uint CardWordWidth = 32;
        private uint CardSize => ((uint)_memoryReader.PointerSize / 4) * DtGcPageSize / CardWordWidth;
#pragma warning restore IDE0051 // Remove unused private members

        private static void ShouldCheckBgcMark(ClrSegment seg, out bool considerBgcMark, out bool checkCurrentSweep, out bool checkSavedSweep)
        {
            // Keep in sync with should_check_bgc_mark in gc.cpp
            considerBgcMark = false;
            checkCurrentSweep = false;
            checkSavedSweep = false;

            // if (current_c_gc_state == c_gc_state_planning)
            ClrSubHeap heap = seg.SubHeap;
            if (heap.State == HeapMarkState.Planning)
            {
                if ((seg.Flags & ClrSegmentFlags.Swept) == ClrSegmentFlags.Swept || !seg.ObjectRange.Contains(heap.CurrentSweepPosition))
                {
                    // gc.cpp: if ((seg->flags & heap_segment_flags_swept) || (current_sweep_pos == heap_segment_reserved (seg)))

                    // this seg was already swept
                }
                else if (seg.BackgroundAllocated == 0)
                {
                    // gc.cpp:  else if (heap_segment_background_allocated (seg) == 0)

                    // newly alloc during bgc
                }
                else
                {
                    considerBgcMark = true;

                    // gc.cpp:  if (seg == saved_sweep_ephemeral_seg)
                    if (seg.Address == heap.SavedSweepEphemeralSegment)
                        checkSavedSweep = true;

                    // gc.cpp:  if (in_range_for_segment (current_sweep_pos, seg))
                    if (seg.ObjectRange.Contains(heap.CurrentSweepPosition))
                        checkCurrentSweep = true;
                }
            }
        }

        private bool BackgroundObjectMarked(ClrSubHeap heap, ClrObject obj)
        {
            // gc.cpp: if ((o >= background_saved_lowest_address) && (o < background_saved_highest_address))
            if (obj >= heap.BackgroundSavedLowestAddress && obj < heap.BackgroundSavedHighestAddress)
                return MarkArrayMarked(heap, obj);

            return true;
        }

        private bool MarkArrayMarked(ClrSubHeap heap, ClrObject obj)
        {
            ulong address = heap.MarkArray + sizeof(uint) * MarkWordOf(obj);
            if (!_memoryReader.Read(address, out uint entry))
                throw new IOException($"Could not read mark array at {address:x}");

            return (entry & (1u << MarkBitOf(obj))) != 0;
        }

        private static int MarkBitOf(ulong address) => (int)((address / MarkBitPitch) % MarkWordWidth);
        private static ulong MarkWordOf(ulong address) => address / MarkWordSize;
    }
}