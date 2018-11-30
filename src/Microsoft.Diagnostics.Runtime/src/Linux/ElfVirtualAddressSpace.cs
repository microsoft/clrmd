// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfVirtualAddressSpace : IAddressSpace
    {
        private readonly IReadOnlyList<ElfProgramHeader> _segments;
        private readonly IAddressSpace _addressSpace;

        public string Name => _addressSpace.Name;

        public ElfVirtualAddressSpace(IReadOnlyList<ElfProgramHeader> segments, IAddressSpace addressSpace)
        {
            _segments = segments;
            Length = _segments.Max(s => s.Header.VirtualAddress + s.Header.VirtualSize);
            _addressSpace = addressSpace;
        }

        public long Length { get; }

        public int Read(long position, byte[] buffer, int bufferOffset, int count)
        {
            for (var i = 0; i < _segments.Count; i++)
            {
                ref var header = ref _segments[i].RefHeader;
                // FileSize == 0 means the segment isn't backed by any data
                if (header.FileSize > 0 && header.VirtualAddress <= position && position + count <= header.VirtualAddress + header.VirtualSize)
                {
                    var segmentOffset = position - header.VirtualAddress;
                    var fileBytes = (int)Math.Min(count, header.FileSize);

                    var fileOffset = header.FileOffset + segmentOffset;
                    var bytesRead = _addressSpace.Read(fileOffset, buffer, bufferOffset, fileBytes);

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