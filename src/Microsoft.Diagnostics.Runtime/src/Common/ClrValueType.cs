// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance of a type which inherits from <see cref="ValueType"/>.
    /// </summary>
    public readonly struct ClrValueType : IAddressableTypedEntity
    {
        private IDataReader DataReader => GetTypeOrThrow().ClrObjectHelpers.DataReader;
        private readonly bool _interior;

        /// <summary>
        /// Gets the address of the object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Gets the type of the object.
        /// </summary>
        public ClrType Type { get; }

        internal ClrValueType(ulong address, ClrType type, bool interior)
        {
            Address = address;
            Type = type;
            _interior = interior;

            DebugOnly.Assert(type.IsValueType);
        }

        /// <summary>
        /// Gets the given object reference field from this ClrObject.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        /// <exception cref="ArgumentException">
        /// The given field does not exist in the object.
        /// -or-
        /// The given field was not an object reference.
        /// </exception>
        public ClrObject ReadObjectField(string fieldName)
        {
            ClrInstanceField? field = Type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{Type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{Type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = Type.Heap;

            ulong addr = field.GetAddress(Address, _interior);
            if (!DataReader.ReadPointer(addr, out ulong obj))
                return default;

            return heap.GetObject(obj);
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
            ClrInstanceField? field = Type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{Type.Name}' does not contain a field named '{fieldName}'");

            object value = field.Read<T>(Address, _interior);
            return (T)value;
        }

        /// <summary>
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public ClrValueType ReadValueTypeField(string fieldName)
        {
            ClrInstanceField? field = Type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{Type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsValueType)
                throw new ArgumentException($"Field '{Type.Name}.{fieldName}' is not a ValueClass.");

            if (field.Type is null)
                throw new InvalidOperationException("Field does not have an associated class.");

            ulong addr = field.GetAddress(Address, _interior);
            return new ClrValueType(addr, field.Type, true);
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
        /// <exception cref="InvalidOperationException">The field is not a string.</exception>
        public string? ReadStringField(string fieldName, int maxLength = 4096)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string");
            if (!DataReader.ReadPointer(address, out ulong str))
                return null;

            if (str == 0)
                return null;

            ClrObject obj = new ClrObject(str, Type.Heap.StringType);
            return obj.AsString(maxLength);
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName)
        {
            ClrInstanceField? field = Type.GetInstanceFieldByName(fieldName);
            if (field is null)
                throw new ArgumentException($"Type '{Type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{Type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(Address, _interior);
            return address;
        }

        public bool Equals(IAddressableTypedEntity? other)
            => other != null && Address == other.Address && Type == other.Type;

        private ClrType GetTypeOrThrow()
        {
            if (Type is null)
                throw new InvalidOperationException($"Unknown type of value at {Address:x}.");

            return Type;
        }
    }
}