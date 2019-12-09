// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of a field in the target process.
    /// </summary>
    public abstract class ClrField
    {
        /// <summary>
        /// The ClrType containing this field.
        /// </summary>
        public abstract ClrType Parent { get; }

        /// <summary>
        /// The name of the field.
        /// </summary>
        public abstract string? Name { get; }

        /// <summary>
        /// Returns the type token of this field.
        /// </summary>
        public abstract uint Token { get; }

        /// <summary>
        /// The type of the field.  Note this property may return null on error.  There is a bug in several versions
        /// of our debugging layer which causes this.  You should always null-check the return value of this field.
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// Returns the element type of this field.  Note that even when Type is null, this should still tell you
        /// the element type of the field.
        /// </summary>
        public abstract ClrElementType ElementType { get; }

        /// <summary>
        /// Returns true if this field is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this field is a primitive (int, float, etc), false otherwise.</returns>
        public virtual bool IsPrimitive => ElementType.IsPrimitive();

        /// <summary>
        /// Returns true if this field is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this field is a ValueClass (struct), false otherwise.</returns>
        public virtual bool IsValueClass => ElementType.IsValueClass();

        /// <summary>
        /// Returns true if this field is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this field is an object reference, false otherwise.</returns>
        public virtual bool IsObjectReference => ElementType.IsObjectReference();

        /// <summary>
        /// Gets the size of this field.
        /// </summary>
        public abstract int Size { get; }

        /// <summary>
        /// Returns true if this field is public.
        /// </summary>
        public abstract bool IsPublic { get; }

        /// <summary>
        /// Returns true if this field is private.
        /// </summary>
        public abstract bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this field is internal.
        /// </summary>
        public abstract bool IsInternal { get; }

        /// <summary>
        /// Returns true if this field is protected.
        /// </summary>
        public abstract bool IsProtected { get; }

        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1).
        /// </summary>
        public virtual int Offset => -1;

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string? ToString()
        {
            ClrType type = Type;
            if (type != null)
                return $"{type.Name} {Name}";

            return Name;
        }
    }
}