// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it respresents a name and a type
    /// </summary>
    public abstract class ClrInstanceField : ClrField
    {
        /// <summary>
        /// Reads the value of the field as an unmanaged struct or primitive type.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type.</typeparam>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public abstract T Read<T>(ulong objRef, bool interior) where T : unmanaged;

        /// <summary>
        /// Reads the value of an object field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public abstract ClrObject ReadObject(ulong objRef, bool interior);

        /// <summary>
        /// Reads a ValueType struct from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public abstract ClrValueType ReadStruct(ulong objRef, bool interior);

        /// <summary>
        /// Reads a string from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public abstract string? ReadString(ulong objRef, bool interior);

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <returns>The value of the field.</returns>
        public virtual ulong GetAddress(ulong objRef)
        {
            return GetAddress(objRef, false);
        }

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <param name="interior">
        /// Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.
        /// </param>
        /// <returns>The value of the field.</returns>
        public abstract ulong GetAddress(ulong objRef, bool interior);
    }
}