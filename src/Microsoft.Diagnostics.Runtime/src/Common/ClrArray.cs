// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an array in the target process.
    /// </summary>
    public struct ClrArray : IEquatable<ClrArray>, IEquatable<ClrObject>
    {
        /// <summary>
        /// The address of the object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The type of the object.
        /// </summary>
        public ClrType Type { get; }

        private int _length;

        /// <summary>
        /// Returns the count of elements in this array.
        /// </summary>
        public int Length
        {
            get
            {
                if (_length == -1)
                {
                    _length = Type.ClrObjectHelpers.DataReader.Read<int>(Address + (ulong)IntPtr.Size);
                }

                return _length;
            }
        }

        public ClrArray(ulong address, ClrType type)
        {
            Address = address;
            Type = type;
            // default uninitialized value for size. Will be lazy loaded.
            _length = -1;
        }

        /// <summary>
        /// Gets <paramref name="count"/> value elements from the array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count"></param>
        /// <returns></returns>
        public T[]? GetContent<T>(int count) where T : unmanaged
        {
            if (count > this.Length)
            {
                throw new ArgumentException("Cannot read more elements than elements in array.");
            }

            return Type.GetArrayElementsValues<T>(Address, count);
        }

        /// <summary>
        /// Determines if this instance and another specific <see cref="ClrArray" /> have the same value.
        /// <para>Instances are considered equal when they have same <see cref="Address" />.</para>
        /// </summary>
        /// <param name="other">The <see cref="ClrArray" /> to compare to this instance.</param>
        /// <returns><c>true</c> if the <see cref="Address" /> of the parameter is same as <see cref="Address" /> in this instance; <c>false</c> otherwise.</returns>
        public override bool Equals(object? other)
        {
            if (other is null)
                return false;
            if (other is ulong ul)
                return ul == Address;
            if (other is ClrArray clrArray)
                return Address == clrArray.Address;
            if (other is ClrObject clrObject)
                return Address == clrObject.Address;

            return false;
        }

        /// <summary>
        /// Determines if this instance and another specific <see cref="ClrArray" /> have the same value.
        /// <para>Instances are considered equal when they have same <see cref="Address" />.</para>
        /// </summary>
        /// <param name="other">The <see cref="ClrArray" /> to compare to this instance.</param>
        /// <returns><c>true</c> if the <see cref="Address" /> of the parameter is same as <see cref="Address" /> in this instance; <c>false</c> otherwise.</returns>
        public bool Equals(ClrArray other) => Address == other.Address;

        /// <summary>
        /// Determines whether this instance and a specified object.
        /// </summary>
        /// <param name="other">The <see cref="ClrObject" /> to compare to this instance.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="other" /> is <see cref="ClrObject" />, and its <see cref="Address" /> is same as <see cref="Address" /> in this instance; <c>false</c>
        /// otherwise.
        /// </returns>
        public bool Equals(ClrObject other) => Address == other.Address;

        /// <summary>
        /// Returns the hash code for this <see cref="ClrArray" /> based on its <see cref="Address" />.
        /// </summary>
        /// <returns>An <see cref="int" /> hash code for this instance.</returns>
        public override int GetHashCode() => Address.GetHashCode();

        /// <summary>
        /// Determines whether two specified <see cref="ClrArray" /> have the same value.
        /// </summary>
        /// <param name="left">First <see cref="ClrArray" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrArray" /> to compare.</param>
        /// <returns><c>true</c> if <paramref name="left" /> <see cref="Equals(ClrArray)" /> <paramref name="right" />; <c>false</c> otherwise.</returns>
        public static bool operator ==(ClrArray left, ClrArray right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified <see cref="ClrArray" /> have different values.
        /// </summary>
        /// <param name="left">First <see cref="ClrArray" /> to compare.</param>
        /// <param name="right">Second <see cref="ClrArray" /> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left" /> is different from the value of <paramref name="right" />; <c>false</c> otherwise.</returns>
        public static bool operator !=(ClrArray left, ClrArray right) => !(left == right);
    }
}
