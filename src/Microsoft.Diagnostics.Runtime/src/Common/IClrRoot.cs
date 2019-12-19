// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrRoot
    {
        /// <summary>
        /// Gets the address in memory of the root.  Typically dereferencing this address will
        /// give you the associated Object, but not always.
        /// </summary>
        ulong Address { get; }

        /// <summary>
        /// Gets the object the root points to.
        /// </summary>
        ClrObject Object { get; }

        /// <summary>
        /// Gets the kind of root this is.
        /// </summary>
        ClrRootKind RootKind { get; }

        /// <summary>
        /// Gets a value indicating whether Address may point to the interior of an object (i.e. not the start of an object).
        /// If Address happens to point to the start of the object, IClrRoot.Object will be filled
        /// as normal, otherwise IClrRoot.Object.IsNull will be <see langword="true"/>.  In order to properly account
        /// for interior objects, you must read the value out of Address then find the object which
        /// contains it.
        /// </summary>
        bool IsInterior { get; }

        /// <summary>
        /// Gets a value indicating whether the object is pinned in place by this root and will not be relocated by the GC.
        /// </summary>
        bool IsPinned { get; }
    }
}
