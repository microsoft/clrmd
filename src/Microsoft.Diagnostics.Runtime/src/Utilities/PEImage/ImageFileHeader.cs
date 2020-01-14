// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DbgEng;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A wrapper over IMAGE_FILE_HEADER.  See https://msdn.microsoft.com/en-us/library/windows/desktop/ms680313(v=vs.85).aspx.
    /// </summary>
    public class ImageFileHeader
    {
        private IMAGE_FILE_HEADER _header;

        internal ImageFileHeader(ref IMAGE_FILE_HEADER header)
        {
            _header = header;
        }

        /// <summary>
        /// Gets the architecture type of the computer. An image file can only be run on the specified computer or a system that emulates the specified computer.
        /// </summary>
        public IMAGE_FILE_MACHINE Machine => (IMAGE_FILE_MACHINE)_header.Machine;

        /// <summary>
        /// Gets the number of sections. This indicates the size of the section table, which immediately follows the headers. Note that the Windows loader limits the number of sections to 96.
        /// </summary>
        public ushort NumberOfSections => _header.NumberOfSections;

        /// <summary>
        /// Gets the offset of the symbol table, in bytes, or zero if no COFF symbol table exists.
        /// </summary>
        public uint PointerToSymbolTable => _header.PointerToSymbolTable;

        /// <summary>
        /// Gets the number of symbols in the symbol table.
        /// </summary>
        public uint NumberOfSymbols => _header.NumberOfSymbols;

        /// <summary>
        /// Gets the low 32 bits of the time stamp of the image. This represents the date and time the image was created by the linker. The value is represented in the number of seconds elapsed since midnight (00:00:00), January 1, 1970, Universal Coordinated Time, according to the system clock.
        /// </summary>
        public int TimeDateStamp => _header.TimeDateStamp;

        /// <summary>
        /// Gets the size of the optional header, in bytes. This value should be 0 for object files.
        /// </summary>
        public ushort SizeOfOptionalHeader => _header.SizeOfOptionalHeader;

        /// <summary>
        /// Gets the characteristics of the image.
        /// </summary>
        public IMAGE_FILE Characteristics => (IMAGE_FILE)_header.Characteristics;
    }
}
