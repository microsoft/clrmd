// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NET45
using System;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class MemoryVirtualAddressSpace : IAddressSpace
    {
        private readonly LinuxLiveDataReader _dataReader;

        public MemoryVirtualAddressSpace(LinuxLiveDataReader dataReader)
        {
            _dataReader = dataReader;
        }

        public long Length => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public int Read(long position, byte[] buffer, int bufferOffset, int count)
        {
            _dataReader.ReadMemory((ulong)position, buffer, bufferOffset, out int read);
            return read;
        }
    }
}
#endif