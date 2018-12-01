// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
        /// Gets the value of the static field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the value.</param>
        /// <returns>The value of this static field.</returns>
        public virtual object GetValue(ClrAppDomain appDomain)
        {
            return GetValue(appDomain, true);
        }

        /// <summary>
        /// Gets the value of the static field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the value.</param>
        /// <param name="convertStrings">
        /// When true, the value of a string field will be
        /// returned as a System.String object; otherwise the address of the String object will be returned.
        /// </param>
        /// <returns>The value of this static field.</returns>
        public abstract object GetValue(ClrAppDomain appDomain, bool convertStrings);

        /// <summary>
        /// Returns the address of the static field's value in memory.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's address.</param>
        /// <returns>The address of the field's value.</returns>
        public abstract ulong GetAddress(ClrAppDomain appDomain);

        /// <summary>
        /// Returns true if the static field has a default value (and if we can obtain it).
        /// </summary>
        public virtual bool HasDefaultValue => false;

        /// <summary>
        /// The default value of the field.
        /// </summary>
        /// <returns>The default value of the field.</returns>
        public virtual object GetDefaultValue()
        {
            throw new NotImplementedException();
        }
    }
}