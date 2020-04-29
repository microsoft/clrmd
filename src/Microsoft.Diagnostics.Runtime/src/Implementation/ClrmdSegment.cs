// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdSegment : ClrSegment
    {
        const int MarkerCount = 16;

        private readonly IHeapHelpers _helpers;
        private readonly ClrmdHeap _clrmdHeap;
        private readonly ulong[] _markers;

        public ClrmdSegment(ClrmdHeap heap, IHeapHelpers helpers, ISegmentData data)
        {
            if (helpers is null)
                throw new ArgumentNullException(nameof(helpers));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _helpers = helpers;
            _clrmdHeap = heap;

            LogicalHeap = data.LogicalHeap;
            IsLargeObjectSegment = data.IsLargeObjectSegment;
            IsEphemeralSegment = data.IsEphemeralSegment;

            ObjectRange = new MemoryRange(data.Start, data.End);
            ReservedMemory = new MemoryRange(data.CommittedEnd, data.ReservedEnd);
            CommittedMemory = new MemoryRange(data.BaseAddress, data.CommittedEnd);

            Generation0 = MemoryRange.CreateFromLength(data.Gen0Start, data.Gen0Length);
            Generation1 = MemoryRange.CreateFromLength(data.Gen1Start, data.Gen1Length);
            Generation2 = MemoryRange.CreateFromLength(data.Gen2Start, data.Gen2Length);

            _markers = new ulong[MarkerCount];
        }

        public override ClrHeap Heap => _clrmdHeap;

        public override int LogicalHeap { get; }

        public override bool IsLargeObjectSegment { get; }
        public override bool IsEphemeralSegment { get; }

        public override MemoryRange ObjectRange { get; }

        public override MemoryRange ReservedMemory { get; }
        
        public override MemoryRange CommittedMemory { get; }

        public override MemoryRange Generation0 { get; }
        
        public override MemoryRange Generation1 { get; }
        
        public override MemoryRange Generation2 { get; }

        public override ulong FirstObjectAddress => ObjectRange.Start;
        
        public override IEnumerable<ClrObject> EnumerateObjects()
        {
            bool large = IsLargeObjectSegment;
            uint minObjSize = (uint)IntPtr.Size * 3;
            ulong obj = FirstObjectAddress;
            IDataReader dataReader = _helpers.DataReader;

            // C# isn't smart enough to understand that !large means memoryReader is non-null.  We will just be
            // careful here.
            using MemoryReader memoryReader = (!large ? new MemoryReader(dataReader, 0x10000) : null)!;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(IntPtr.Size * 2 + sizeof(uint));

            // The large object heap
            if (!large)
                memoryReader.EnsureRangeInCache(obj);

            while (ObjectRange.Contains(obj))
            {
                ulong mt;
                if (large)
                {
                    if (!dataReader.Read(obj, buffer, out int read) || read != buffer.Length)
                        break;

                    if (IntPtr.Size == 4)
                        mt = Unsafe.As<byte, uint>(ref buffer[0]);
                    else
                        mt = Unsafe.As<byte, ulong>(ref buffer[0]);
                }
                else
                {
                    if (!memoryReader.ReadPtr(obj, out mt))
                        break;
                }

                ClrType? type = _helpers.Factory.GetOrCreateType(_clrmdHeap, mt, obj);
                if (type is null)
                    break;

                int marker = GetMarkerIndex(obj);
                if (marker != -1 && _markers[marker] == 0)
                    _markers[marker] = obj;

                ClrObject result = new ClrObject(obj, type);
                yield return result;

                ulong size;
                if (type.ComponentSize == 0)
                {
                    size = (uint)type.StaticSize;
                }
                else
                {
                    uint count;
                    if (large)
                        count = Unsafe.As<byte, uint>(ref buffer[IntPtr.Size * 2]);
                    else
                        memoryReader.ReadDword(obj + (uint)IntPtr.Size, out count);

                    // Strings in v4+ contain a trailing null terminator not accounted for.
                    if (_clrmdHeap.StringType == type)
                        count++;

                    size = count * (ulong)type.ComponentSize + (ulong)type.StaticSize;
                }

                size = ClrmdHeap.Align(size, large);
                if (size < minObjSize)
                    size = minObjSize;

                obj += size;
                obj = _clrmdHeap.SkipAllocationContext(this, obj);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }

        private int GetMarkerIndex(ulong obj)
        {
            if (obj <= FirstObjectAddress)
                return -1;

            if (obj >= End)
                return _markers.Length - 1;

            ulong step = Length / ((uint)_markers.Length + 1);

            ulong offset = obj - FirstObjectAddress;
            int index = (int)(offset / step) - 1;

            if (index >= _markers.Length)
                index = _markers.Length - 1;

            return index;
        }

        public override ulong GetPreviousObjectAddress(ulong addr)
        {
            if (!ObjectRange.Contains(addr))
                throw new InvalidOperationException($"Segment [{FirstObjectAddress:x},{CommittedMemory:x}] does not contain address {addr:x}");

            if (addr == FirstObjectAddress)
                return 0;

            // Default to the start of the segment
            ulong prevAddr = FirstObjectAddress;

            // Look for markers that are closer to the address.  We keep the size of _markers small so a linear walk
            // should be roughly as fast as a binary search.
            foreach (ulong marker in _markers)
            {
                // Markers can be 0 even when _markers was fully intialized by a full heap walk.  This is because parts of
                // the ephemeral GC heap may be not in use (allocation contexts) or when objects on the large object heap
                // are so big that there's simply not a valid object starting point in that range.
                if (marker != 0)
                {
                    if (marker >= addr)
                        break;

                    prevAddr = marker;
                }
            }

            // Linear walk from the last known good previous address to the one we are looking for.
            // This could take a while if we don't know a close enough address.
            ulong curr = prevAddr;
            while (curr != 0 && curr <= addr)
            {
                ulong next = GetNextObjectAddress(curr);

                if (next >= addr)
                    return curr;

                curr = next;
            }

            return 0;
        }

        public override ulong GetNextObjectAddress(ulong addr)
        {
            if (addr == 0)
                return 0;

            if (!ObjectRange.Contains(addr))
                throw new InvalidOperationException($"Segment [{FirstObjectAddress:x},{CommittedMemory:x}] does not contain object {addr:x}");

            bool large = IsLargeObjectSegment;
            uint minObjSize = (uint)IntPtr.Size * 3;
            IMemoryReader memoryReader = _helpers.DataReader;
            ulong mt = memoryReader.ReadPointer(addr);

            ClrType? type = _helpers.Factory.GetOrCreateType(Heap, mt, addr);
            if (type is null)
                return 0;

            ulong size;
            if (type.ComponentSize == 0)
            {
                size = (uint)type.StaticSize;
            }
            else
            {
                uint count = memoryReader.Read<uint>(addr + (uint)IntPtr.Size);

                // Strings in v4+ contain a trailing null terminator not accounted for.
                if (Heap.StringType == type)
                    count++;

                size = count * (ulong)type.ComponentSize + (ulong)type.StaticSize;
            }

            size = ClrmdHeap.Align(size, large);
            if (size < minObjSize)
                size = minObjSize;

            ulong obj = addr + size;

            if (!large)
                obj = _clrmdHeap.SkipAllocationContext(this, obj); // ignore mt here because it won't be used

            if (obj >= End)
                return 0;

            int marker = GetMarkerIndex(obj);
            if (marker != -1 && _markers[marker] == 0)
                _markers[marker] = obj;

            return obj;
        }
    }
}