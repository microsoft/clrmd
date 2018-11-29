using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class ELFVirtualAddressSpace : IAddressSpace
    {
        private readonly IReadOnlyList<ElfProgramHeader> _segments;
        private readonly IAddressSpace _addressSpace;

        public string Name => _addressSpace.Name;

        public ELFVirtualAddressSpace(IReadOnlyList<ElfProgramHeader> segments, IAddressSpace addressSpace)
        {
            _segments = segments;
            Length = _segments.Max(s => s.Header.VirtualAddress + s.Header.VirtualSize);
            _addressSpace = addressSpace;
        }

        public long Length { get; }

        public int Read(long position, byte[] buffer, int bufferOffset, int count)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                ref ELFProgramHeader64 header = ref _segments[i].RefHeader;
                // FileSize == 0 means the segment isn't backed by any data
                if (header.FileSize > 0 && header.VirtualAddress <= position && position + count <= header.VirtualAddress + header.VirtualSize)
                {
                    long segmentOffset = position - header.VirtualAddress;
                    int fileBytes = (int)Math.Min(count, header.FileSize);

                    long fileOffset = header.FileOffset + segmentOffset;
                    int bytesRead = _addressSpace.Read(fileOffset, buffer, bufferOffset, fileBytes);

                    //zero the rest of the buffer if it is in the virtual address space but not the physical address space
                    if (bytesRead == fileBytes && fileBytes != count)
                    {
                        Array.Clear(buffer, bufferOffset + fileBytes, count - fileBytes);
                        bytesRead = count;
                    }

                    return bytesRead;
                }
            }

            return 0;
        }
    }
}
