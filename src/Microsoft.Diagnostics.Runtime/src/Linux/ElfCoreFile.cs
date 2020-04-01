// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        private Dictionary<ulong, ulong> _auxvEntries;
        private ELFVirtualAddressSpace _virtualAddressSpace;

        public ElfFile ElfFile { get; }

        public IEnumerable<IElfPRStatus> EnumeratePRStatus()
        {
            ElfMachine architecture = ElfFile.Header.Architecture;

            return GetNotes(ElfNoteType.PrpsStatus).Select<ElfNote, IElfPRStatus>(r => {
                switch (architecture)
                {
                    case ElfMachine.EM_X86_64:
                        return r.ReadContents<ElfPRStatusX64>(0);

                    case ElfMachine.EM_ARM:
                        return r.ReadContents<ElfPRStatusArm>(0);

                    case ElfMachine.EM_AARCH64:
                        return r.ReadContents<ElfPRStatusArm64>(0);

                    case ElfMachine.EM_386:
                        return r.ReadContents<ElfPRStatusX86>(0);
                }
                throw new NotSupportedException($"Invalid architecture {architecture}");
            });
        }

        public ulong GetAuxvValue(ElfAuxvType type)
        {
            LoadAuxvTable();
            _auxvEntries.TryGetValue((ulong)type, out ulong value);
            return value;
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

        private void LoadAuxvTable()
        {
            if (_auxvEntries != null)
                return;

            _auxvEntries = new Dictionary<ulong, ulong>();
            ElfNote auxvNote = GetNotes(ElfNoteType.Aux).SingleOrDefault();
            if (auxvNote == null)
                throw new BadImageFormatException($"No auxv entries in coredump");

            long position = 0;
            while (true)
            {
                ulong type;
                ulong value;
                if (ElfFile.Header.Is64Bit)
                {
                    var elfauxv64 = auxvNote.ReadContents<ElfAuxv64>(ref position);
                    type = elfauxv64.Type;
                    value = elfauxv64.Value;
                }
                else
                {
                    var elfauxv32 = auxvNote.ReadContents<ElfAuxv32>(ref position);
                    type = elfauxv32.Type;
                    value = elfauxv32.Value;
                }
                if (type == (ulong)ElfAuxvType.Null)
                {
                    break;
                }
                _auxvEntries.Add(type, value);
            }
        }

        private void LoadFileTable()
        {
            if (_loadedImages != null)
                return;

            ElfNote fileNote = GetNotes(ElfNoteType.File).Single();

            long position = 0;
            ulong entryCount = 0;
            if (ElfFile.Header.Is64Bit)
            {
                ElfFileTableHeader64 header = fileNote.ReadContents<ElfFileTableHeader64>(ref position);
                entryCount = header.EntryCount;
            }
            else
            {
                ElfFileTableHeader32 header = fileNote.ReadContents<ElfFileTableHeader32>(ref position);
                entryCount = header.EntryCount;
            }

            ElfFileTableEntryPointers64[] fileTable = new ElfFileTableEntryPointers64[entryCount];
            Dictionary<string, ElfLoadedImage> lookup = new Dictionary<string, ElfLoadedImage>(fileTable.Length);

            for (int i = 0; i < fileTable.Length; i++)
            {
                if (ElfFile.Header.Is64Bit)
                {
                    fileTable[i] = fileNote.ReadContents<ElfFileTableEntryPointers64>(ref position);
                }
                else
                {
                    ElfFileTableEntryPointers32 entry = fileNote.ReadContents<ElfFileTableEntryPointers32>(ref position);
                    fileTable[i].Start = entry.Start;
                    fileTable[i].Stop = entry.Stop;
                    fileTable[i].PageOffset = entry.PageOffset;
                }
            }

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
                    image = lookup[path] = new ElfLoadedImage(ElfFile.VirtualAddressReader, ElfFile.Header.Is64Bit, path);

                image.AddTableEntryPointers(fileTable[i]);
            }

            _loadedImages = lookup.Values.OrderBy(i => i.BaseAddress).ToArray();
        }
    }
}