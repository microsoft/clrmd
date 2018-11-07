// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it respresents a name and a type 
    /// </summary>
    public abstract class ClrInstanceField : ClrField
    {
        /// <summary>
        /// Returns the value of this field.  Equivalent to GetFieldValue(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field value for.</param>
        /// <returns>The value of the field.</returns>
        virtual public object GetValue(ulong objRef)
        {
            return GetValue(objRef, false, true);
        }

        /// <summary>
        /// Returns the value of this field, optionally specifying if this field is
        /// on a value class which is on the interior of another object.
        /// </summary>
        /// <param name="objRef">The object to get the field value for.</param>
        /// <param name="interior">Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.</param>
        /// <returns>The value of the field.</returns>
        virtual public object GetValue(ulong objRef, bool interior)
        {
            return GetValue(objRef, interior, true);
        }

        /// <summary>
        /// Returns the value of this field, optionally specifying if this field is
        /// on a value class which is on the interior of another object.
        /// </summary>
        /// <param name="objRef">The object to get the field value for.</param>
        /// <param name="interior">Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.</param>
        /// <param name="convertStrings">When true, the value of a string field will be 
        /// returned as a System.String object; otherwise the address of the String object will be returned.</param>
        /// <returns>The value of the field.</returns>
        abstract public object GetValue(ulong objRef, bool interior, bool convertStrings);

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <returns>The value of the field.</returns>
        virtual public ulong GetAddress(ulong objRef)
        {
            return GetAddress(objRef, false);
        }


        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <param name="interior">Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.</param>
        /// <returns>The value of the field.</returns>
        abstract public ulong GetAddress(ulong objRef, bool interior);
    }

}
