// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public class ClrmdSegment : ClrSegment
    {
        private readonly IHeapHelpers _helpers;
        private readonly ClrmdHeap _clrmdHeap;

        public ClrmdSegment(ClrmdHeap heap, IHeapHelpers helpers, ISegmentData data)
        {
            if (helpers is null)
                throw new ArgumentNullException(nameof(helpers));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _helpers = helpers;
            _clrmdHeap = heap;

            LogicalHeap = data.LogicalHeap;
            Start = data.Start;
            End = data.End;

            IsLargeObjectSegment = data.IsLargeObjectSegment;
            IsEphemeralSegment = data.IsEphemeralSegment;

            ReservedEnd = data.ReservedEnd;
            CommittedEnd = data.CommitedEnd;

            Gen0Start = data.Gen0Start;
            Gen0Length = data.Gen0Length;
            Gen1Start = data.Gen1Start;
            Gen1Length = data.Gen1Length;
            Gen2Start = data.Gen2Start;
            Gen2Length = data.Gen2Length;
        }

        public override ClrHeap Heap => _clrmdHeap;

        public override int LogicalHeap { get; }
        public override ulong Start { get; }
        public override ulong End { get; }

        public override bool IsLargeObjectSegment { get; }
        public override bool IsEphemeralSegment { get; }

        public override ulong ReservedEnd { get; }
        public override ulong CommittedEnd { get; }

        public override ulong Gen0Start { get; }
        public override ulong Gen0Length { get; }
        public override ulong Gen1Start { get; }
        public override ulong Gen1Length { get; }
        public override ulong Gen2Start { get; }
        public override ulong Gen2Length { get; }
        public override ulong FirstObjectAddress => Gen2Start < End ? Gen2Start : 0;

        public override IEnumerable<ClrObject> EnumerateObjects() => EnumerateObjects(null);
        
        public IEnumerable<ClrObject> EnumerateObjects(Action<ulong, ulong, int, int, uint>? callback)
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

            while (obj < CommittedEnd)
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
                {
                    callback?.Invoke(obj, mt, int.MinValue + 1, -1, 0);
                    break;
                }

                ClrObject result = new ClrObject(obj, type);
                yield return result;

                ulong size;
                if (type.ComponentSize == 0)
                {
                    size = (uint)type.StaticSize;
                    callback?.Invoke(obj, mt, type.StaticSize, -1, 0);
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
                    callback?.Invoke(obj, mt, type.StaticSize, type.ComponentSize, count);
                }

                size = ClrmdHeap.Align(size, large);
                if (size < minObjSize)
                    size = minObjSize;

                obj += size;
                obj = _clrmdHeap.SkipAllocationContext(this, obj, mt, callback);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }


        public override ulong NextObject(ulong addr)
        {
            if (addr == 0)
                return 0;

            if (addr < FirstObjectAddress || addr >= CommittedEnd)
                throw new InvalidOperationException($"Segment [{FirstObjectAddress:x},{CommittedEnd:x}] does not contain object {addr:x}");

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
                obj = _clrmdHeap.SkipAllocationContext(this, obj, 0, null); // ignore mt here because it won't be used

            if (obj >= End)
                return 0;

            return obj;
        }
    }
}