// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal sealed class ElfVirtualAddressSpace : IAddressSpace
    {
        private readonly ElfProgramHeader[] _segments;
        private readonly IAddressSpace _addressSpace;

        public string Name => _addressSpace.Name;

        public ElfVirtualAddressSpace(ImmutableArray<ElfProgramHeader> segments, IAddressSpace addressSpace)
        {
            // FileSize == 0 means the segment isn't backed by any data
            _segments = segments.Where(segment => segment.FileSize > 0).OrderBy(segment => segment.VirtualAddress).ThenBy(segment => segment.VirtualSize).ToArray();
            ElfProgramHeader lastSegment = _segments[_segments.Length - 1];
            Length = lastSegment.VirtualAddress + lastSegment.FileSize;
            _addressSpace = addressSpace;
        }

        public long Length { get; }

        public int Read(long address, Span<byte> buffer)
        {
            if (address == 0 || buffer.Length == 0)
                return 0;

            int i = GetFirstSegmentContaining(address);
            if (i < 0)
                return 0;

            int bytesRead = 0;
            for (; i < _segments.Length; i++)
            {
                ElfProgramHeader segment = _segments[i];
                long virtualAddress = segment.VirtualAddress;
                long virtualSize = segment.VirtualSize;

                if (virtualAddress > address)
                    break;

                if (address >= virtualAddress + virtualSize)
                    continue;

                long offset = address - virtualAddress;
                int toRead = (int)Math.Min(buffer.Length - bytesRead, virtualSize - offset);

                Span<byte> slice = buffer.Slice(bytesRead, toRead);
                int read = segment.AddressSpace.Read(offset, slice);
                if (read < toRead)
                    break;

                bytesRead += read;
                if (bytesRead == buffer.Length)
                    break;

                address += read;
            }

            return bytesRead;
        }

        private int GetFirstSegmentContaining(long address)
        {
            int lower = 0;
            int upper = _segments.Length - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;
                ElfProgramHeader segment = _segments[mid];
                long virtualAddress = segment.VirtualAddress;
                long virtualSize = segment.VirtualSize;

                if (virtualAddress <= address && address < virtualAddress + virtualSize)
                {
                    while (mid > 0 && address < _segments[mid - 1].VirtualAddress + _segments[mid - 1].VirtualSize)
                    {
                        mid--;
                    }

                    return mid;
                }

                if (address < virtualAddress)
                    upper = mid - 1;
                else
                    lower = mid + 1;
            }

            return -1;
        }
    }
}
