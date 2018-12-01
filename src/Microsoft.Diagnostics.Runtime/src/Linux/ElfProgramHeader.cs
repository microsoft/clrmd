// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfProgramHeader
    {
        private readonly Reader _reader;
        private ElfProgramHeader64 _header;

        public ElfProgramHeader64 Header => _header;
        public ref ElfProgramHeader64 RefHeader => ref _header;
        public IAddressSpace AddressSpace { get; }

        public ElfProgramHeader(Reader reader, long headerPositon, long fileOffset, bool virt = false)
        {
            _reader = reader;
            _header = _reader.Read<ElfProgramHeader64>(headerPositon);

            if (virt && _header.Type == ElfProgramHeaderType.Load)
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", _header.VirtualAddress, _header.VirtualSize);
            else
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", fileOffset + _header.FileOffset, _header.FileSize);
        }
    }
}