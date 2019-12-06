// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a Clr handle in the target process.
    /// </summary>
    public sealed class ClrHandle : IClrRoot
    {
        /// <summary>
        /// The address of the handle itself.  That is, *ulong == Object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The Object the handle roots.
        /// </summary>
        public ClrObject Object { get; }

        /// <summary>
        /// Gets the type of handle.
        /// </summary>
        public ClrHandleKind HandleKind { get; }

        /// <summary>
        /// If this handle is a RefCount handle, this returns the reference count.
        /// RefCount handles with a RefCount > 0 are strong.
        /// NOTE: v2 CLR CANNOT determine the RefCount.  We always set the RefCount
        /// to 1 in a v2 query since a strong RefCount handle is the common case.
        /// </summary>
        public uint RefCount { get; }

        /// <summary>
        /// The dependent handle target if this is a dependent handle.
        /// </summary>
        public ClrObject Dependent { get; }

        /// The AppDomain the handle resides in.
        /// </summary>
        public ClrAppDomain AppDomain { get; }

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return HandleKind + " " + (Object.Type?.Name ?? string.Empty);
        }

        public ClrHandle(ulong address, ClrObject obj, ClrHandleKind handleKind, uint refCount, ClrObject dependent, ClrAppDomain domain)
        {
            Address = address;
            Object = obj;
            AppDomain = domain;
            HandleKind = handleKind;
            RefCount = refCount;
            Dependent = dependent;
        }

        public ClrHandle(in HandleData handleData, ClrObject obj, ClrAppDomain domain, ClrType? dependentSecondary)
        {
            Address = handleData.Handle;

            Object = obj;

            uint refCount = 0;

            if (handleData.Type == (int)ClrHandleKind.RefCount)
            {
                if (handleData.IsPegged != 0)
                    refCount = handleData.JupiterRefCount;

                if (refCount < handleData.RefCount)
                    refCount = handleData.RefCount;

                if (!obj.IsNull)
                {
                    ComCallWrapper? ccw = obj.Type?.GetCCWData(obj);
                    if (ccw != null && refCount < ccw.RefCount)
                    {
                        refCount = (uint)ccw.RefCount;
                    }
                    else
                    {
                        RuntimeCallableWrapper? rcw = obj.Type?.GetRCWData(obj);
                        if (rcw != null && refCount < rcw.RefCount)
                            refCount = (uint)rcw.RefCount;
                    }
                }

                RefCount = refCount;
            }

            HandleKind = (ClrHandleKind)handleData.Type;
            AppDomain = domain;

            if (HandleKind == ClrHandleKind.Dependent)
                Dependent = new ClrObject(handleData.Secondary, dependentSecondary);
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
                        return RefCount > 0;

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
