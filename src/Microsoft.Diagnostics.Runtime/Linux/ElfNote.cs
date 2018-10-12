using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    class ElfNote
    {
        private readonly Reader _reader;
        private readonly long _position;
        private string _name;

        public ELFNoteHeader Header { get; }

        public ELFNoteType Type => Header.Type;

        public string Name
        {
            get
            {
                if (_name != null)
                    return _name;

                long namePosition = _position + HeaderSize;
                _name = _reader.ReadNullTerminatedAscii(namePosition, (int)Header.NameSize);
                return _name;
            }
        }

        public long TotalSize => HeaderSize + Align4(Header.NameSize) + Align4(Header.ContentSize);

        private int HeaderSize => Marshal.SizeOf(typeof(ELFNoteHeader));

        public byte[] ReadContents(long position, int length)
        {
            long contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _reader.ReadBytes(position + contentsoffset, length);
        }

        public T ReadContents<T>(long position, uint nameSize) where T: struct
        {
            long contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _reader.Read<T>(contentsoffset + position);
        }
        
        public T ReadContents<T>(ref long position) where T : struct
        {
            long contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            long locationOrig = contentsOffset + position;
            long location = locationOrig;
            T result = _reader.Read<T>(ref location);

            position += location - locationOrig;
            return result;
        }

        public T ReadContents<T>(long position) where T : struct
        {
            long contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            long location = contentsOffset + position;
            T result = _reader.Read<T>(location);
            return result;
        }
        
        public ElfNote(Reader reader, long position)
        {
            _position = position;
            _reader = reader;

            Header = _reader.Read<ELFNoteHeader>(_position);
        }

        private uint Align4(uint x)
        {
            return (x + 3U) & ~3U;
        }
    }
}
