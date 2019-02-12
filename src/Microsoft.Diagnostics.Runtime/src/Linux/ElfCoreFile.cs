// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfCoreFile
    {
        private readonly Reader _reader;
        private ElfLoadedImage[] _loadedImages;
        private ELFVirtualAddressSpace _virtualAddressSpace;

        public ElfFile ElfFile { get; }
        public ElfMachine Architecture => (ElfMachine)ElfFile.Header.Machine;

        public IEnumerable<ElfPRStatus> EnumeratePRStatus()
        {
            return GetNotes(ElfNoteType.PrpsStatus).Select(r => r.ReadContents<ElfPRStatus>(0));
        }

        public IReadOnlyCollection<ElfLoadedImage> LoadedImages
        {
            get
            {
                LoadFileTable();
                return _loadedImages;
            }
        }

        public ElfCoreFile(Stream stream)
        {
            _reader = new Reader(new StreamAddressSpace(stream));
            ElfFile = new ElfFile(_reader);

            if (ElfFile.Header.Type != ElfHeaderType.Core)
                throw new InvalidDataException($"{stream.GetFilename() ?? "The given stream"} is not a coredump");

#if DEBUG
            LoadFileTable();
#endif
        }

        public int ReadMemory(long address, byte[] buffer, int bytesRequested)
        {
            if (_virtualAddressSpace == null)
                _virtualAddressSpace = new ELFVirtualAddressSpace(ElfFile.ProgramHeaders, _reader.DataSource);

            return _virtualAddressSpace.Read(address, buffer, 0, bytesRequested);
        }

        private IEnumerable<ElfNote> GetNotes(ElfNoteType type)
        {
            return ElfFile.Notes.Where(n => n.Type == type);
        }

        private void LoadFileTable()
        {
            if (_loadedImages != null)
                return;

            ElfNote fileNote = GetNotes(ElfNoteType.File).Single();

            long position = 0;
            ElfFileTableHeader header = fileNote.ReadContents<ElfFileTableHeader>(ref position);

            ElfFileTableEntryPointers[] fileTable = new ElfFileTableEntryPointers[header.EntryCount.ToInt32()];
            List<ElfLoadedImage> images = new List<ElfLoadedImage>(fileTable.Length);
            Dictionary<string, ElfLoadedImage> lookup = new Dictionary<string, ElfLoadedImage>(fileTable.Length);

            for (int i = 0; i < fileTable.Length; i++)
                fileTable[i] = fileNote.ReadContents<ElfFileTableEntryPointers>(ref position);

            long size = fileNote.Header.ContentSize - position;
            byte[] bytes = fileNote.ReadContents(position, (int)size);
            int start = 0;
            for (int i = 0; i < fileTable.Length; i++)
            {
                int end = start;
                while (bytes[end] != 0)
                    end++;

                string path = Encoding.ASCII.GetString(bytes, start, end - start);
                start = end + 1;

                if (!lookup.TryGetValue(path, out ElfLoadedImage image))
                    image = lookup[path] = new ElfLoadedImage(ElfFile.VirtualAddressReader, path);

                image.AddTableEntryPointers(fileTable[i]);
            }

            _loadedImages = lookup.Values.OrderBy(i => i.BaseAddress).ToArray();
        }
    }
}