// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct ArrayRankHandle : IEquatable<ArrayRankHandle>
    {
        private readonly ClrElementType _type;
        private readonly int _ranks;

        public ArrayRankHandle(ClrElementType eltype, int ranks)
        {
            _type = eltype;
            _ranks = ranks;
        }

        public bool Equals(ArrayRankHandle other)
        {
            return _type == other._type && _ranks == other._ranks;
        }
    }
}