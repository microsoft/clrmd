using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class ElfFile
    {
        private readonly Reader _reader;
        private readonly long _position;
        private readonly bool _virtual;

        private ElfNote[] _notes;

        private ElfProgramHeader[] _programHeaders;
        private ElfSectionHeader[] _sections;
        private string[] _sectionNames;

        public ElfHeader Header { get; }

        public IReadOnlyCollection<ElfNote> Notes { get { LoadNotes(); return _notes; } }
        public IReadOnlyList<ElfProgramHeader> ProgramHeaders { get { LoadProgramHeaders(); return _programHeaders; } }

        public ElfFile(Reader reader, long position = 0, bool virt=false)
        {
            _reader = reader;
            _position = position;
            _virtual = virt;

            Header = reader.Read<ElfHeader>(position);
            Header.Validate(reader.DataSource.Name);

#if DEBUG
            LoadSections();
            LoadAllSectionNames();
            LoadProgramHeaders();
            LoadNotes();
#endif
        }

        private void LoadNotes()
        {
            if (_notes != null)
                return;

            LoadProgramHeaders();
            ElfProgramHeader noteHeader = _programHeaders.SingleOrDefault(ph => ph.Header.Type == ELFProgramHeaderType.Note);

            if (noteHeader == null)
            {
                _notes = new ElfNote[0];
                return;
            }

            List<ElfNote> notes = new List<ElfNote>();

            Reader reader = new Reader(noteHeader.AddressSpace);
            long position = 0;
            while (position < reader.DataSource.Length)
            {
                ElfNote note = new ElfNote(reader, position);
                notes.Add(note);

                position += note.TotalSize;
            }

            _notes = notes.ToArray();
        }

        private void LoadProgramHeaders()
        {
            if (_programHeaders != null)
                return;

            _programHeaders = new ElfProgramHeader[Header.ProgramHeaderCount];
            for (int i = 0; i < _programHeaders.Length; i++)
                _programHeaders[i] = new ElfProgramHeader(_reader, _position + (long)Header.ProgramHeaderOffset + i * Header.ProgramHeaderEntrySize, _position, _virtual);
        }


        private string GetSectionName(int section)
        {
            LoadSections();
            if (section < 0 || section >= _sections.Length)
                throw new ArgumentOutOfRangeException(nameof(section));

            if (_sectionNames == null)
                _sectionNames = new string[_sections.Length];

            if (_sectionNames[section] != null)
                return _sectionNames[section];

            int nameTableIndex = Header.SectionHeaderStringIndex;
            if (Header.SectionHeaderOffset != IntPtr.Zero && Header.SectionHeaderCount > 0 && nameTableIndex != 0)
            {
                ref ElfSectionHeader hdr = ref _sections[nameTableIndex];
                long offset = hdr.FileOffset.ToInt64();
                int size = checked((int)hdr.FileSize.ToInt64());

                byte[] buffer = _reader.ReadBytes(offset, size);

                throw new NotImplementedException();
            }

            return _sectionNames[section];
        }

        private void LoadSections()
        {
            if (_sections != null)
                return;
            
            _sections = new ElfSectionHeader[Header.SectionHeaderCount];
            for (int i = 0; i < _sections.Length; i++)
                _sections[i] = _reader.Read<ElfSectionHeader>(_position + (long)Header.SectionHeaderOffset + i * Header.SectionHeaderEntrySize);
        }

#if DEBUG
        private void LoadAllSectionNames()
        {
            LoadSections();

            for (int i = 0; i < _sections.Length; i++)
                GetSectionName(i);
        }
#endif
    }
}
