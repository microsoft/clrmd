// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a static field in the target process.
    /// </summary>
    public abstract class ClrStaticField : ClrField
    {
        /// <summary>
        /// Returns whether this static field has been initialized in a particular AppDomain
        /// or not.  If a static variable has not been initialized, then its class constructor
        /// may have not been run yet.  Calling any of the Read* methods on an uninitialized static
        /// will result in returning either NULL or a value of 0.
        /// </summary>
        /// <param name="appDomain">The AppDomain to see if the variable has been initialized.</param>
        /// <returns>
        /// True if the field has been initialized (even if initialized to NULL or a default
        /// value), false if the runtime has not initialized this variable.
        /// </returns>
        public abstract bool IsInitialized(ClrAppDomain appDomain);

        /// <summary>
        /// Gets the address of the static field's value in memory.
        /// </summary>
        /// <returns>The address of the field's value.</returns>
        public abstract ulong GetAddress(ClrAppDomain appDomain);

        /// <summary>
        /// Reads the value of the field as an unmanaged struct or primitive type.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type.</typeparam>
        /// <returns>The value read.</returns>
        public abstract T Read<T>(ClrAppDomain appDomain) where T : unmanaged;

        /// <summary>
        /// Reads the value of an object field.
        /// </summary>
        /// <returns>The value read.</returns>
        public abstract ClrObject ReadObject(ClrAppDomain appDomain);

        /// <summary>
        /// Reads a ValueType struct from the instance field.
        /// </summary>
        /// <returns>The value read.</returns>
        public abstract ClrValueType ReadStruct(ClrAppDomain appDomain);

        /// <summary>
        /// Reads a string from the instance field.
        /// </summary>
        /// <returns>The value read.</returns>
        public abstract string? ReadString(ClrAppDomain appDomain);
    }
}