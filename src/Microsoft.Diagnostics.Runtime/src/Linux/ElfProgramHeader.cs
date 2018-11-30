// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfProgramHeader
    {
        private readonly ElfReader _elfReader;
        private ElfProgramHeader64 _header;

        public ElfProgramHeader64 Header => _header;
        public ref ElfProgramHeader64 RefHeader => ref _header;
        public IAddressSpace AddressSpace { get; }

        public ElfProgramHeader(ElfReader reader, long headerPositon, long fileOffset, bool virt = false)
        {
            _elfReader = reader;
            _header = _elfReader.Read<ElfProgramHeader64>(headerPositon);

            if (virt && _header.Type == ElfProgramHeaderType.Load)
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", _header.VirtualAddress, _header.VirtualSize);
            else
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", fileOffset + _header.FileOffset, _header.FileSize);
        }
    }
}