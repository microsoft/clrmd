// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal class ElfNote
    {
        private readonly Reader _reader;
        private readonly long _position;
        private string? _name;

        public ElfNoteHeader Header { get; }

        public ElfNoteType Type => Header.Type;

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

        private static int HeaderSize => Marshal.SizeOf(typeof(ElfNoteHeader));

        public int ReadContents(long position, Span<byte> buffer)
        {
            long contentsoffset = _position + HeaderSize + Align4(Header.NameSize);
            return _reader.ReadBytes(position + contentsoffset, buffer);
        }

        public T ReadContents<T>(ref long position)
            where T : unmanaged
        {
            long contentsOffset = _position + HeaderSize + Align4(Header.NameSize);
            long locationOrig = contentsOffset + position;
            long location = locationOrig;
            T result = _reader.Read<T>(ref location);

            position += location - locationOrig;
            return result;
        }

        public T ReadContents<T>(long position)
            where T : unmanaged
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

            Header = _reader.Read<ElfNoteHeader>(_position);
        }

        private static uint Align4(uint x)
        {
            return (x + 3U) & ~3U;
        }
    }
}