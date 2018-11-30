// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COR_TYPEID : IEquatable<COR_TYPEID>
    {
        public ulong token1;
        public ulong token2;

        public override int GetHashCode()
        {
            return (int)token1 + (int)token2;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is COR_TYPEID))
                return false;

            return Equals((COR_TYPEID)obj);
        }

        public bool Equals(COR_TYPEID other)
        {
            return token1 == other.token1 && token2 == other.token2;
        }
    }
}