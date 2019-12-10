// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of a type in the target process.
    /// </summary>
    public abstract class ClrType
    {
        /// <summary>
        /// Gets the GCDesc associated with this type.  Only valid if ContainsPointers returns true.
        /// </summary>
        public abstract GCDesc GCDesc { get; }

        /// <summary>
        /// The MethodTable of this type (this is the TypeHandle if this is a type without a MethodTable).
        /// </summary>
        public abstract ulong MethodTable { get; }

        /// <summary>
        /// Returns the metadata token of this type.
        /// </summary>
        public abstract uint MetadataToken { get; }

        /// <summary>
        /// Types have names.
        /// </summary>
        public abstract string? Name { get; }

        /// <summary>
        /// Returns true if the type CAN contain references to other objects.  This is used in optimizations
        /// and 'true' can always be returned safely.
        /// </summary>
        public virtual bool ContainsPointers => true;

        /// <summary>
        /// Whether this is a collectible type or not.
        /// </summary>
        public virtual bool IsCollectible => false;

        /// <summary>
        /// The handle to the LoaderAllocator object for collectible types.
        /// </summary>
        public virtual ulong LoaderAllocatorHandle => 0;

        /// <summary>
        /// All types know the heap they belong to.
        /// </summary>
        public abstract ClrHeap Heap { get; }

        /// <summary>
        /// Returns the module this type is defined in.
        /// </summary>
        public abstract ClrModule? Module { get; }

        /// <summary>
        /// Returns the <see cref="ClrElementType"/> of this Type.  Can return <see cref="ClrElementType.Unknown"/> on error.
        /// </summary>
        public abstract ClrElementType ElementType { get; }

        /// <summary>
        /// Returns true if this type is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this type is a primitive (int, float, etc), false otherwise.</returns>
        public virtual bool IsPrimitive => ElementType.IsPrimitive();

        /// <summary>
        /// Returns true if this type is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this type is a ValueClass (struct), false otherwise.</returns>
        public virtual bool IsValueClass => ElementType.IsValueClass();

        /// <summary>
        /// Returns true if this type is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this type is an object reference, false otherwise.</returns>
        public virtual bool IsObjectReference => ElementType.IsObjectReference();

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
        /// Returns whether objects of this type are finalizable.
        /// </summary>
        public abstract bool IsFinalizable { get; }

        // Visibility:
        /// <summary>
        /// Returns true if this type is marked Public.
        /// </summary>
        public abstract bool IsPublic { get; }

        /// <summary>
        /// returns true if this type is marked Private.
        /// </summary>
        public abstract bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this type is accessible only by items in its own assembly.
        /// </summary>
        public abstract bool IsInternal { get; }

        /// <summary>
        /// Returns true if this nested type is accessible only by subtypes of its outer type.
        /// </summary>
        public abstract bool IsProtected { get; }

        // Other attributes:

        /// <summary>
        /// Returns true if this class is abstract.
        /// </summary>
        public abstract bool IsAbstract { get; }

        /// <summary>
        /// Returns true if this class is sealed.
        /// </summary>
        public abstract bool IsSealed { get; }

        /// <summary>
        /// Returns true if this type is an interface.
        /// </summary>
        public abstract bool IsInterface { get; }

        /// <summary>
        /// Returns all possible fields in this type.   It does not return dynamically typed fields.
        /// Returns an empty list if there are no fields.
        /// </summary>
        public abstract IReadOnlyList<ClrInstanceField> Fields { get; }

        /// <summary>
        /// Returns a list of static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        public abstract IReadOnlyList<ClrStaticField> StaticFields { get; }

        /// <summary>
        /// Gets the list of methods this type implements.
        /// </summary>
        public abstract IReadOnlyList<ClrMethod> Methods { get; }

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrInstanceField? GetFieldByName(string name);

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrStaticField? GetStaticFieldByName(string name);

        /// <summary>
        /// If this type inherits from another type, this is that type.  Can return null if it does not inherit (or is unknown).
        /// </summary>
        public abstract ClrType? BaseType { get; }

        /// <summary>
        /// Indicates if the type is in fact a pointer. If so, the pointer operators
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
        /// Returns the array element value at the given index.  Returns 'null' if the array element is of type
        /// VALUE_CLASS.
        /// </summary>
        public abstract object? GetArrayElementValue(ulong objRef, int index);

        /// <summary>
        /// Returns the static size of objects of this type when they are created on the CLR heap.
        /// </summary>
        public abstract int StaticSize { get; }

        /// <summary>
        /// Returns the size of elements of this object.
        /// </summary>
        public abstract int ComponentSize { get; }

        /// <summary>
        /// Returns true if this type is System.String.
        /// </summary>
        public virtual bool IsString => false;

        /// <summary>
        /// Returns true if this type represents free space on the heap.
        /// </summary>
        public virtual bool IsFree => false;

        /// <summary>
        /// Returns true if this type is an exception (that is, it derives from System.Exception).
        /// </summary>
        public virtual bool IsException => false;

        /// <summary>
        /// Returns true if this type is an enum.
        /// </summary>
        public abstract bool IsEnum { get; }

        /// <summary>
        /// Returns the ClrEnum representation of this type.
        /// </summary>
        /// <returns>The ClrEnum representation of this type.  Throws InvalidOperationException if IsEnum returns false.</returns>
        public abstract ClrEnum AsEnum();

        /// <summary>
        /// Returns true if this type is shared across multiple AppDomains.
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

        public override bool Equals(object? obj)
        {
            if (obj is ClrType type)
            {
                if (MethodTable != 0 && type.MethodTable != 0)
                    return MethodTable == type.MethodTable;

                if (type.IsPointer)
                {
                    if (type.ComponentType is null)
                        return base.Equals(obj);

                    return ComponentType == type.ComponentType;
                }

                if (IsPrimitive && type.IsPrimitive && ElementType != ClrElementType.Unknown)
                    return ElementType == type.ElementType;

                // Ok we aren't a primitive type, or a pointer, and our MethodTables are 0.  Last resort is to
                // check if we resolved from the same token out of the same module.
                if (Module != null && MetadataToken != 0)
                    return Module == type.Module && MetadataToken == type.MetadataToken;

                // Fall back to reference equality
                return base.Equals(obj);
            }

            return false;
        }

        public override int GetHashCode() => MethodTable.GetHashCode();

        public static bool operator ==(ClrType? left, ClrType? right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(ClrType? left, ClrType? right)
        {
            return !(left == right);
        }
    }
}
