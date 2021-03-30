// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfStringTable
    {
        private readonly Reader _reader;

        public ElfStringTable(Reader reader, long address, long size)
        {
            _reader = new Reader(new RelativeAddressSpace(reader.DataSource, "StringTable", address, size));
        }

        public string GetStringAtIndex(int index) => _reader.ReadNullTerminatedAscii(index);
    }
}