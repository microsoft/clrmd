// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A helper class to represent ELF program headers.
    /// </summary>
    public sealed class ElfProgramHeader
    {
        private readonly ElfProgramHeaderAttributes _attributes;

        internal IAddressSpace AddressSpace { get; }

        /// <summary>
        /// The type of header this is.
        /// </summary>
        public ElfProgramHeaderType Type { get; }

        /// <summary>
        /// The VirtualAddress of this header.
        /// </summary>
        public long VirtualAddress { get; }

        /// <summary>
        /// The size of this header.
        /// </summary>
        public long VirtualSize { get; }

        /// <summary>
        /// The offset of this header within the file.
        /// </summary>
        public long FileOffset { get; }

        /// <summary>
        /// The size of this header within the file.
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// Whether this section of memory is executable.
        /// </summary>
        public bool IsExecutable => (_attributes & ElfProgramHeaderAttributes.Executable) != 0;

        /// <summary>
        /// Whether this section of memory is writable.
        /// </summary>
        public bool IsWritable => (_attributes & ElfProgramHeaderAttributes.Writable) != 0;

        internal ElfProgramHeader(Reader reader, bool is64bit, long headerPositon, long loadBias, bool isVirtual = false)
        {
            if (is64bit)
            {
                var header = reader.Read<ElfProgramHeader64>(headerPositon);
                _attributes = (ElfProgramHeaderAttributes)header.Flags;
                Type = header.Type;
                VirtualAddress = unchecked((long)header.VirtualAddress);
                VirtualSize = unchecked((long)header.VirtualSize);
                FileOffset = unchecked((long)header.FileOffset);
                FileSize = unchecked((long)header.FileSize);
            }
            else
            {
                var header = reader.Read<ElfProgramHeader32>(headerPositon);
                _attributes = (ElfProgramHeaderAttributes)header.Flags;
                Type = header.Type;
                VirtualAddress = header.VirtualAddress;
                VirtualSize = header.VirtualSize;
                FileOffset = header.FileOffset;
                FileSize = header.FileSize;
            }

            if (isVirtual)
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", loadBias + VirtualAddress, VirtualSize);
            else
                AddressSpace = new RelativeAddressSpace(reader.DataSource, "ProgramHeader", loadBias + FileOffset, FileSize);
        }
    }
}