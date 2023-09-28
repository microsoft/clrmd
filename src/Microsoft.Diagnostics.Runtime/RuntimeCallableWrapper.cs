// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.Diagnostics.Runtime.AbstractDac;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Helper for Runtime Callable Wrapper objects.  (RCWs are COM objects which are exposed to the runtime
    /// as managed objects.)
    /// </summary>
    public sealed class RuntimeCallableWrapper : IRuntimeCallableWrapper
    {
        public ulong Address { get; }

        /// <summary>
        /// Gets the pointer to the IUnknown representing this CCW.
        /// </summary>
        public ulong IUnknown { get; }

        /// <summary>
        /// Gets the external VTable associated with this RCW.  (It's useful to resolve the VTable as a symbol
        /// which will tell you what the underlying native type is...if you have the symbols for it loaded).
        /// </summary>
        public ulong VTablePointer { get; }

        /// <summary>
        /// Gets the RefCount of the RCW.
        /// </summary>
        public int RefCount { get; }

        /// <summary>
        /// Gets the managed object associated with this of RCW.
        /// </summary>
        public ulong Object { get; }

        /// <summary>
        /// Gets a value indicating whether the RCW is disconnected from the underlying COM type.
        /// </summary>
        public bool IsDisconnected { get; }

        /// <summary>
        /// Gets the thread which created this RCW.
        /// </summary>
        public ulong CreatorThreadAddress { get; }

        /// <summary>
        /// Gets the internal WinRT object associated with this RCW (if one exists).
        /// </summary>
        public ulong WinRTObject { get; }

        /// <summary>
        /// Gets the list of interfaces this RCW implements.
        /// </summary>
        public ImmutableArray<ComInterfaceData> Interfaces { get; }

        internal RuntimeCallableWrapper(ClrRuntime runtime, in RcwInfo rcw)
        {
            Address = rcw.Address;
            IUnknown = rcw.IUnknown;
            VTablePointer = rcw.VTablePointer;
            RefCount = rcw.RefCount;
            Object = rcw.Object;
            IsDisconnected = rcw.IsDisconnected;
            CreatorThreadAddress = rcw.CreatorThread;

            ClrHeap heap = runtime.Heap;
            Interfaces = rcw.Interfaces.Select(r => new ComInterfaceData(heap.GetTypeByMethodTable(r.MethodTable), r.InterfacePointer)).ToImmutableArray();
        }
    }
}