// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class RelativeAddressSpace : IAddressSpace
    {
        private readonly IAddressSpace _baseAddressSpace;
        private readonly long _baseStart;
        private readonly long _length;
        private readonly long _baseToRelativeShift;
        private readonly string _name;

        public string Name => _name == null ? _baseAddressSpace.Name : $"{_baseAddressSpace.Name}:{_name}";

        public RelativeAddressSpace(IAddressSpace baseAddressSpace, string name, long startOffset, long length) :
            this(baseAddressSpace, name, startOffset, length, -startOffset)
        {
        }

        public RelativeAddressSpace(IAddressSpace baseAddressSpace, string name, long startOffset, long length, long baseToRelativeShift)
        {
            _baseAddressSpace = baseAddressSpace;
            _baseStart = startOffset;
            _length = length;
            _baseToRelativeShift = baseToRelativeShift;
            _name = name;
        }

        public int Read(long position, byte[] buffer, int bufferOffset, int count)
        {
            long basePosition = position - _baseToRelativeShift;
            if (basePosition < _baseStart)
                return 0;

            count = (int)Math.Min(count, _length);
            return _baseAddressSpace.Read(basePosition, buffer, bufferOffset, count);
        }

        public long Length => _baseStart + _length + _baseToRelativeShift;
    }
}