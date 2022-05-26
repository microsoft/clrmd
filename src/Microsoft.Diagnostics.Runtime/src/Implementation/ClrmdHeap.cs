﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

#pragma warning disable CA1721 // Property names should not match get methods

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal sealed class ClrmdHeap : ClrHeap
    {
        private const int MaxGen2ObjectSize = 85000;
        private readonly IHeapHelpers _helpers;

        private readonly object _sync = new();
        private volatile VolatileHeapData? _volatileHeapData;
        private volatile VolatileSyncBlockData? _volatileSyncBlocks;
        private ulong _lastComFlags;

        public override ClrRuntime Runtime { get; }

        public override bool CanWalkHeap { get; }

        private Dictionary<ulong, ulong> AllocationContexts => GetHeapData().AllocationContext;

        private ImmutableArray<FinalizerQueueSegment> FQRoots => GetHeapData().FQRoots;

        private ImmutableArray<FinalizerQueueSegment> FQObjects => GetHeapData().FQObjects;

        public override ImmutableArray<ClrSegment> Segments => GetHeapData().Segments;

        public override int LogicalHeapCount { get; }

        public override ClrType FreeType { get; }

        public override ClrType StringType { get; }

        public override ClrType ObjectType { get; }

        public override ClrType ExceptionType { get; }

        public override bool IsServer { get; }

        public ClrmdHeap(ClrRuntime runtime, IHeapData data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _helpers = data.HeapHelpers;

            Runtime = runtime;
            CanWalkHeap = data.CanWalkHeap;
            IsServer = data.IsServer;
            LogicalHeapCount = data.LogicalHeapCount;

            // Prepopulate a few important method tables.  This should never fail.
            FreeType = _helpers.Factory.CreateSystemType(this, data.FreeMethodTable, "Free");
            ObjectType = _helpers.Factory.CreateSystemType(this, data.ObjectMethodTable, "System.Object");
            ExceptionType = _helpers.Factory.CreateSystemType(this, data.ExceptionMethodTable, "System.Exception");

            // Triage dumps may not place System.String data into the dump.  In these cases we'll create a "fake"
            // System.String type for the library to use.
            try
            {
                StringType = _helpers.Factory.CreateSystemType(this, data.StringMethodTable, "System.String");
            }
            catch (System.IO.InvalidDataException)
            {
                int token = 0;
                using var sosdac = Runtime.DacLibrary.SOSDacInterface;
                if (sosdac.GetMethodTableData(data.StringMethodTable, out DacInterface.MethodTableData mtd))
                    token = (int)mtd.Token;

                StringType = new ClrmdStringType((ITypeHelpers)_helpers, this, data.StringMethodTable, token);
            }
        }

        public override IEnumerable<MemoryRange> EnumerateAllocationContexts() => AllocationContexts.Select(item => new MemoryRange(item.Key, item.Value));

        private VolatileHeapData GetHeapData()
        {
            VolatileHeapData? data = _volatileHeapData;
            if (data != null)
                return data;

            lock (_sync)
            {
                data = _volatileHeapData;
                if (data != null)
                    return data;

                data = new VolatileHeapData(this, _helpers);
                _volatileHeapData = data;
                return data;
            }
        }

        public void ClearCachedData()
        {
            _volatileHeapData = null;
            _volatileSyncBlocks = null;
        }

        public ulong SkipAllocationContext(ClrSegment seg, ulong address)
        {
            if (seg is null)
                throw new ArgumentNullException(nameof(seg));

            if (seg.IsLargeObjectSegment)
                return address;

            uint minObjSize = (uint)IntPtr.Size * 3;
            while (AllocationContexts.TryGetValue(address, out ulong nextObj))
            {
                nextObj += Align(minObjSize, seg.IsLargeObjectSegment || seg.IsPinnedObjectSegment);

                if (address >= nextObj || address >= seg.End)
                    return 0;

                // Only if there's data corruption:
                if (address >= nextObj || address >= seg.End)
                    return 0;

                address = nextObj;
            }

            return address;
        }

        public override IEnumerable<ClrObject> EnumerateObjects() => Segments.SelectMany(s => s.EnumerateObjects());

        internal static ulong Align(ulong size, bool large)
        {
            ulong AlignConst;
            ulong AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~AlignLargeConst;

            return (size + AlignConst) & ~AlignConst;
        }

        public override ClrType? GetObjectType(ulong objRef)
        {
            ulong mt = _helpers.DataReader.ReadPointer(objRef);

            if (mt == 0)
                return null;

            return _helpers.Factory.GetOrCreateType(mt, objRef);
        }

        public override ClrSegment? GetSegmentByAddress(ulong address)
        {
            VolatileHeapData data = GetHeapData();
            ImmutableArray<ClrSegment> segments = data.Segments;
            if (segments.Length == 0)
                return null;

            if (segments[0].FirstObjectAddress <= address && address < segments[segments.Length - 1].End)
            {
                int index = segments.Search(address, (seg, value) => seg.ObjectRange.CompareTo(value));
                if (index == -1)
                    return null;

                return segments[index];
            }

            return null;
        }

        public override SyncBlock? GetSyncBlock(ulong obj)
        {
            VolatileSyncBlockData? syncBlocks = _volatileSyncBlocks;
            if (syncBlocks is null)
            {
                syncBlocks = new VolatileSyncBlockData(_helpers);
                _volatileSyncBlocks = syncBlocks;
            }

            return syncBlocks.GetSyncBlock(obj);
        }

        public override SyncBlockComFlags GetComFlags(ulong obj)
        {
            if (obj == 0)
                return SyncBlockComFlags.None;

            const ulong mask = ~0xe000000000000000;
            ulong lastComFlags = _lastComFlags;
            if ((lastComFlags & mask) == obj)
                return (SyncBlockComFlags)(lastComFlags >> 61);

            VolatileSyncBlockData? syncBlocks = _volatileSyncBlocks;
            if (syncBlocks is null)
            {
                syncBlocks = new VolatileSyncBlockData(_helpers);
                _volatileSyncBlocks = syncBlocks;
            }

            SyncBlockComFlags flags = syncBlocks.GetComFlags(obj);
            _lastComFlags = ((ulong)flags << 61) | (obj & mask);
            return flags;
        }

        public override ulong GetObjectSize(ulong objRef, ClrType type)
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

                uint count = _helpers.DataReader.Read<uint>(loc);

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

        public override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                ImmutableArray<(ulong Source, ulong Target)> dependent = GetHeapData().GetDependentHandles(_helpers);

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
                            yield return new ClrObject(dependantObj, GetObjectType(dependantObj));
                        }
                    }
                }
            }

            if (type.IsCollectible)
            {
                ulong la = _helpers.DataReader.ReadPointer(type.LoaderAllocatorHandle);
                if (la != 0)
                    yield return new ClrObject(la, GetObjectType(la));
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

                        bool large = seg.IsLargeObjectSegment || seg.IsPinnedObjectSegment;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    try
                    {
                        int read = _helpers.DataReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                        if (read > IntPtr.Size)
                        {
                            foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                                yield return new ClrObject(reference, GetObjectType(reference));
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }

        public override IEnumerable<ulong> EnumerateReferenceAddresses(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                ImmutableArray<(ulong Source, ulong Target)> dependent = GetHeapData().GetDependentHandles(_helpers);

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

                        bool large = seg.IsLargeObjectSegment || seg.IsPinnedObjectSegment;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    try
                    {
                        int read = _helpers.DataReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                        if (read > IntPtr.Size)
                        {
                            foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                            {
                                yield return reference;
                                DebugOnly.Assert(offset >= IntPtr.Size);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }

        public override IEnumerable<ClrReference> EnumerateReferencesWithFields(ulong obj, ClrType type, bool carefully, bool considerDependantHandles)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (considerDependantHandles)
            {
                ImmutableArray<(ulong Source, ulong Target)> dependent = GetHeapData().GetDependentHandles(_helpers);

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
                            ClrObject target = new ClrObject(dependantObj, GetObjectType(dependantObj));
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

                        bool large = seg.IsLargeObjectSegment || seg.IsPinnedObjectSegment;
                        if (obj + size > seg.End || (!large && size > MaxGen2ObjectSize))
                            yield break;
                    }

                    int intSize = (int)size;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(intSize);
                    try
                    {
                        int read = _helpers.DataReader.Read(obj, new Span<byte>(buffer, 0, intSize));
                        if (read > IntPtr.Size)
                        {
                            foreach ((ulong reference, int offset) in gcdesc.WalkObject(buffer, read))
                            {
                                ClrObject target = new ClrObject(reference, GetObjectType(reference));

                                DebugOnly.Assert(offset >= IntPtr.Size);
                                yield return ClrReference.CreateFromFieldOrArray(target, type, offset - IntPtr.Size);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }

        public override IEnumerable<IClrRoot> EnumerateRoots()
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
            foreach (IClrRoot root in EnumerateStackRoots())
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
                    ulong objPtr = _helpers.DataReader.ReadPointer(address);
                    ClrObject obj = GetObject(objPtr);

                    if (obj.IsValid)
                        return (address, obj);
                }
            }

            return (0ul, default);
        }

        private IEnumerable<IClrRoot> EnumerateStackRoots()
        {
            foreach (ClrThread thread in Runtime.Threads)
            {
                if (thread.IsAlive)
                {
                    foreach (IClrRoot root in thread.EnumerateStackRoots())
                        yield return root;
                }
            }
        }

        public override IEnumerable<ClrObject> EnumerateFinalizableObjects() => EnumerateFQ(FQObjects).Select(root => root.Object);

        public override IEnumerable<ClrFinalizerRoot> EnumerateFinalizerRoots() => EnumerateFQ(FQRoots);

        private IEnumerable<ClrFinalizerRoot> EnumerateFQ(IEnumerable<FinalizerQueueSegment> fqList)
        {
            if (fqList is null)
                yield break;

            foreach (FinalizerQueueSegment seg in fqList)
            {
                for (ulong ptr = seg.Start; ptr < seg.End; ptr += (uint)IntPtr.Size)
                {
                    ulong obj = _helpers.DataReader.ReadPointer(ptr);
                    if (obj == 0)
                        continue;

                    ulong mt = _helpers.DataReader.ReadPointer(obj);
                    ClrType? type = _helpers.Factory.GetOrCreateType(mt, obj);
                    if (type != null)
                        yield return new ClrFinalizerRoot(ptr, new ClrObject(obj, type));
                }
            }
        }

        class VolatileSyncBlockData
        {
            public ImmutableArray<ComSyncBlock> ComOnlySyncBlocks { get; }
            public ImmutableArray<FullSyncBlock> FullSyncBlocks { get; }
            public ImmutableHashSet<ulong> EmptySyncBlocks { get; }

            public VolatileSyncBlockData(IHeapHelpers helpers)
            {
                // We don't care about a false return as that'll simply create empty arrays and sets, which is fine.
                helpers.GetSyncBlocks(out List<ComSyncBlock> comSyncBlock, out List<FullSyncBlock> fullSyncBlocks, out List<ulong> empty);

                if (comSyncBlock.Count > 0)
                    comSyncBlock.Sort((x, y) => x.Object.CompareTo(y.Object));

                if (fullSyncBlocks.Count > 0)
                    fullSyncBlocks.Sort((x, y) => x.Object.CompareTo(y.Object));

                ComOnlySyncBlocks = comSyncBlock.ToImmutableArray();
                FullSyncBlocks = fullSyncBlocks.ToImmutableArray();
                EmptySyncBlocks = empty.ToImmutableHashSet();
            }

            public SyncBlock? GetSyncBlock(ulong obj)
            {
                if (EmptySyncBlocks.Contains(obj))
                    return new SyncBlock(obj);

                int index = ComOnlySyncBlocks.Search(obj, (x, y) => x.Object.CompareTo(y));
                if (index != -1)
                    return ComOnlySyncBlocks[index];

                index = FullSyncBlocks.Search(obj, (x, y) => x.Object.CompareTo(y));
                if (index != -1)
                    return FullSyncBlocks[index];

                return null;
            }

            public SyncBlockComFlags GetComFlags(ulong obj)
            {
                int index = ComOnlySyncBlocks.Search(obj, (x, y) => x.Object.CompareTo(y));
                if (index != -1)
                    return ComOnlySyncBlocks[index].ComFlags;

                index = FullSyncBlocks.Search(obj, (x, y) => x.Object.CompareTo(y));
                if (index != -1)
                    return FullSyncBlocks[index].ComFlags;

                return SyncBlockComFlags.None;
            }
        }

        class VolatileHeapData
        {
            private readonly object _sync = new();
            private ImmutableArray<(ulong Source, ulong Target)> _dependentHandles;

            public ImmutableArray<FinalizerQueueSegment> FQRoots { get; }
            public ImmutableArray<FinalizerQueueSegment> FQObjects { get; }
            public Dictionary<ulong, ulong> AllocationContext { get; }

            public ImmutableArray<ClrSegment> Segments { get; }

            public int LastSegmentIndex { get; set; }

            public VolatileHeapData(ClrHeap parent, IHeapHelpers _helpers)
            {
                _helpers.CreateSegments(parent,
                                        out ImmutableArray<ClrSegment> segments,
                                        out ImmutableArray<MemoryRange> allocContext,
                                        out ImmutableArray<FinalizerQueueSegment> fqRoots,
                                        out ImmutableArray<FinalizerQueueSegment> fqObjects);

                // Segments must be in sorted order.  We won't check all of them but we will at least check the beginning and end
                if (segments.Length > 0 && segments[0].Start > segments[segments.Length - 1].Start)
                    Segments = segments.Sort((x, y) => x.Start.CompareTo(y.Start));
                else
                    Segments = segments;

                FQRoots = fqRoots;
                FQObjects = fqObjects;
                AllocationContext = allocContext.ToDictionary(k => k.Start, v => v.End);
            }

            public ImmutableArray<(ulong Source, ulong Target)> GetDependentHandles(IHeapHelpers helpers)
            {
                if (!_dependentHandles.IsDefault)
                    return _dependentHandles;

                lock (_sync)
                {
                    if (!_dependentHandles.IsDefault)
                        return _dependentHandles;

                    var dependentHandles = helpers.EnumerateDependentHandleLinks().OrderBy(x => x.Source).ToImmutableArray();
                    _dependentHandles = dependentHandles;
                    return dependentHandles;
                }
            }
        }
    }
}