// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public readonly struct ClrObjectReference
    {
        /// <value>
        /// -1 if the target object is not referenced by a field.
        /// </value>
        public int FieldOffset { get; }
        public ulong Address { get; }
        public ClrType TargetType { get; }

        public ClrObjectReference(int fieldOffset, ulong address, ClrType targetType)
        {
            FieldOffset = fieldOffset;
            Address = address;
            TargetType = targetType;
        }

        public ClrObject Object => new ClrObject(Address, TargetType);

        public override bool Equals(object obj)
        {
            if (obj is ClrObjectReference objRef)
            {
                return FieldOffset == objRef.FieldOffset
                    && Address == objRef.Address
                    && TargetType == objRef.TargetType;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int code = FieldOffset.GetHashCode() ^ Address.GetHashCode();
            if (TargetType != null)
                code ^= TargetType.GetHashCode();

            return code;
        }

        public static bool operator ==(ClrObjectReference left, ClrObjectReference right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClrObjectReference left, ClrObjectReference right)
        {
            return !(left == right);
        }
    }
}