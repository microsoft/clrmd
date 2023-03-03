// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class ClrStackInteriorRoot : IClrStackRoot
    {
        private ClrObject? _object;
        private readonly ClrSegment _segment;

        public ulong Address { get; }

        public ClrObject Object => AsObject() ?? default;

        public ClrStackFrame StackFrame { get; }

        public ClrRootKind RootKind => ClrRootKind.Stack;

        public bool IsInterior => true;

        public bool IsPinned { get; }

        public ulong ObjectPointer { get; }

        public ClrStackInteriorRoot(ClrSegment seg, ulong address, ulong objAddr, ClrStackFrame stackFrame, bool pinned)
        {
            _segment = seg;
            ObjectPointer = objAddr;

            Address = address;
            StackFrame = stackFrame;
            IsPinned = pinned;
        }

        public ClrObject? AsObject()
        {
            if (_object.HasValue)
                return _object.Value;

            // It's possible that ObjectPointer points the beginning of an object, though that's rare.  Check that first.
            ClrHeap heap = _segment.SubHeap.Heap;
            ClrType? type = heap.GetObjectType(ObjectPointer);
            if (type is not null)
            {
                _object = new ClrObject(ObjectPointer, type);
                return _object.Value;
            }

            // ObjectPointer is pointing in the middle of an object, get the previous object for the address.
            return heap.FindPreviousObjectOnSegment(ObjectPointer);
        }

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"stack root @{Address:x12} -> {Object}";
    }
}
