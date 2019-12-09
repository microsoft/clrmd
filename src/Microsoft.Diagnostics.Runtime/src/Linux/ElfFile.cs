// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfFile
    {
        private readonly Reader _reader;
        private readonly long _position;
        private readonly bool _virtual;

        private Reader? _virtualAddressReader;
        private ElfNote[]? _notes;
        private ElfProgramHeader[]? _programHeaders;
        private byte[]? _buildId;

        public IElfHeader Header { get; }

        public IReadOnlyCollection<ElfNote> Notes
        {
            get
            {
                LoadNotes();
                return _notes!;
            }
        }
        public IReadOnlyList<ElfProgramHeader> ProgramHeaders
        {
            get
            {
                LoadProgramHeaders();
                return _programHeaders!;
            }
        }

        public Reader VirtualAddressReader
        {
            get
            {
                CreateVirtualAddressReader();
                return _virtualAddressReader!;
            }
        }

        public byte[]? BuildId
        {
            get
            {
                if (_buildId != null)
                    return _buildId;

                if (Header.ProgramHeaderOffset != 0 && Header.ProgramHeaderEntrySize > 0 && Header.ProgramHeaderCount > 0)
                {
                    try
                    {
                        foreach (ElfNote note in Notes)
                        {
                            if (note.Type == ElfNoteType.PrpsInfo && note.Name.Equals("GNU"))
                            {
                                _buildId = new byte[note.Header.ContentSize];
                                note.ReadContents(0, _buildId);
                                return _buildId;
                            }
                        }
                    }
                    catch (IOException)
                    {
                    }
                }

                return null;
            }
        }

        public ElfFile(Reader reader, long position = 0, bool isVirtual = false)
        {
            _reader = reader;
            _position = position;
            _virtual = isVirtual;

            if (isVirtual)
                _virtualAddressReader = reader;

            ElfHeaderCommon common = reader.Read<ElfHeaderCommon>(position);
            Header = common.GetHeader(reader, position)!;
            if (Header is null)
                throw new InvalidDataException($"{reader.DataSource.Name ?? "This coredump"} does not contain a valid ELF header.");
        }

        internal ElfFile(IElfHeader header, Reader reader, long position = 0, bool isVirtual = false)
        {
            _reader = reader;
            _position = position;
            _virtual = isVirtual;

            if (isVirtual)
                _virtualAddressReader = reader;

            Header = header;
        }

        private void CreateVirtualAddressReader()
        {
            _virtualAddressReader ??= new Reader(new ELFVirtualAddressSpace(ProgramHeaders, _reader.DataSource));
        }

        private void LoadNotes()
        {
            if (_notes != null)
                return;

            LoadProgramHeaders();

            List<ElfNote> notes = new List<ElfNote>();
            foreach (ElfProgramHeader programHeader in _programHeaders!)
            {
                if (programHeader.Type == ElfProgramHeaderType.Note)
                {
                    Reader reader = new Reader(programHeader.AddressSpace);
                    long position = 0;
                    while (position < reader.DataSource.Length)
                    {
                        ElfNote note = new ElfNote(reader, position);
                        notes.Add(note);

                        position += note.TotalSize;
                    }
                }
            }

            _notes = notes.ToArray();
        }

        private void LoadProgramHeaders()
        {
            if (_programHeaders != null)
                return;

            _programHeaders = new ElfProgramHeader[Header.ProgramHeaderCount];
            for (int i = 0; i < _programHeaders.Length; i++)
                _programHeaders[i] = new ElfProgramHeader(_reader, Header.Is64Bit, _position + Header.ProgramHeaderOffset + i * Header.ProgramHeaderEntrySize, _position, _virtual);
        }
    }
}