// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Helper for Com Callable Wrapper objects.  (CCWs are CLR objects exposed to native code as COM
    /// objects).
    /// </summary>
    public abstract class CcwData
    {
        /// <summary>
        /// Returns the pointer to the IUnknown representing this CCW.
        /// </summary>
        public abstract ulong IUnknown { get; }

        /// <summary>
        /// Returns the pointer to the managed object representing this CCW.
        /// </summary>
        public abstract ulong Object { get; }

        /// <summary>
        /// Returns the CLR handle associated with this CCW.
        /// </summary>
        public abstract ulong Handle { get; }

        /// <summary>
        /// Returns the refcount of this CCW.
        /// </summary>
        public abstract int RefCount { get; }

        /// <summary>
        /// Returns the interfaces that this CCW implements.
        /// </summary>
        public abstract IList<ComInterfaceData> Interfaces { get; }
    }
}