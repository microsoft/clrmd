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
        /// may have not been run yet.  Calling GetFieldValue on an uninitialized static
        /// will result in returning either NULL or a value of 0.
        /// </summary>
        /// <param name="appDomain">The AppDomain to see if the variable has been initialized.</param>
        /// <returns>
        /// True if the field has been initialized (even if initialized to NULL or a default
        /// value), false if the runtime has not initialized this variable.
        /// </returns>
        public abstract bool IsInitialized(ClrAppDomain appDomain);

        /// <summary>
        /// Returns the address of the static field's value in memory.
        /// </summary>
        /// <returns>The address of the field's value.</returns>
        public abstract ulong Address { get; }

        public abstract T Read<T>() where T : unmanaged;
        public abstract ClrObject ReadObject();
        public abstract ClrValueClass ReadStruct();
        public abstract string ReadString();
    }
}