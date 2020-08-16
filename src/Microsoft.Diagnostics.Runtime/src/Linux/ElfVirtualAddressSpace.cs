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
        private readonly ElfProgramHeader[] _segments;
        private readonly IAddressSpace _addressSpace;

        public string Name => _addressSpace.Name;

        public ElfVirtualAddressSpace(IReadOnlyList<ElfProgramHeader> segments, IAddressSpace addressSpace)
        {
            Length = segments.Max(s => s.VirtualAddress + s.VirtualSize);
            // FileSize == 0 means the segment isn't backed by any data
            _segments = segments.Where(programHeader => programHeader.FileSize > 0).ToArray();
            _addressSpace = addressSpace;
        }

        public long Length { get; }

        public int Read(long position, Span<byte> buffer)
        {
            int bytesRead = 0;
            while (bytesRead != buffer.Length)
            {
                int i = 0;
                for (; i < _segments.Length; i++)
                {
                    ElfProgramHeader segment = _segments[i];
                    long virtualAddress = segment.VirtualAddress;
                    long virtualSize = segment.VirtualSize;

                    long upperAddress = virtualAddress + virtualSize;
                    if (virtualAddress <= position && position < upperAddress)
                    {
                        int bytesToReadRange = (int)Math.Min(buffer.Length - bytesRead, upperAddress - position);
                        long segmentOffset = position - virtualAddress;

                        Span<byte> slice = buffer.Slice(bytesRead, bytesToReadRange);
                        int bytesReadRange = segment.AddressSpace.Read(segmentOffset, slice);
                        if (bytesReadRange == 0)
                            goto done;

                        position += bytesReadRange;
                        bytesRead += bytesReadRange;
                        if (bytesReadRange < bytesToReadRange)
                            goto done;

                        break;
                    }
                }

                if (i == _segments.Length)
                    break;
            }

        done:
            buffer.Slice(bytesRead).Clear();
            return bytesRead;
        }
    }
}
