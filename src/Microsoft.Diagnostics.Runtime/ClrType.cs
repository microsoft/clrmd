// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        abstract internal GCDesc GCDesc { get; }

        /// <summary>
        /// Retrieves the first type handle in EnumerateMethodTables().  MethodTables
        /// are unique to an AppDomain/Type pair, so when there are multiple domains
        /// there will be multiple MethodTable for a class.
        /// </summary>
        abstract public ulong MethodTable { get; }

        /// <summary>
        /// Enumerates all MethodTable for this type in the process.  MethodTable
        /// are unique to an AppDomain/Type pair, so when there are multiple domains
        /// there may be multiple MethodTable.  Note that even if a type could be
        /// used in an AppDomain, that does not mean we actually have a MethodTable
        /// if the type hasn't been created yet.
        /// </summary>
        /// <returns>An enumeration of MethodTable in the process for this given
        /// type.</returns>
        abstract public IEnumerable<ulong> EnumerateMethodTables();

        /// <summary>
        /// Returns the metadata token of this type.
        /// </summary>
        abstract public uint MetadataToken { get; }

        /// <summary>
        /// Types have names.
        /// </summary>
        abstract public string Name { get; }
        /// <summary>
        /// GetSize returns the size in bytes for the total overhead of the object 'objRef'.   
        /// </summary>
        abstract public ulong GetSize(ulong objRef);
        /// <summary>
        /// EnumeationRefsOfObject will call 'action' once for each object reference inside 'objRef'.  
        /// 'action' is passed the address of the outgoing refernece as well as an integer that
        /// represents the field offset.  While often this is the physical offset of the outgoing
        /// refernece, abstractly is simply something that can be given to GetFieldForOffset to 
        /// return the field information for that object reference  
        /// </summary>
        abstract public void EnumerateRefsOfObject(ulong objRef, Action<ulong, int> action);

        /// <summary>
        /// Does the same as EnumerateRefsOfObject, but does additional bounds checking to ensure
        /// we don't loop forever with inconsistent data.
        /// </summary>
        abstract public void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action);

        /// <summary>
        /// Enumerates all objects that the given object references.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="carefully">Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)</param>
        virtual public IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, bool carefully = false)
        {
            Debug.Assert(Heap.GetObjectType(obj) == this);
            return Heap.EnumerateObjectReferences(obj, this, carefully);
        }

        /// <summary>
        /// Returns true if the type CAN contain references to other objects.  This is used in optimizations 
        /// and 'true' can always be returned safely.  
        /// </summary>
        virtual public bool ContainsPointers { get { return true; } }

        /// <summary>
        /// All types know the heap they belong to.  
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// Returns true if this object is a 'RuntimeType' (that is, the concrete System.RuntimeType class
        /// which is what you get when calling "typeof" in C#).
        /// </summary>
        virtual public bool IsRuntimeType { get { return false; } }

        /// <summary>
        /// Returns the concrete type (in the target process) that this RuntimeType represents.
        /// Note you may only call this function if IsRuntimeType returns true.
        /// </summary>
        /// <param name="obj">The RuntimeType object to get the concrete type for.</param>
        /// <returns>The underlying type that this RuntimeType actually represents.  May return null if the
        ///          underlying type has not been fully constructed by the runtime, or if the underlying type
        ///          is actually a typehandle (which unfortunately ClrMD cannot convert into a ClrType due to
        ///          limitations in the underlying APIs.  (So always null-check the return value of this
        ///          function.) </returns>
        virtual public ClrType GetRuntimeType(ulong obj) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns the module this type is defined in.
        /// </summary>
        virtual public ClrModule Module { get { return null; } }

        /// <summary>
        /// Returns a method based on its token.
        /// </summary>
        /// <param name="token">The token of the method to return.</param>
        /// <returns>A ClrMethod for the given token, null if no such methodDesc exists.</returns>
        internal virtual ClrMethod GetMethod(uint token) { return null; }

        /// <summary>
        /// Returns the ElementType of this Type.  Can return ELEMENT_TYPE_VOID on error.
        /// </summary>
        virtual public ClrElementType ElementType { get { return ClrElementType.Unknown; } internal set { throw new NotImplementedException(); } }

        /// <summary>
        /// Returns true if this type is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this type is a primitive (int, float, etc), false otherwise.</returns>
        virtual public bool IsPrimitive { get { return ClrRuntime.IsPrimitive(ElementType); } }

        /// <summary>
        /// Returns true if this type is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this type is a ValueClass (struct), false otherwise.</returns>
        virtual public bool IsValueClass { get { return ClrRuntime.IsValueClass(ElementType); } }

        /// <summary>
        /// Returns true if this type is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this type is an object reference, false otherwise.</returns>
        virtual public bool IsObjectReference { get { return ClrRuntime.IsObjectReference(ElementType); } }

        /// <summary>
        /// Returns the list of interfaces this type implements.
        /// </summary>
        abstract public IList<ClrInterface> Interfaces { get; }

        /// <summary>
        /// Returns true if the finalization is suppressed for an object.  (The user program called
        /// System.GC.SupressFinalize.  The behavior of this function is undefined if the object itself
        /// is not finalizable.
        /// </summary>
        virtual public bool IsFinalizeSuppressed(ulong obj) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns whether objects of this type are finalizable.
        /// </summary>
        abstract public bool IsFinalizable { get; }

        // Visibility:
        /// <summary>
        /// Returns true if this type is marked Public.
        /// </summary>
        abstract public bool IsPublic { get; }

        /// <summary>
        /// returns true if this type is marked Private.
        /// </summary>
        abstract public bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this type is accessable only by items in its own assembly.
        /// </summary>
        abstract public bool IsInternal { get; }

        /// <summary>
        /// Returns true if this nested type is accessable only by subtypes of its outer type.
        /// </summary>
        abstract public bool IsProtected { get; }

        // Other attributes:
        /// <summary>
        /// Returns true if this class is abstract.
        /// </summary>
        abstract public bool IsAbstract { get; }

        /// <summary>
        /// Returns true if this class is sealed.
        /// </summary>
        abstract public bool IsSealed { get; }

        /// <summary>
        /// Returns true if this type is an interface.
        /// </summary>
        abstract public bool IsInterface { get; }

        /// <summary>
        /// Returns all possible fields in this type.   It does not return dynamically typed fields.  
        /// Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrInstanceField> Fields { get { return null; } }

        /// <summary>
        /// Returns a list of static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrStaticField> StaticFields { get { return null; } }

        /// <summary>
        /// Returns a list of thread static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrThreadStaticField> ThreadStaticFields { get { return null; } }

        /// <summary>
        /// Gets the list of methods this type implements.
        /// </summary>
        virtual public IList<ClrMethod> Methods { get { return null; } }

        /// <summary>
        /// When you enumerate a object, the offset within the object is returned.  This offset might represent
        /// nested fields (obj.Field1.Field2).    GetFieldOffset returns the first of these field (Field1), 
        /// and 'remaining' offset with the type of Field1 (which must be a struct type).   Calling 
        /// GetFieldForOffset repeatedly until the childFieldOffset is 0 will retrieve the whole chain.  
        /// </summary>
        /// <returns>true if successful.  Will fail if it 'this' is an array type</returns>
        abstract public bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset);

        /// <summary>
        /// Returns the field given by 'name', case sensitive.  Returns NULL if no such field name exists (or on error).
        /// </summary>
        abstract public ClrInstanceField GetFieldByName(string name);

        /// <summary>
        /// Returns the field given by 'name', case sensitive.  Returns NULL if no such field name exists (or on error).
        /// </summary>
        abstract public ClrStaticField GetStaticFieldByName(string name);

        /// <summary>
        /// If this type inherits from another type, this is that type.  Can return null if it does not inherit (or is unknown)
        /// </summary>
        abstract public ClrType BaseType { get; }

        /// <summary>
        /// Returns true if the given object is a Com-Callable-Wrapper.  This is only supported in v4.5 and later.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if this is a CCW.</returns>
        virtual public bool IsCCW(ulong obj) { return false; }

        /// <summary>
        /// Returns the CCWData for the given object.  Note you may only call this function if IsCCW returns true.
        /// </summary>
        /// <returns>The CCWData associated with the object, undefined result of obj is not a CCW.</returns>
        virtual public CcwData GetCCWData(ulong obj)
        {
            return null;
        }

        /// <summary>
        /// Returns true if the given object is a Runtime-Callable-Wrapper.  This is only supported in v4.5 and later.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if this is an RCW.</returns>
        virtual public bool IsRCW(ulong obj) { return false; }

        /// <summary>
        /// Returns the RCWData for the given object.  Note you may only call this function if IsRCW returns true.
        /// </summary>
        /// <returns>The RCWData associated with the object, undefined result of obj is not a RCW.</returns>
        virtual public RcwData GetRCWData(ulong obj)
        {
            return null;
        }

        /// <summary>
        /// Indicates if the type is in fact a pointer. If so, the pointer operators
        /// may be used.
        /// </summary>
        virtual public bool IsPointer { get { return false; } }

        /// <summary>
        /// Gets the type of the element referenced by the pointer.
        /// </summary>
        virtual public ClrType ComponentType { get; internal set; }

        /// <summary>
        /// A type is an array if you can use the array operators below, Abstractly arrays are objects 
        /// that whose children are not statically known by just knowing the type.  
        /// </summary>
        virtual public bool IsArray { get { return false; } }

        /// <summary>
        /// If the type is an array, then GetArrayLength returns the number of elements in the array.  Undefined
        /// behavior if this type is not an array.
        /// </summary>
        abstract public int GetArrayLength(ulong objRef);

        /// <summary>
        /// Returns the absolute address to the given array element.  You may then make a direct memory read out
        /// of the process to get the value if you want.
        /// </summary>
        abstract public ulong GetArrayElementAddress(ulong objRef, int index);

        /// <summary>
        /// Returns the array element value at the given index.  Returns 'null' if the array element is of type
        /// VALUE_CLASS.
        /// </summary>
        abstract public object GetArrayElementValue(ulong objRef, int index);

        /// <summary>
        /// Returns the size of individual elements of an array.
        /// </summary>
        abstract public int ElementSize { get; }

        /// <summary>
        /// Returns the base size of the object.
        /// </summary>
        abstract public int BaseSize { get; }

        /// <summary>
        /// Returns true if this type is System.String.
        /// </summary>
        virtual public bool IsString { get { return false; } }

        /// <summary>
        /// Returns true if this type represents free space on the heap.
        /// </summary>
        virtual public bool IsFree { get { return false; } }

        /// <summary>
        /// Returns true if this type is an exception (that is, it derives from System.Exception).
        /// </summary>
        virtual public bool IsException { get { return false; } }

        /// <summary>
        /// Returns true if this type is an enum.
        /// </summary>
        virtual public bool IsEnum { get { return false; } }

        /// <summary>
        /// Returns the element type of this enum.
        /// </summary>
        virtual public ClrElementType GetEnumElementType() { throw new NotImplementedException(); }

        /// <summary>
        /// Returns a list of names in the enum.
        /// </summary>
        virtual public IEnumerable<string> GetEnumNames() { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        virtual public string GetEnumName(object value) { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        virtual public string GetEnumName(int value) { throw new NotImplementedException(); }

        /// <summary>
        /// Attempts to get the integer value for a given enum entry.  Note you should only call this function if
        /// GetEnumElementType returns ELEMENT_TYPE_I4.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if 'name' is not a part of the enumeration.</returns>
        virtual public bool TryGetEnumValue(string name, out int value) { throw new NotImplementedException(); }

        /// <summary>
        /// Attempts to get the value for a given enum entry.  The type of "value" can be determined by the
        /// return value of GetEnumElementType.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if 'name' is not a part of the enumeration.</returns>
        virtual public bool TryGetEnumValue(string name, out object value) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns true if instances of this type have a simple value.
        /// </summary>
        virtual public bool HasSimpleValue { get { return false; } }

        /// <summary>
        /// Returns the simple value of an instance of this type.  Undefined behavior if HasSimpleValue returns false.
        /// For example ELEMENT_TYPE_I4 is an "int" and the return value of this function would be an int.
        /// </summary>
        /// <param name="address">The address of an instance of this type.</param>
        virtual public object GetValue(ulong address) { return null; }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
