// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an object in the target process.
    /// </summary>
    public readonly struct ClrObject : IAddressableTypedEntity, IEquatable<ClrObject>
    {
        private const string RuntimeType = "System.RuntimeType";

        private IClrObjectHelpers Helpers => GetTypeOrThrow().ClrObjectHelpers;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The address of the object.</param>
        /// <param name="type">The concrete type of the object.</param>
        public ClrObject(ulong address, ClrType? type)
        {
            Address = address;
            Type = type;

            DebugOnly.Assert(address == 0 || type != null);
            DebugOnly.Assert(address == 0 || (type != null && type.Heap.GetObjectType(address) == type));
        }

        /// <summary>
        /// Enumerates all objects that this object references.
        /// </summary>
        /// <param name="carefully">Only returns pointers which lie on the managed heap.  In very rare cases it's possible to
        /// create a crash dump where the GC was in the middle of updating data structures, or to create a crash dump of a process
        /// with heap corruption.  In those cases, setting carefully=true would ensure we would not enumerate those bad references.
        /// Note that setting carefully=true will cause a small performance penalty.</param>
        /// <param name="considerDependantHandles">Setting this to true will have ClrMD check for dependent handle references.
        /// Checking dependent handles does come at a performance penalty but will give you the true reference chain as the
        /// GC sees it.</param>
        /// <returns>An enumeration of object references.</returns>
        public IEnumerable<ClrObject> EnumerateReferences(bool carefully = false, bool considerDependantHandles = true)
        {
            if (Type is null)
                return Enumerable.Empty<ClrObject>();

            return Type.Heap.EnumerateObjectReferences(Address, Type, carefully, considerDependantHandles);
        }

        /// <summary>
        /// Enumerates all objects that this object references.  This method also enumerates the field (or handle) that this
        /// reference comes from.
        /// </summary>
        /// <param name="carefully">Only returns pointers which lie on the managed heap.  In very rare cases it's possible to
        /// create a crash dump where the GC was in the middle of updating data structures, or to create a crash dump of a process
        /// with heap corruption.  In those cases, setting carefully=true would ensure we would not enumerate those bad references.
        /// Note that setting carefully=true will cause a small performance penalty.</param>
        /// <param name="considerDependantHandles">Setting this to true will have ClrMD check for dependent handle references.
        /// Checking dependent handles does come at a performance penalty but will give you the true reference chain as the
        /// GC sees it.</param>
        /// <returns>An enumeration of object references.</returns>
        public IEnumerable<ClrReference> EnumerateReferencesWithFields(bool carefully = false, bool considerDependantHandles = true)
        {
            if (Type is null)
                return Enumerable.Empty<ClrReference>();

            return Type.Heap.EnumerateReferencesWithFields(Address, Type, carefully, considerDependantHandles);
        }

        /// <summary>
        /// Returns true if this object is a boxed struct or primitive type that 
        /// </summary>
        public bool IsBoxedValue => Type != null && (Type.IsPrimitive || Type.IsValueType);

        /// <summary>
        /// Reads a boxed primitive value.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type to read out of the object.</typeparam>
        /// <returns>The value read.</returns>
        public T ReadBoxedValue<T>() where T : unmanaged
        {
            IClrObjectHelpers? helpers = Helpers;
            if (helpers is null)
                return default;

            return helpers.DataReader.Read<T>(Address + (ulong)IntPtr.Size);
        }

        public bool IsException => Type != null && Type.IsException;

        public ClrException AsException()
        {
            if (!IsException)
                throw new InvalidOperationException($"Object {Address:x} is not an Exception.");

            if (Type is null || !Type.IsException)
                return default;

            return new ClrException(Helpers.ExceptionHelpers, null, this);
        }

        /// <summary>
        /// Gets the address of the object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Gets the type of the object.
        /// </summary>
        public ClrType? Type { get; }

        public bool IsValidObject => Address != 0 && Type != null;

        /// <summary>
        /// Returns if the object value is <see langword="null"/>.
        /// </summary>
        public bool IsNull => Address == 0;

        /// <summary>
        /// Gets the size of the object.
        /// </summary>
        public ulong Size => GetTypeOrThrow().Heap.GetObjectSize(Address, GetTypeOrThrow());

        /// <summary>
        /// Returns the ComCallableWrapper for the given object.
        /// </summary>
        /// <returns>The ComCallableWrapper associated with the object, <see langword="null"/> if obj is not a CCW.</returns>
        public ComCallableWrapper? AsComCallableWrapper()
        {
            if (IsNull || !IsValidObject)
                return null;

            return Helpers.Factory.CreateCCWForObject(Address);
        }

        /// <summary>
        /// Returns the RuntimeCallableWrapper for the given object.
        /// </summary>
        /// <returns>The RuntimeCallableWrapper associated with the object, <see langword="null"/> if obj is not a RCW.</returns>
        public RuntimeCallableWrapper? AsRuntimeCallableWrapper()
        {
            if (IsNull || !IsValidObject)
                return null;

            return Helpers.Factory.CreateRCWForObject(Address);
        }

        /// <summary>
        /// Gets a value indicating whether this object possibly contians GC pointers.
        /// </summary>
        public bool ContainsPointers => Type != null && Type.ContainsPointers;

        /// <summary>
        /// Gets a value indicating whether this object is an array.
        /// </summary>
        public bool IsArray => GetTypeOrThrow().IsArray;

        /// <summary>
        /// returns the object as an array if the object has array type.
        /// </summary>
        /// <returns></returns>
        public ClrArray AsArray()
        {
            ClrType type = GetTypeOrThrow();
            if (!type.IsArray)
            {
                throw new InvalidOperationException($"Object {Address:x} is not an array, type is '{type.Name}'.");
            }

            return new ClrArray(Address, type);
        }

        /// <summary>
        /// Converts a ClrObject into its string value.
        /// </summary>
        /// <param name="obj">A string object.</param>
        public static explicit operator string?(ClrObject obj) => obj.AsString();

        /// <summary>
        /// Returns <see cref="Address"/> sweetening obj to pointer move.
        /// <Para>Example: ulong address = clrObject</Para>
        /// </summary>
        /// <param name="clrObject">An object to get address of.</param>
        public static implicit operator ulong(ClrObject clrObject) => clrObject.Address;

        /// <summary>
        /// Gets the given object reference field from this ClrObject.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        /// <exception cref="ArgumentException">The given field does not exist in the object.</exception>
        /// <exception cref="InvalidOperationException"><see cref="IsNull"/> is <see langword="true"/>.</exception>
        public ClrObject ReadObjectField(string fieldName)
        {
            ClrType type = GetTypeOrThrow();
            ClrInstanceField? field = type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = type.Heap;

            ulong addr = field.GetAddress(Address);
            if (!type.ClrObjectHelpers.DataReader.ReadPointer(addr, out ulong obj))
                return default;

            return heap.GetObject(obj);
        }

        /// <summary>
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public ClrValueType ReadValueTypeField(string fieldName)
        {
            ClrType type = GetTypeOrThrow();

            ClrInstanceField? field = type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsValueType)
                throw new ArgumentException($"Field '{type.Name}.{fieldName}' is not a ValueClass.");

            if (field.Type is null)
                throw new InvalidOperationException("Field does not have an associated class.");

            ulong addr = field.GetAddress(Address);
            return new ClrValueType(addr, field.Type, true);
        }

        /// <summary>
        /// Gets the value of a primitive field.  This will throw an InvalidCastException if the type parameter
        /// does not match the field's type.
        /// </summary>
        /// <typeparam name="T">The type of the field itself.</typeparam>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of this field.</returns>
        public T ReadField<T>(string fieldName)
            where T : unmanaged
        {
            ClrType type = GetTypeOrThrow();
            ClrInstanceField? field = type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            object value = field.Read<T>(Address, interior: false);
            return (T)value;
        }

        public bool IsRuntimeType => Type?.Name == RuntimeType;
        public ClrType? AsRuntimeType()
        {
            ClrType type = GetTypeOrThrow();

            if (!IsRuntimeType)
                throw new InvalidOperationException($"Object {Address:x} is of type '{Type?.Name ?? "null" }', expected '{RuntimeType}'.");

            ClrInstanceField? field = type.Fields.Where(f => f.Name == "m_handle").FirstOrDefault();
            if (field is null)
                return null;

            ulong mt;
            if (field.ElementType == ClrElementType.NativeInt)
                mt = (ulong)ReadField<IntPtr>("m_handle");
            else
                mt = (ulong)ReadValueTypeField("m_handle").ReadField<IntPtr>("m_ptr");

            return type.ClrObjectHelpers.Factory.GetOrCreateType(mt, 0);
        }

        /// <summary>
        /// Gets a string field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <param name="maxLength">The maximum length of the string returned.  Warning: If the DataTarget
        /// being inspected has corrupted or an inconsistent heap state, the length of a string may be
        /// incorrect, leading to OutOfMemory and other failures.</param>
        /// <returns>The value of the given field.</returns>
        /// <exception cref="ArgumentException">No field matches the given name.</exception>
        /// <exception cref="InvalidOperationException">
        /// The target object is <see langword="null"/> (that is, <see cref="IsNull"/> is <see langword="true"/>).
        /// -or-
        /// The field is not of the correct type.
        /// </exception>
        public string? ReadStringField(string fieldName, int maxLength = 4096)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string");
            IDataReader dataReader = Helpers.DataReader;
            if (!dataReader.ReadPointer(address, out ulong strPtr))
                return null;

            if (strPtr == 0)
                return null;

            return Helpers.ReadString(strPtr, maxLength);
        }

        public string? AsString(int maxLength = 4096)
        {
            ClrType type = GetTypeOrThrow();
            if (!type.IsString)
                throw new InvalidOperationException($"Object {Address:x} is not a string, actual type: {Type?.Name ?? "null"}.");

            return Helpers.ReadString(Address, maxLength);
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName)
        {
            ClrType type = GetTypeOrThrow();

            if (IsNull)
                throw new InvalidOperationException($"Cannot get field from null object.");

            ClrInstanceField? field = type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(Address);
            return address;
        }

        private ClrType GetTypeOrThrow()
        {
            if (IsNull)
                throw new InvalidOperationException("Object is null.");

            if (!IsValidObject)
                throw new InvalidOperationException($"Object {Address:x} is corrupted, could not determine type.");

            return Type!;
        }

        /// <summary>
        /// Determines if this instance and another specific <see cref="ClrObject" /> have the same value.
        /// <para>Instances are considered equal when they have same <see cref="Address" />.</para>
        /// </summary>
        /// <param name="other">The <see cref="ClrObject" /> to compare to this instance.</param>
        /// <returns><see langword="true"/> if the <see cref="Address" /> of the parameter is same as <see cref="Address" /> in this instance; <see langword="false"/> otherwise.</returns>
        public bool Equals(ClrObject other) => Address == other.Address;

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="ClrObject" />, have the same value.
        /// </summary>
        /// <param name="obj">The <see cref="ClrObject" /> to compare to this instance.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj" /> is <see cref="ClrObject" />, and its <see cref="Address" /> is same as <see cref="Address" /> in this instance; <see langword="false"/>
        /// otherwise.
        /// </returns>
        public override bool Equals(object? obj) => obj is ClrObject other && Equals(other);

        /// <summary>
        /// Returns the hash code for this <see cref="ClrObject" /> based on its <see cref="Address" />.
        /// </summary>
        /// <returns>An <see cref="int" /> hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public bool Equals(IAddressableTypedEntity? entity) => entity is ClrObject other && Equals(other);

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject" /> have the same value.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject" /> to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="left" /> <see cref="Equals(ClrObject)" /> <paramref name="right" />; <see langword="false"/> otherwise.</returns>
        public static bool operator ==(ClrObject left, ClrObject right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject" /> have different values.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject" /> to compare.</param>
        /// <returns><see langword="true"/> if the value of <paramref name="left" /> is different from the value of <paramref name="right" />; <see langword="false"/> otherwise.</returns>
        public static bool operator !=(ClrObject left, ClrObject right) => !(left == right);

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Type?.Name} {Address:x}";
        }
    }
}
