// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    // TODO: remove this class.  ClrInstanceField should be ClrField.  GetValue/GetAddress should be folded elsewhere.

    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it respresents a name and a type
    /// </summary>
    public abstract class ClrInstanceField : ClrField
    {
        public abstract T Read<T>(ulong objRef, bool interior) where T : unmanaged;
        public abstract ClrObject ReadObject(ulong objRef, bool interior);
        public abstract ClrValueType ReadStruct(ulong objRef, bool interior);
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