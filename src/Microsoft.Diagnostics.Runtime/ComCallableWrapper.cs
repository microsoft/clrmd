// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Helper for COM Callable Wrapper objects.  (CCWs are CLR objects exposed to native code as COM
    /// objects).
    /// </summary>
    public sealed class ComCallableWrapper : IComCallableWrapper
    {
        public ulong Address { get; }

        /// <summary>
        /// Gets the pointer to the IUnknown representing this CCW.
        /// </summary>
        public ulong IUnknown { get; }

        /// <summary>
        /// Gets the pointer to the managed object representing this CCW.
        /// </summary>
        public ulong Object { get; }

        /// <summary>
        /// Gets the CLR handle associated with this CCW.
        /// </summary>
        public ulong Handle { get; }

        /// <summary>
        /// Gets the refcount of this CCW.
        /// </summary>
        public int RefCount { get; }

        /// <summary>
        /// Gets the interfaces that this CCW implements.
        /// </summary>
        public ImmutableArray<ComInterfaceData> Interfaces { get; }

        internal ComCallableWrapper(ClrRuntime runtime, in CcwInfo ccw)
        {
            Address = ccw.Address;
            IUnknown = ccw.IUnknown;
            Object = ccw.Object;
            Handle = ccw.Handle;
            RefCount = ccw.RefCount + ccw.JupiterRefCount;

            ClrHeap heap = runtime.Heap;
            Interfaces = ccw.Interfaces.Select(r => new ComInterfaceData(heap.GetTypeByMethodTable(r.MethodTable), r.InterfacePointer)).ToImmutableArray();
        }
    }
}