// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an object in the target process.
    /// </summary>
    public struct ClrObject : IAddressableTypedEntity, IEquatable<ClrObject>
    {
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
        /// <returns>An enumeration of object references.</returns>
        public IEnumerable<ClrObject> EnumerateReferences(bool carefully = false, bool considerDependantHandles = true)
        {
            if (Type is null)
                return Array.Empty<ClrObject>();

            return Type.Heap.EnumerateObjectReferences(Address, Type, carefully, considerDependantHandles);
        }

        public T ReadBoxed<T>() where T : unmanaged
        {
            IClrObjectHelpers? helpers = Helpers;
            if (helpers is null)
                return default;

            return helpers.DataReader.ReadUnsafe<T>(Address + (ulong)IntPtr.Size);
        }

        public bool IsException => Type != null && Type.IsException;

        public ClrException AsException()
        {
            if (!IsException)
                throw new InvalidOperationException($"Object {Address:x} is not an Exception.");

            if (Type is null || !Type.IsException)
                return default;

            return new ClrException(Helpers!.ExceptionHelpers, null, this);
        }

        /// <summary>
        /// The address of the object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The type of the object.
        /// </summary>
        public ClrType? Type { get; }

        public bool IsValidObject => Address != 0 && Type != null;

        /// <summary>
        /// Returns if the object value is null.
        /// </summary>
        public bool IsNull => Address == 0;

        /// <summary>
        /// Gets the size of the object.
        /// </summary>
        public ulong Size => GetTypeOrThrow().Heap.GetObjectSize(Address, GetTypeOrThrow());

        /// <summary>
        /// Returns whether this object is an array or not.
        /// </summary>
        public bool IsArray => GetTypeOrThrow().IsArray;

        /// <summary>
        /// Returns the count of elements in this array, or throws InvalidOperatonException if this object is not an array.
        /// </summary>
        public int Length
        {
            get
            {
                ClrType type = GetTypeOrThrow();
                if (!type.IsArray)
                    throw new InvalidOperationException($"Object {Address:x} is not an array, type is '{Type!.Name}'.");

                return type.ClrObjectHelpers.DataReader.ReadUnsafe<int>(Address + (uint)IntPtr.Size);
            }
        }
        /// <summary>
        /// Returns the ComCallableWrapper for the given object.
        /// </summary>
        /// <returns>The ComCallableWrapper associated with the object, null if obj is not a CCW.</returns>
        public ComCallableWrapper? AsComCallableWrapper()
        {
            if (IsNull || !IsValidObject)
                return null;

            return Helpers.Factory.CreateCCWForObject(Address);
        }

        /// <summary>
        /// Returns the RuntimeCallableWrapper for the given object.
        /// </summary>
        /// <returns>The RuntimeCallableWrapper associated with the object, null if obj is not a RCW.</returns>
        public RuntimeCallableWrapper? AsRuntimeCallableWrapper()
        {
            if (IsNull || !IsValidObject)
                return null;

            return Helpers.Factory.CreateRCWForObject(Address);
        }

        /// <summary>
        /// Returns true if this object possibly contians GC pointers.
        /// </summary>
        public bool ContainsPointers => Type != null && Type.ContainsPointers;

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Type?.Name} {Address:x}";
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
        /// Gets the given object reference field from this ClrObject.  Throws ArgumentException if the given field does
        /// not exist in the object.  Throws NullReferenceException if IsNull is true.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        public ClrObject GetObjectField(string fieldName)
        {
            ClrType type = GetTypeOrThrow();
            ClrInstanceField? field = type.GetFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = type.Heap;

            ulong addr = field.GetAddress(Address);
            if (!type.ClrObjectHelpers.DataReader.ReadPointer(addr, out ulong obj))
                throw new MemoryReadException(addr);

            return heap.GetObject(obj);
        }

        /// <summary>
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public ClrValueClass GetValueClassField(string fieldName)
        {
            ClrType type = GetTypeOrThrow();

            ClrInstanceField? field = type.GetFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsValueClass)
                throw new ArgumentException($"Field '{type.Name}.{fieldName}' is not a ValueClass.");

            if (field.Type is null)
                throw new Exception("Field does not have an associated class.");

            ulong addr = field.GetAddress(Address);
            return new ClrValueClass(addr, field.Type, true);
        }

        /// <summary>
        /// Gets the value of a primitive field.  This will throw an InvalidCastException if the type parameter
        /// does not match the field's type.
        /// </summary>
        /// <typeparam name="T">The type of the field itself.</typeparam>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of this field.</returns>
        public T GetField<T>(string fieldName)
            where T : unmanaged
        {
            ClrType type = GetTypeOrThrow();
            ClrInstanceField? field = type.GetFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            object value = field.Read<T>(Address, interior: false);
            return (T)value;
        }

        public bool IsRuntimeType => Type?.Name == "System.RuntimeType";
        public ClrType? AsRuntimeType()
        {
            ClrType type = GetTypeOrThrow();

            if (!IsRuntimeType)
                throw new InvalidOperationException();

            ClrInstanceField? field = type.Fields.Where(f => f.Name == "m_handle").FirstOrDefault();
            if (field is null)
                return null;

            ulong mt;
            if (field.ElementType == ClrElementType.NativeInt)
                mt = (ulong)GetField<IntPtr>("m_handle");
            else
                mt = (ulong)GetValueClassField("m_handle").GetField<IntPtr>("m_ptr");

            return type.ClrObjectHelpers.Factory.GetOrCreateType(mt, 0);
        }

        /// <summary>
        /// Gets a string field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public string? GetStringField(string fieldName, int maxLength = 4096)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, out ClrType stringType, "string");
            IDataReader dataReader = Helpers.DataReader;
            if (!dataReader.ReadPointer(address, out ulong strPtr))
                throw new MemoryReadException(address);

            if (strPtr == 0)
                return null;

            return ValueReader.GetStringContents(stringType, dataReader, strPtr, maxLength);
        }

        public string? AsString(int maxLength = 4096)
        {
            ClrType type = GetTypeOrThrow();
            if (!type.IsString)
                throw new InvalidOperationException($"Object {Address:x} is not a string, actual type: {Type?.Name ?? "null"}.");


            return ValueReader.GetStringContents(type, Helpers.DataReader, Address, maxLength);
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, out ClrType fieldType, string typeName)
        {
            ClrType type = GetTypeOrThrow();

            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField? field = type.GetFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(Address);
            fieldType = field.Type;
            return address;
        }

        /// <summary>
        /// Determines if this instance and another specific <see cref="ClrObject" /> have the same value.
        /// <para>Instances are considered equal when they have same <see cref="Address" />.</para>
        /// </summary>
        /// <param name="other">The <see cref="ClrObject" /> to compare to this instance.</param>
        /// <returns><c>true</c> if the <see cref="Address" /> of the parameter is same as <see cref="Address" /> in this instance; <c>false</c> otherwise.</returns>
        public bool Equals(ClrObject other)
        {
            return Address == other.Address;
        }

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="ClrObject" />, have the same value.
        /// </summary>
        /// <param name="other">The <see cref="ClrObject" /> to compare to this instance.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="other" /> is <see cref="ClrObject" />, and its <see cref="Address" /> is same as <see cref="Address" /> in this instance; <c>false</c>
        /// otherwise.
        /// </returns>
        public override bool Equals(object? obj)
        {
            return obj is ClrObject other && Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this <see cref="ClrObject" /> based on its <see cref="Address" />.
        /// </summary>
        /// <returns>An <see cref="int" /> hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public bool Equals(IAddressableTypedEntity other)
            => other is ClrObject && Equals((ClrObject)other);

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject" /> have the same value.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject" /> to compare.</param>
        /// <returns><c>true</c> if <paramref name="left" /> <see cref="Equals(ClrObject)" /> <paramref name="right" />; <c>false</c> otherwise.</returns>
        public static bool operator ==(ClrObject? left, ClrObject? right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject" /> have different values.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject" /> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left" /> is different from the value of <paramref name="right" />; <c>false</c> otherwise.</returns>
        public static bool operator !=(ClrObject? left, ClrObject? right)
        {
            return !(left == right);
        }

        private ClrType GetTypeOrThrow()
        {
            if (IsNull)
                throw new NullReferenceException("Object is null.");

            if (!IsValidObject)
                throw new InvalidOperationException($"Object {Address:x} is corrupted, could not determine type.");

            return Type!;
        }
    }
}
