// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfNote
    {
        private readonly ElfReader _elfReader;
        private readonly long _position;
        private string _name;

        public ElfNoteHeader Header { get; }

        public ElfNoteType Type => Header.Type;

        public string Name
        {
            get
            {
                if (_name != null)
                    return _name;

                var namePosition = _position + HeaderSize;
                _name = _elfReader.ReadNullTerminatedAscii(namePosition, (int)Header.NameSize);
                return _name;
            }
        }

        public long TotalSize => HeaderSize + Align4(Header.NameSize) + Align4(Header.ContentSize);

        private int HeaderSize => Marshal.SizeOf(typeof(ElfNoteHeader));

        public byte[] ReadContents(long position, int length)
        {
            var contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _elfReader.ReadBytes(position + contentsoffset, length);
        }

        public T ReadContents<T>(long position, uint nameSize)
            where T : struct
        {
            var contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _elfReader.Read<T>(contentsoffset + position);
        }

        public T ReadContents<T>(ref long position)
            where T : struct
        {
            var contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            var locationOrig = contentsOffset + position;
            var location = locationOrig;
            var result = _elfReader.Read<T>(ref location);

            position += location - locationOrig;
            return result;
        }

        public T ReadContents<T>(long position)
            where T : struct
        {
            var contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            var location = contentsOffset + position;
            var result = _elfReader.Read<T>(location);
            return result;
        }

        public ElfNote(ElfReader reader, long position)
        {
            _position = position;
            _elfReader = reader;

            Header = _elfReader.Read<ElfNoteHeader>(_position);
        }

        private uint Align4(uint x)
        {
            return (x + 3U) & ~3U;
        }
    }
}