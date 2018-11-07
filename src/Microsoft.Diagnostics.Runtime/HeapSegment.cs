// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class HeapSegment : ClrSegment
    {
        public override int ProcessorAffinity
        {
            get { return _subHeap.HeapNum; }
        }
        public override ulong Start { get { return _segment.Start; } }
        public override ulong End { get { return _subHeap.EphemeralSegment == _segment.Address ? _subHeap.EphemeralEnd : _segment.End; } }
        public override ClrHeap Heap { get { return _heap; } }

        public override bool IsLarge { get { return _large; } }

        public override ulong ReservedEnd { get { return _segment.Reserved; } }
        public override ulong CommittedEnd { get { return _segment.Committed; } }

        public override ulong Gen0Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen0Start;
                else
                    return End;
            }
        }
        public override ulong Gen0Length { get { return End - Gen0Start; } }
        public override ulong Gen1Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen1Start;
                else
                    return End;
            }
        }
        public override ulong Gen1Length { get { return Gen0Start - Gen1Start; } }
        public override ulong Gen2Start { get { return Start; } }
        public override ulong Gen2Length { get { return Gen1Start - Start; } }


        public override IEnumerable<ulong> EnumerateObjectAddresses()
        {
            for (ulong obj = FirstObject; obj != 0; obj = NextObject(obj))
                yield return obj;
        }

        public override ulong FirstObject
        {
            get
            {
                ulong start = Gen2Start;
                if (start >= End)
                    return 0;

                _heap.MemoryReader.EnsureRangeInCache(start);
                return start;
            }
        }

        public override ulong GetFirstObject(out ClrType type)
        {
            ulong start = Gen2Start;
            if (start >= End)
            {
                type = null;
                return 0;
            }

            _heap.MemoryReader.EnsureRangeInCache(start);
            type = _heap.GetObjectType(start);
            return start;
        }

        public override ulong NextObject(ulong objRef)
        {
            if (objRef >= CommittedEnd)
                return 0;

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType currType = _heap.GetObjectType(objRef);
            if (currType == null)
                return 0;

            ulong size = currType.GetSize(objRef);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            objRef += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (objRef >= End)
                return 0;

            // Ensure we aren't at the start of an alloc context
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(objRef, out ulong tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (objRef >= tmp)
                    return 0;

                // Otherwise:
                objRef = tmp;

                if (objRef >= End)
                    return 0;
            }
            
            return objRef;
        }

        public override ulong NextObject(ulong objRef, out ClrType type)
        {
            if (objRef >= CommittedEnd)
            {
                type = null;
                return 0;
            }

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType currType = _heap.GetObjectType(objRef);
            if (currType == null)
            {
                type = null;
                return 0;
            }

            ulong size = currType.GetSize(objRef);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            objRef += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (objRef >= End)
            {
                type = null;
                return 0;
            }

            // Ensure we aren't at the start of an alloc context
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(objRef, out ulong tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (objRef >= tmp)
                {
                    type = null;
                    return 0;
                }

                // Otherwise:
                objRef = tmp;

                if (objRef >= End)
                {
                    type = null;
                    return 0;
                }
            }

            type = _heap.GetObjectType(objRef);
            return objRef;
        }

        #region private
        internal static ulong Align(ulong size, bool large)
        {
            ulong AlignConst;
            ulong AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~(AlignLargeConst);

            return (size + AlignConst) & ~(AlignConst);
        }

        public override bool IsEphemeral { get { return _segment.Address == _subHeap.EphemeralSegment; ; } }
        internal HeapSegment(RuntimeBase clr, ISegmentData segment, SubHeap subHeap, bool large, HeapBase heap)
        {
            _clr = clr;
            _large = large;
            _segment = segment;
            _heap = heap;
            _subHeap = subHeap;
        }

        private bool _large;
        private RuntimeBase _clr;
        private ISegmentData _segment;
        private SubHeap _subHeap;
        private HeapBase _heap;
        #endregion
    }

}
