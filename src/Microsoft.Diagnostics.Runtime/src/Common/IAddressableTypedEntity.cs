// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an addressable entity (class or struct) with associated type.
    /// <para>Allows locating field values by known names.</para>
    /// </summary>
    public interface IAddressableTypedEntity: IEquatable<IAddressableTypedEntity>
    {
        /// <summary>
        /// Gets the address of this entity.
        /// </summary>
        ulong Address { get; }

        /// <summary>
        /// Gets the type associated with this entity.
        /// </summary>
        ClrType Type { get; }

        /// <summary>
        /// Gets the value of a primitive field (f.e. <see cref="int"/>, <see cref="bool"/>).
        /// </summary>
        /// <typeparam name="T">The primitive type of the field.</typeparam>
        /// <param name="fieldName">The name of the field to read value from.</param>
        /// <returns>The value of the field.</returns>
        /// <exception cref="ArgumentException">Thrown when field was not found by name.</exception>
        T GetField<T>(string fieldName) where T : struct;

        /// <summary>
        /// Gets the <see cref="string"/> value from the entity field.
        /// <para>Note that the type must match exactly, as this method
        /// will not do type coercion.</para>
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        /// <exception cref="ArgumentException">Thrown when field was not found by name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when found field has other type than <see cref="string"/>.</exception>
        /// <exception cref="MemoryReadException">Thrown when object reference could not be followed, or <see cref="string"/> could not be read.</exception>
        string GetStringField(string fieldName);

        /// <summary>
        /// Gets the struct field value from the entity field.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        /// <exception cref="ArgumentException">Thrown when field was not found by name, or found field is not of struct type.</exception>
        ClrValueClass GetValueClassField(string fieldName);

        /// <summary>
        /// Gets the value of reference field.
        /// </summary>
        /// <param name="fieldName">The name of the field to read value from.</param>
        /// <returns>A <see cref="ClrObject"/> found field points on.</returns>
        /// <exception cref="ArgumentException">Thrown when field was not found by name, or found field is not of reference type.</exception>
        /// <exception cref="MemoryReadException">Thrown when object reference could not be followed.</exception>
        ClrObject GetObjectField(string fieldName);
    }
}
