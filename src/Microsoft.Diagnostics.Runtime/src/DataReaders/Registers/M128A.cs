// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

#pragma warning disable CA1066 // Type {0} should implement IEquatable<T> because it overrides Equals
namespace Microsoft.Diagnostics.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public struct M128A
    {
        public ulong Low;
        public ulong High;

        public void Clear()
        {
            Low = 0;
            High = 0;
        }

        public static bool operator ==(M128A lhs, M128A rhs)
        {
            return lhs.Low == rhs.Low && lhs.High == rhs.High;
        }

        public static bool operator !=(M128A lhs, M128A rhs)
        {
            return lhs.Low != rhs.Low || lhs.High != rhs.High;
        }

        public override bool Equals(object? obj)
        {
            return obj is M128A other && this == other;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}