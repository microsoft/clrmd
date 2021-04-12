// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A representation of an ELF loaded image section.
    /// </summary>
    public class ElfLoadedImage
    {
        private readonly List<ElfFileTableEntryPointers64> _fileTable = new(4);
        private readonly Reader _vaReader;
        private readonly bool _is64bit;
        private ulong _end;

        // The path of the image on disk.
        public string FileName { get; }

        /// <summary>
        /// The BaseAddress of this image
        /// </summary>
        public ulong BaseAddress { get; private set; }

        /// <summary>
        /// The size of this image in memory.
        /// </summary>
        public ulong Size => _end - BaseAddress;

        internal ElfLoadedImage(Reader virtualAddressReader, bool is64bit, string path)
        {
            _vaReader = virtualAddressReader;
            _is64bit = is64bit;
            FileName = path;
        }

        /// <summary>
        /// Open the loaded image as an ELFFile.
        /// </summary>
        /// <returns>An ELFFile if this is a valid ELF image, null otherwise.</returns>
        public ElfFile? Open()
        {
            IElfHeader? header;

            if (_is64bit)
                header = _vaReader.TryRead<ElfHeader64>(BaseAddress);
            else
                header = _vaReader.TryRead<ElfHeader32>(BaseAddress);

            if (header is null || !header.IsValid)
                return null;

            return new ElfFile(header, _vaReader, BaseAddress, true);
        }

        /// <summary>
        /// Returns this ELF loaded image as a stream.
        /// </summary>
        /// <returns></returns>
        public Stream AsStream()
        {
            Stream stream = new ReaderStream(BaseAddress, Size, _vaReader);
            return stream;
        }

        internal void AddTableEntryPointers(ElfFileTableEntryPointers64 pointers)
        {
            _fileTable.Add(pointers);

            if (BaseAddress == 0 || pointers.Start < BaseAddress)
                BaseAddress = pointers.Start;

            if (_end < pointers.Stop)
                _end = pointers.Stop;
        }

        /// <summary>
        /// Returns <see cref="FileName"/>.
        /// </summary>
        /// <returns><see cref="FileName"/></returns>
        public override string ToString() => FileName;
    }
}