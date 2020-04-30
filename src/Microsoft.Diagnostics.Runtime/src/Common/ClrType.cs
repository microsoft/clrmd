// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of a type in the target process.
    /// </summary>
    public abstract class ClrType :
#nullable disable // to enable use with both T and T? for reference types due to IEquatable<T> being invariant
        IEquatable<ClrType>
#nullable restore
    {
        /// <summary>
        /// Gets the <see cref="GCDesc"/> associated with this type.  Only valid if <see cref="ContainsPointers"/> is <see langword="true"/>.
        /// </summary>
        public abstract GCDesc GCDesc { get; }

        /// <summary>
        /// Gets the MethodTable of this type (this is the TypeHandle if this is a type without a MethodTable).
        /// </summary>
        public abstract ulong MethodTable { get; }

        /// <summary>
        /// Gets the metadata token of this type.
        /// </summary>
        public abstract int MetadataToken { get; }

        /// <summary>
        /// Gets the name of this type.
        /// </summary>
        public abstract string? Name { get; }

        /// <summary>
        /// Gets a value indicating whether the type <b>can</b> contain references to other objects.  This is used in optimizations
        /// and 'true' can always be returned safely.
        /// </summary>
        public virtual bool ContainsPointers => true;

        /// <summary>
        /// Gets a value indicating whether this is a collectible type.
        /// </summary>
        public virtual bool IsCollectible => false;

        /// <summary>
        /// Gets the handle to the <c>LoaderAllocator</c> object for collectible types.
        /// </summary>
        public virtual ulong LoaderAllocatorHandle => 0;

        /// <summary>
        /// Gets the <see cref="ClrHeap"/> this type belongs to.
        /// </summary>
        public abstract ClrHeap Heap { get; }

        /// <summary>
        /// Gets the module this type is defined in.
        /// </summary>
        public abstract ClrModule? Module { get; }

        /// <summary>
        /// Gets the <see cref="ClrElementType"/> of this Type.  Can return <see cref="ClrElementType.Unknown"/> on error.
        /// </summary>
        public abstract ClrElementType ElementType { get; }

        /// <summary>
        /// Gets a value indicating whether this type is a primitive (<see cref="int"/>, <see cref="float"/>, etc).
        /// </summary>
        /// <returns>True if this type is a primitive (<see cref="int"/>, <see cref="float"/>, etc), false otherwise.</returns>
        public virtual bool IsPrimitive => ElementType.IsPrimitive();

        /// <summary>
        /// Gets a value indicating whether this type is a value type.
        /// </summary>
        /// <returns>True if this type is a value type, false otherwise.</returns>
        public virtual bool IsValueType => ElementType.IsValueType();

        /// <summary>
        /// Gets a value indicating whether this type is an object reference.
        /// </summary>
        /// <returns>True if this type is an object reference, false otherwise.</returns>
        public virtual bool IsObjectReference => ElementType.IsObjectReference();

        public abstract IEnumerable<ClrGenericParameter> EnumerateGenericParameters();

        /// <summary>
        /// Returns the list of interfaces this type implements.
        /// </summary>
        public abstract IEnumerable<ClrInterface> EnumerateInterfaces();

        /// <summary>
        /// Returns true if the finalization is suppressed for an object (the user program called
        /// <see cref="GC.SuppressFinalize"/>). The behavior of this function is undefined if the object itself
        /// is not finalizable.
        /// </summary>
        public abstract bool IsFinalizeSuppressed(ulong obj);

        /// <summary>
        /// Gets a value indicating whether objects of this type are finalizable.
        /// </summary>
        public abstract bool IsFinalizable { get; }

        // Visibility:
        /// <summary>
        /// Gets a value indicating whether this type is marked Public.
        /// </summary>
        public abstract bool IsPublic { get; }

        /// <summary>
        /// Gets a value indicating whether this type is marked Private.
        /// </summary>
        public abstract bool IsPrivate { get; }

        /// <summary>
        /// Gets a value indicating whether this type is accessible only by items in its own assembly.
        /// </summary>
        public abstract bool IsInternal { get; }

        /// <summary>
        /// Gets a value indicating whether this nested type is accessible only by subtypes of its outer type.
        /// </summary>
        public abstract bool IsProtected { get; }

        // Other attributes:

        /// <summary>
        /// Gets a value indicating whether this class is abstract.
        /// </summary>
        public abstract bool IsAbstract { get; }

        /// <summary>
        /// Gets a value indicating whether this class is sealed.
        /// </summary>
        public abstract bool IsSealed { get; }

        /// <summary>
        /// Gets a value indicating whether this type is an interface.
        /// </summary>
        public abstract bool IsInterface { get; }

        /// <summary>
        /// Gets all possible fields in this type.   It does not return dynamically typed fields.
        /// Returns an empty list if there are no fields.
        /// </summary>
        public abstract ImmutableArray<ClrInstanceField> Fields { get; }

        /// <summary>
        /// Gets a list of static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        public abstract ImmutableArray<ClrStaticField> StaticFields { get; }

        /// <summary>
        /// Gets the list of methods this type implements.
        /// </summary>
        public abstract ImmutableArray<ClrMethod> Methods { get; }

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrInstanceField? GetInstanceFieldByName(string name);

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrStaticField? GetStaticFieldByName(string name);

        /// <summary>
        /// If this type inherits from another type, this is that type.  Can return <see langword="null"/> if it does not inherit (or is unknown).
        /// </summary>
        public abstract ClrType? BaseType { get; }

        /// <summary>
        /// Gets a value indicating whether the type is in fact a pointer. If so, the pointer operators
        /// may be used.
        /// </summary>
        public virtual bool IsPointer => false;

        /// <summary>
        /// Gets the type of the element referenced by the pointer.
        /// </summary>
        public abstract ClrType? ComponentType { get; }

        /// <summary>
        /// A type is an array if you can use the array operators below, Abstractly arrays are objects
        /// that whose children are not statically known by just knowing the type.
        /// </summary>
        public abstract bool IsArray { get; }

        /// <summary>
        /// Returns the absolute address to the given array element.  You may then make a direct memory read out
        /// of the process to get the value if you want.
        /// </summary>
        public abstract ulong GetArrayElementAddress(ulong objRef, int index);

        /// <summary>
        /// Returns multiple consecutive array element values.
        /// </summary>
        public abstract T[]? ReadArrayElements<T>(ulong objRef, int start, int count) where T : unmanaged;

        /// <summary>
        /// Gets the static size of objects of this type when they are created on the CLR heap.
        /// </summary>
        public abstract int StaticSize { get; }

        /// <summary>
        /// Gets the size of elements of this object.
        /// </summary>
        public abstract int ComponentSize { get; }

        /// <summary>
        /// Gets a value indicating whether this type is <see cref="string"/>.
        /// </summary>
        public virtual bool IsString => false;

        /// <summary>
        /// Gets a value indicating whether this type represents free space on the heap.
        /// </summary>
        public virtual bool IsFree => false;

        /// <summary>
        /// Gets a value indicating whether this type is an exception (that is, it derives from <see cref="Exception"/>).
        /// </summary>
        public virtual bool IsException => false;

        /// <summary>
        /// Gets a value indicating whether this type is an enum.
        /// </summary>
        public abstract bool IsEnum { get; }

        /// <summary>
        /// Returns the <see cref="ClrEnum"/> representation of this type.
        /// </summary>
        /// <returns>The <see cref="ClrEnum"/> representation of this type.</returns>
        /// <exception cref="InvalidOperationException"><see cref="IsEnum"/> is <see langword="false"/>.</exception>
        public abstract ClrEnum AsEnum();

        /// <summary>
        /// Gets a value indicating whether this type is shared across multiple AppDomains.
        /// </summary>
        public abstract bool IsShared { get; }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string? ToString() => Name;

        /// <summary>
        /// Used to provide functionality to ClrObject.
        /// </summary>
        public abstract IClrObjectHelpers ClrObjectHelpers { get; }

        public override bool Equals(object? obj) => Equals(obj as ClrType);

        public bool Equals(ClrType? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            if (MethodTable != 0 && other.MethodTable != 0)
                return MethodTable == other.MethodTable;

            if (other.IsPointer)
                return ComponentType == other.ComponentType;

            if (IsPrimitive && other.IsPrimitive && ElementType != ClrElementType.Unknown)
                return ElementType == other.ElementType;

            // Ok we aren't a primitive type, or a pointer, and our MethodTables are 0.  Last resort is to
            // check if we resolved from the same token out of the same module.
            if (Module != null && MetadataToken != 0)
                return Module == other.Module && MetadataToken == other.MetadataToken;

            return false;
        }

        public override int GetHashCode() => MethodTable.GetHashCode();

        public static bool operator ==(ClrType? left, ClrType? right)
        {
            if (right is null)
                return left is null;

            return right.Equals(left);
        }

        public static bool operator !=(ClrType? left, ClrType? right)
        {
            return !(left == right);
        }
    }
}
