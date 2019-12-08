// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a Clr handle in the target process.
    /// </summary>
    public abstract class ClrHandle : IClrRoot
    {
        /// <summary>
        /// The address of the handle itself.  That is, *ulong == Object.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// The Object the handle roots.
        /// </summary>
        public abstract ClrObject Object { get; }

        /// <summary>
        /// Gets the type of handle.
        /// </summary>
        public abstract ClrHandleKind HandleKind { get; }

        /// <summary>
        /// If this handle is a RefCount handle, this returns the reference count.
        /// RefCount handles with a RefCount > 0 are strong.
        /// NOTE: v2 CLR CANNOT determine the RefCount.  We always set the RefCount
        /// to 1 in a v2 query since a strong RefCount handle is the common case.
        /// </summary>
        public abstract uint ReferenceCount { get; }

        /// <summary>
        /// The dependent handle target if this is a dependent handle.
        /// </summary>
        public abstract ClrObject Dependent { get; }

        /// The AppDomain the handle resides in.
        /// </summary>
        public abstract ClrAppDomain AppDomain { get; }

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return HandleKind + " " + (Object.Type?.Name ?? string.Empty);
        }

        /// <summary>
        /// Whether the handle is strong (roots the object) or not.
        /// </summary>
        public bool IsStrong
        {
            get
            {
                switch (HandleKind)
                {
                    case ClrHandleKind.RefCount:
                        return ReferenceCount > 0;

                    case ClrHandleKind.WeakLong:
                    case ClrHandleKind.WeakShort:
                    case ClrHandleKind.Dependent:
                        return false;

                    default:
                        return true;
                }
            }
        }

        public ClrRootKind RootKind => IsStrong ? (ClrRootKind)HandleKind : ClrRootKind.None;

        public bool IsInterior => false;

        /// <summary>
        /// Whether or not the handle pins the object (doesn't allow the GC to
        /// relocate it) or not.
        /// </summary>
        public bool IsPinned => HandleKind == ClrHandleKind.AsyncPinned || HandleKind == ClrHandleKind.Pinned;
    }
}
