// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime
{
    public struct ClrFieldReference
    {
        const ulong OffsetFlag = 1 << 61;
        const ulong DependentFlag = 1 << 60;
        const ulong ValueMask = ~(OffsetFlag | DependentFlag);

        private readonly ulong _offsetOrHandle;

        /// <summary>
        /// The object that <see cref="Field"/> contained.
        /// </summary>
        public ClrObject Object { get; }

        /// <summary>
        /// The offset into the containing object this address is found at.  Only valid if <see cref="IsObjectInterior"/> is true.
        /// </summary>
        public int Offset
        {
            get
            {
                if ((_offsetOrHandle & OffsetFlag) == OffsetFlag)
                {
                    unchecked
                    {
                        // The (uint) cast will slice off the high bits
                        return (int)(uint)_offsetOrHandle;
                    }
                }

                return -1;
            }
        }

        /// <summary>
        /// The field this object was contained in.  This property may be null if metadata is not available, if
        /// this reference came from a dependent handle/loader allocator, or if this came from an array.
        /// Only valid to call if <see cref="IsObjectInterior"/> is true.
        /// </summary>
        public ClrInstanceField? Field { get; }

        /// <summary>
        /// If this reference came from a collectible type, it means there's a collectible type which keeps this object alive.
        /// In that case, the <see cref="LoaderAllocator"/> property contains the address of the loader allocator's handle.
        /// </summary>
        public ulong LoaderAllocator
        {
            get
            {
                // Return the loader allocator handle if this doesn't have the high bits set.
                if ((_offsetOrHandle & ValueMask) == _offsetOrHandle)
                    return ValueMask & _offsetOrHandle;

                return 0;
            }
        }

        /// <summary>
        /// Returns true if this reference came from a LoaderAllocator handle (e.g. collectible types).
        /// </summary>
        public bool IsLoaderAllocator => LoaderAllocator != 0;

        /// <summary>
        /// Returns true if this reference came from a dependent handle.
        /// </summary>
        public bool IsDepenendentHandle => (_offsetOrHandle & DependentFlag) == DependentFlag;

        /// <summary>
        /// Returns true if this reference came from a field in another object.
        /// </summary>
        public bool IsObjectInterior => LoaderAllocator == 0;

        /// <summary>
        /// Create a field reference from a dependent handle value.  We do not keep track of the dependent handle it came from
        /// so we don't accept the value here.
        /// </summary>
        /// <param name="reference">The object referenced.</param>
        public static ClrFieldReference CreateFromDependentHandle(ClrObject reference) => new ClrFieldReference(reference, null, DependentFlag);

        /// <summary>
        /// Creates a ClrFieldReference from an actual field.
        /// </summary>
        /// <param name="reference">The object referenced.</param>
        /// <param name="containingType">The type of the object which points to <paramref name="reference"/>.</param>
        /// <param name="offset">The offset within the source object where <paramref name="reference"/> was located.</param>
        public static ClrFieldReference CreateFromField(ClrObject reference, ClrType containingType, int offset)
        {
            if (containingType == null)
                throw new ArgumentNullException(nameof(containingType));

            ClrInstanceField? field = containingType.Fields.FirstOrDefault(f => f.Offset <= offset && offset < f.Offset + f.Size);
            unchecked
            {
                return new ClrFieldReference(reference, field, OffsetFlag | (uint)offset);
            }
        }

        /// <summary>
        /// Creates a ClrFieldReference from a collectable type's LoaderAllocator handle.
        /// </summary>
        /// <param name="reference">The object referenced.</param>
        /// <param name="loaderAllocator">The address of the LoaderAllocator handle.</param>
        public static ClrFieldReference CreateFromLoaderAllocatorHandle(ClrObject reference, ulong loaderAllocator) => new ClrFieldReference(reference, null, loaderAllocator);

        private ClrFieldReference(ClrObject obj, ClrInstanceField? field, ulong offsetOrHandleValue)
        {
            _offsetOrHandle = offsetOrHandleValue;
            Object = obj;
            Field = field;
        }
    }
}
