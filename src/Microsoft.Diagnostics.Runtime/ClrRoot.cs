// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    public class ClrRoot : IClrRoot
    {
        /// <summary>
        /// Gets the address in memory of the root.  Typically dereferencing this address will
        /// give you the associated Object, but not always.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Gets the object the root points to.
        /// </summary>
        public virtual ClrObject Object { get; }

        IClrValue IClrRoot.Object => Object;

        /// <summary>
        /// Gets the kind of root this is.
        /// </summary>
        public ClrRootKind RootKind { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether Address may point to the interior of an object (i.e. not the start of an object).
        /// If Address happens to point to the start of the object, ClrRoot.Object will be filled
        /// as normal, otherwise ClrRoot.Object.IsNull will be <see langword="true"/>.  In order to properly account
        /// for interior objects, you must read the value out of Address then find the object which
        /// contains it.
        /// </summary>
        public bool IsInterior { get; }

        /// <summary>
        /// Gets a value indicating whether the object is pinned in place by this root and will not be relocated by the GC.
        /// </summary>
        public bool IsPinned { get; }

        /// <summary>
        /// The stack frame holding this root.  Non-null if RootKind == Stack.
        /// </summary>
        public virtual ClrStackFrame? StackFrame => null;
        IClrStackFrame? IClrRoot.StackFrame => StackFrame;

        public ClrRoot(ulong address, ClrObject obj, ClrRootKind rootKind, bool isInterior, bool isPinned)
        {
            Address = address;
            Object = obj;
            RootKind = rootKind;
            IsInterior = isInterior;
            IsPinned = isPinned;
        }
    }

    internal sealed class ClrStackRoot : ClrRoot
    {
        private readonly ClrHeap _heap;
        private ClrObject _object;

        public ClrStackRoot(ulong address, ClrObject obj, bool isInterior, bool isPinned, ClrHeap heap, ClrStackFrame? frame)
            : base(address, obj, ClrRootKind.Stack, isInterior, isPinned)
        {
            _heap = heap;
            StackFrame = frame;
        }

        public override ClrStackFrame? StackFrame { get; }

        public override ClrObject Object
        {
            get
            {
                if (_object.Address != 0)
                    return _object;

                ClrObject obj = base.Object;
                if (obj.Type is not null)
                {
                    _object = obj;
                }
                else
                {
                    ClrObject prev = _heap.FindPreviousObjectOnSegment(obj);
                    if (prev.IsValid && prev <= obj && obj < prev + prev.Size)
                    {
                        _object = prev;
                    }
                    else
                    {
                        _object = obj;
                    }
                }

                return _object;
            }
        }
    }
}
