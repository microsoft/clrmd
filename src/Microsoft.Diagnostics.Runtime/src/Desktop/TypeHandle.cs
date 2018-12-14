// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct TypeHandle : IEquatable<TypeHandle>
    {
        public readonly ulong MethodTable;
        public readonly ulong ComponentMethodTable;

        public TypeHandle(ulong mt)
        {
            MethodTable = mt;
            ComponentMethodTable = 0;
        }

        public TypeHandle(ulong mt, ulong cmt)
        {
            MethodTable = mt;
            ComponentMethodTable = cmt;
        }

        public override int GetHashCode()
        {
            return ((int)MethodTable + (int)ComponentMethodTable) >> 3;
        }

        bool IEquatable<TypeHandle>.Equals(TypeHandle other)
        {
            return MethodTable == other.MethodTable && ComponentMethodTable == other.ComponentMethodTable;
        }

        // TODO should not be needed. IEquatable should cover it.  
        public static IEqualityComparer<TypeHandle> EqualityComparer = new HeapTypeEqualityComparer();

        private class HeapTypeEqualityComparer : IEqualityComparer<TypeHandle>
        {
            public bool Equals(TypeHandle x, TypeHandle y)
            {
                return x.MethodTable == y.MethodTable && x.ComponentMethodTable == y.ComponentMethodTable;
            }

            public int GetHashCode(TypeHandle obj)
            {
                return ((int)obj.MethodTable + (int)obj.ComponentMethodTable) >> 3;
            }
        }
    }
}