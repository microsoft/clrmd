// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a CLR handle in the target process.
    /// </summary>
    public abstract class ClrHandle : IClrRoot
    {
        /// <summary>
        /// Gets the address of the handle itself.  That is, *ulong == Object.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Gets the Object the handle roots.
        /// </summary>
        public abstract ClrObject Object { get; }

        /// <summary>
        /// Gets the type of handle.
        /// </summary>
        public abstract ClrHandleKind HandleKind { get; }

        /// <summary>
        /// If this handle is a RefCount handle, this returns the reference count.
        /// RefCount handles with a RefCount > 0 are strong.
        /// </summary>
        public abstract uint ReferenceCount { get; }

        /// <summary>
        /// Gets the dependent handle target if this is a dependent handle.
        /// </summary>
        public abstract ClrObject Dependent { get; }

        /// <summary>
        /// Gets the AppDomain the handle resides in.
        /// </summary>
        public abstract ClrAppDomain AppDomain { get; }

        /// <summary>
        /// Gets a value indicating whether the handle is strong (roots the object).
        /// </summary>
        public bool IsStrong
        {
            get
            {
                switch (HandleKind)
                {
                    case ClrHandleKind.RefCounted:
                        return ReferenceCount > 0;

                    case ClrHandleKind.WeakLong:
                    case ClrHandleKind.WeakShort:
                    case ClrHandleKind.Dependent:
                    case ClrHandleKind.WeakWinRT:
                        return false;

                    default:
                        return true;
                }
            }
        }

        public ClrRootKind RootKind => IsStrong ? (ClrRootKind)HandleKind : ClrRootKind.None;

        public bool IsInterior => false;

        /// <summary>
        /// Gets a value indicating whether the handle pins the object (doesn't allow the GC to
        /// relocate it).
        /// </summary>
        public bool IsPinned => HandleKind == ClrHandleKind.AsyncPinned || HandleKind == ClrHandleKind.Pinned;

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{HandleKind.GetName()} @{Address:x12} -> {Object}";
    }
}
