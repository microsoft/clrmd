// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Helper for COM Callable Wrapper objects.  (CCWs are CLR objects exposed to native code as COM
    /// objects).
    /// </summary>
    public sealed class ComCallableWrapper
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

        public ComCallableWrapper(ICcwData data)
        {
            if (data is null)
                throw new System.ArgumentNullException(nameof(data));

            Address = data.Address;
            IUnknown = data.IUnknown;
            Object = data.Object;
            Handle = data.Handle;
            RefCount = data.RefCount + data.JupiterRefCount;
            Interfaces = data.GetInterfaces();
        }
    }
}