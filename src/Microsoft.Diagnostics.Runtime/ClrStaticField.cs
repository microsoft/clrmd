// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// <returns>True if the field has been initialized (even if initialized to NULL or a default
        /// value), false if the runtime has not initialized this variable.</returns>
        abstract public bool IsInitialized(ClrAppDomain appDomain);

        /// <summary>
        /// Gets the value of the static field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the value.</param>
        /// <returns>The value of this static field.</returns>
        virtual public object GetValue(ClrAppDomain appDomain)
        {
            return GetValue(appDomain, true);
        }

        /// <summary>
        /// Gets the value of the static field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the value.</param>
        /// <param name="convertStrings">When true, the value of a string field will be 
        /// returned as a System.String object; otherwise the address of the String object will be returned.</param>
        /// <returns>The value of this static field.</returns>
        abstract public object GetValue(ClrAppDomain appDomain, bool convertStrings);

        /// <summary>
        /// Returns the address of the static field's value in memory.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's address.</param>
        /// <returns>The address of the field's value.</returns>
        abstract public ulong GetAddress(ClrAppDomain appDomain);

        /// <summary>
        /// Returns true if the static field has a default value (and if we can obtain it).
        /// </summary>
        virtual public bool HasDefaultValue { get { return false; } }

        /// <summary>
        /// The default value of the field.
        /// </summary>
        /// <returns>The default value of the field.</returns>
        virtual public object GetDefaultValue() { throw new NotImplementedException(); }
    }

}
