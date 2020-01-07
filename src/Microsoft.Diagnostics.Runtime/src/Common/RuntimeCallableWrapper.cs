// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Helper for Runtime Callable Wrapper objects.  (RCWs are COM objects which are exposed to the runtime
    /// as managed objects.)
    /// </summary>
    public sealed class RuntimeCallableWrapper
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
        public ClrThread? CreatorThread { get; }

        /// <summary>
        /// Gets the internal WinRT object associated with this RCW (if one exists).
        /// </summary>
        public ulong WinRTObject { get; }

        /// <summary>
        /// Gets the list of interfaces this RCW implements.
        /// </summary>
        public ImmutableArray<ComInterfaceData> Interfaces { get; }

        public RuntimeCallableWrapper(ClrRuntime runtime, IRcwData data)
        {
            if (data is null)
                throw new System.ArgumentNullException(nameof(data));

            Address = data.Address;
            IUnknown = data.IUnknown;
            VTablePointer = data.VTablePointer;
            RefCount = data.RefCount;
            Object = data.ManagedObject;
            IsDisconnected = data.Disconnected;
            CreatorThread = runtime?.Threads.FirstOrDefault(t => t.Address == data.CreatorThread);
            Interfaces = data.GetInterfaces();
        }
    }
}