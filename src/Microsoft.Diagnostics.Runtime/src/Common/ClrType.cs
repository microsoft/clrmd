// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        /// The type handle of this type (e.g. MethodTable).
        /// </summary>
        public abstract ulong TypeHandle { get; }
        
        /// <summary>
        /// Returns the metadata token of this type.
        /// </summary>
        public abstract uint MetadataToken { get; }

        /// <summary>
        /// Types have names.
        /// </summary>
        public abstract string Name { get; }

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
        public abstract ClrModule Module { get; }

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
        /// When you enumerate a object, the offset within the object is returned.  This offset might represent
        /// nested fields (obj.Field1.Field2).    GetFieldOffset returns the first of these field (Field1),
        /// and 'remaining' offset with the type of Field1 (which must be a struct type).   Calling
        /// GetFieldForOffset repeatedly until the childFieldOffset is 0 will retrieve the whole chain.
        /// </summary>
        /// <returns>true if successful.  Will fail if it 'this' is an array type</returns>
        public abstract bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset);

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrInstanceField GetFieldByName(string name);

        /// <summary>
        /// Returns the field given by <paramref name="name"/>, case sensitive. Returns <see langword="null" /> if no such field name exists (or on error).
        /// </summary>
        public abstract ClrStaticField GetStaticFieldByName(string name);

        /// <summary>
        /// If this type inherits from another type, this is that type.  Can return null if it does not inherit (or is unknown)
        /// </summary>
        public abstract ClrType BaseType { get; }

        /// <summary>
        /// Returns the CCWData for the given object.
        /// </summary>
        /// <returns>The CCWData associated with the object, null if obj is not a CCW.</returns>
        public abstract ComCallWrapper GetCCWData(ulong obj);

        /// <summary>
        /// Returns the RCWData for the given object.
        /// </summary>
        /// <returns>The RCWData associated with the object, null if obj is not a RCW.</returns>
        public abstract RuntimeCallableWrapper GetRCWData(ulong obj);

        /// <summary>
        /// Indicates if the type is in fact a pointer. If so, the pointer operators
        /// may be used.
        /// </summary>
        public virtual bool IsPointer => false;

        /// <summary>
        /// Gets the type of the element referenced by the pointer.
        /// </summary>
        public abstract ClrType ComponentType { get; }

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
        public abstract object GetArrayElementValue(ulong objRef, int index);

        /// <summary>
        /// Returns the base size of the object.
        /// </summary>
        public abstract int BaseSize { get; }

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
        public virtual bool IsEnum => false;

        /// <summary>
        /// Returns the element type of this enum.
        /// </summary>
        public virtual ClrElementType GetEnumElementType()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a list of names in the enum.
        /// </summary>
        public virtual IEnumerable<string> GetEnumNames()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        public virtual string GetEnumName(object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        public virtual string GetEnumName(int value)
        {
            throw new NotImplementedException();
        }

        public abstract bool IsShared { get; }

        /// <summary>
        /// Attempts to get the integer value for a given enum entry.  Note you should only call this function if
        /// GetEnumElementType returns ELEMENT_TYPE_I4.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if <paramref name="name"/> is not a part of the enumeration.</returns>
        public virtual bool TryGetEnumValue(string name, out int value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attempts to get the value for a given enum entry.  The type of "value" can be determined by the
        /// return value of GetEnumElementType.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if <paramref name="name"/> is not a part of the enumeration.</returns>
        public virtual bool TryGetEnumValue(string name, out object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Used to provide functionality to ClrObject.
        /// </summary>
        public abstract IClrObjectHelpers ClrObjectHelpers { get; }
    }
}
