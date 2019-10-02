// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

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

        public override bool Equals(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (obj.GetType() != typeof(M128A))
                return false;

            return this == (M128A)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}