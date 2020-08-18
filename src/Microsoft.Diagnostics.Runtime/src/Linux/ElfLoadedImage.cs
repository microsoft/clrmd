// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfLoadedImage
    {
        private readonly List<ElfFileTableEntryPointers64> _fileTable = new List<ElfFileTableEntryPointers64>(4);
        private readonly Reader _vaReader;
        private readonly bool _is64bit;
        private long _end;
        internal bool _containsExecutable;

        public string Path { get; }
        public long BaseAddress { get; private set; }
        public long Size => _end - BaseAddress;

        public ElfLoadedImage(Reader virtualAddressReader, bool is64bit, string path)
        {
            _vaReader = virtualAddressReader;
            _is64bit = is64bit;
            Path = path;
        }

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

        public PEImage OpenAsPEImage()
        {
            Stream stream = new ReaderStream(BaseAddress, Size, _vaReader);
            return new PEImage(stream, leaveOpen: false, isVirtual: _containsExecutable);
        }

        internal void AddTableEntryPointers(ElfFileTableEntryPointers64 pointers, bool isExecutable)
        {
            _fileTable.Add(pointers);
            _containsExecutable = _containsExecutable || isExecutable;

            long start = checked((long)pointers.Start);
            if (BaseAddress == 0 || start < BaseAddress)
                BaseAddress = start;

            long end = checked((long)pointers.Stop);
            if (_end < end)
                _end = end;
        }

        public override string ToString() => Path;
    }
}