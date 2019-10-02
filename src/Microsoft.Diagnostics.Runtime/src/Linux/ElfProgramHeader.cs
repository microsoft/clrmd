// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfProgramHeader
    {
        public IAddressSpace AddressSpace { get; }

        public ElfProgramHeaderType Type { get; }

        public long VirtualAddress { get; }

        public long VirtualSize { get; }

        public long FileOffset { get; }

        public long FileSize { get; }

        public ElfProgramHeader(Reader reader, bool is64bit, long headerPositon, long fileOffset, bool virt = false)
        {
            if (is64bit)
            {
                var header = reader.Read<ElfProgramHeader64>(headerPositon);
                Type = header.Type;
                VirtualAddress = unchecked((long)header.VirtualAddress);
                VirtualSize = unchecked((long)header.VirtualSize);
                FileOffset = unchecked((long)header.FileOffset);
                FileSize = unchecked((long)header.FileSize);
            }
            else
            {
                var header = reader.Read<ElfProgramHeader32>(headerPositon);
                Type = header.Type;
                VirtualAddress = header.VirtualAddress;
                VirtualSize = header.VirtualSize;
                FileOffset = header.FileOffset;
                FileSize = header.FileSize;
            }

            if (virt && Type == ElfProgramHeaderType.Load)
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", VirtualAddress, VirtualSize);
            else
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", fileOffset + FileOffset, FileSize);
        }
    }
}