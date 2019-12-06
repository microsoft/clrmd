// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// An interface implementation in the target process.
    /// </summary>
    public sealed class ClrInterface
    {
        /// <summary>
        /// The typename of the interface.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The interface that this interface inherits from.
        /// </summary>
        public ClrInterface? BaseInterface { get; }

        /// <summary>
        /// Display string for this interface.
        /// </summary>
        /// <returns>Display string for this interface.</returns>
        public override string ToString() => Name;

        public ClrInterface(string name, ClrInterface? baseInterface)
        {
            Name = name;
            BaseInterface = baseInterface;
        }

        /// <summary>
        /// Equals override.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>True if this interface equals another.</returns>
        public override bool Equals(object? obj)
        {
            if (!(obj is ClrInterface other))
                return false;

            if (Name != other.Name)
                return false;

            if (BaseInterface is null)
                return other.BaseInterface is null;

            return BaseInterface.Equals(other.BaseInterface);
        }

        /// <summary>
        /// GetHashCode override.
        /// </summary>
        /// <returns>A hashcode for this object.</returns>
        public override int GetHashCode()
        {
            int hashCode = 0;

            if (Name != null)
                hashCode ^= Name.GetHashCode();

            if (BaseInterface != null)
                hashCode ^= BaseInterface.GetHashCode();

            return hashCode;
        }
    }
}