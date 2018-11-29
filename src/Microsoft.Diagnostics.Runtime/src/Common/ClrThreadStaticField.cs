// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a thread static value in the target process.
    /// </summary>
    public abstract class ClrThreadStaticField : ClrField
    {
        /// <summary>
        /// Gets the value of the field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's value.</param>
        /// <param name="thread">The thread on which to get the field's value.</param>
        /// <returns>The value of the field.</returns>
        virtual public object GetValue(ClrAppDomain appDomain, ClrThread thread)
        {
            return GetValue(appDomain, thread, true);
        }

        /// <summary>
        /// Gets the value of the field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's value.</param>
        /// <param name="thread">The thread on which to get the field's value.</param>
        /// <param name="convertStrings">When true, the value of a string field will be 
        /// returned as a System.String object; otherwise the address of the String object will be returned.</param>
        /// <returns>The value of the field.</returns>
        abstract public object GetValue(ClrAppDomain appDomain, ClrThread thread, bool convertStrings);

        /// <summary>
        /// Gets the address of the field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's address.</param>
        /// <param name="thread">The thread on which to get the field's address.</param>
        /// <returns>The address of the field.</returns>
        abstract public ulong GetAddress(ClrAppDomain appDomain, ClrThread thread);
    }

}
