// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of a field in the target process.
    /// </summary>
    public abstract class ClrField
    {
        /// <summary>
        /// The name of the field.
        /// </summary>
        abstract public string Name { get; }

        /// <summary>
        /// Returns the type token of this field.
        /// </summary>
        abstract public uint Token { get; }

        /// <summary>
        /// The type of the field.  Note this property may return null on error.  There is a bug in several versions
        /// of our debugging layer which causes this.  You should always null-check the return value of this field.
        /// </summary>
        abstract public ClrType Type { get; }

        /// <summary>
        /// Returns the element type of this field.  Note that even when Type is null, this should still tell you
        /// the element type of the field.
        /// </summary>
        abstract public ClrElementType ElementType { get; }

        /// <summary>
        /// Returns true if this field is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this field is a primitive (int, float, etc), false otherwise.</returns>
        virtual public bool IsPrimitive { get { return ClrRuntime.IsPrimitive(ElementType); } }

        /// <summary>
        /// Returns true if this field is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this field is a ValueClass (struct), false otherwise.</returns>
        virtual public bool IsValueClass { get { return ClrRuntime.IsValueClass(ElementType); } }

        /// <summary>
        /// Returns true if this field is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this field is an object reference, false otherwise.</returns>
        virtual public bool IsObjectReference { get { return ClrRuntime.IsObjectReference(ElementType); } }

        /// <summary>
        /// Gets the size of this field.
        /// </summary>
        abstract public int Size { get; }

        /// <summary>
        /// Returns true if this field is public.
        /// </summary>
        abstract public bool IsPublic { get; }

        /// <summary>
        /// Returns true if this field is private.
        /// </summary>
        abstract public bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this field is internal.
        /// </summary>
        abstract public bool IsInternal { get; }

        /// <summary>
        /// Returns true if this field is protected.
        /// </summary>
        abstract public bool IsProtected { get; }

        /// <summary>
        /// Returns true if this field has a simple value (meaning you may call "GetFieldValue" in one of the subtypes
        /// of this class).
        /// </summary>
        abstract public bool HasSimpleValue { get; }

        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        virtual public int Offset { get { return -1; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            var type = Type;
            if (type != null)
                return string.Format("{0} {1}", type.Name, Name);

            return Name;
        }
    }

}
