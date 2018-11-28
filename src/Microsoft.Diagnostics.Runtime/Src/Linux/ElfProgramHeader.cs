using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class ElfProgramHeader
    {
        private readonly Reader _reader;
        private ELFProgramHeader64 _header;

        public ELFProgramHeader64 Header => _header;
        public ref ELFProgramHeader64 RefHeader => ref _header;

        public IAddressSpace AddressSpace { get; }

        public ElfProgramHeader(Reader reader, long headerPositon, long fileOffset, bool virt = false)
        {
            _reader = reader;
            _header = _reader.Read<ELFProgramHeader64>(headerPositon);

            if (virt && _header.Type == ELFProgramHeaderType.Load)
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", _header.VirtualAddress, _header.VirtualSize);
            else
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", fileOffset + _header.FileOffset, _header.FileSize);
        }
    }
}
