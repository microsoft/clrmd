using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal unsafe sealed class MachOCoreDump : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        private readonly MachHeader64 _header;
        private readonly MachOSegment[] _segments;


        public MachOCoreDump(Stream stream, bool leaveOpen, string displayName)
        {
            fixed (MachHeader64* header = &_header)
                if (stream.Read(new Span<byte>(header, sizeof(MachHeader64))) != sizeof(MachHeader64))
                    throw new IOException($"Failed to read header from {displayName}.");

            if (_header.Magic != MachHeader64.Magic64)
                throw new InvalidDataException($"'{displayName}' does not have a valid Mach-O header.");

            _stream = stream;
            _leaveOpen = leaveOpen;

            List<MachOSegment> segments = new List<MachOSegment>((int)_header.NumberCommands);

            Console.WriteLine($"Commands: {_header.NumberCommands}");
            for (int i = 0; i < _header.NumberCommands; i++)
            {
                LoadCommandHeader loadCommand = new LoadCommandHeader();
                stream.Read(new Span<byte>(&loadCommand, sizeof(LoadCommandHeader)));

                if (loadCommand.Kind == LoadCommandType.Segment64)
                {
                    Segment64LoadCommand seg = new Segment64LoadCommand();
                    stream.Read(new Span<byte>(&seg, sizeof(Segment64LoadCommand)));

                    Console.WriteLine($"    cmd:{i} name:{seg.Name} addr:{seg.VMAddr:x} size:{seg.VMSize:x}");
                    stream.Seek(loadCommand.Size - sizeof(LoadCommandHeader) - sizeof(Segment64LoadCommand), SeekOrigin.Current);

                    segments.Add(new MachOSegment(seg));
                }
                else
                {
                    Console.WriteLine($"    cmd:{i} kind:{loadCommand.Kind} size:{loadCommand.Size:x}");
                    stream.Seek(loadCommand.Size - sizeof(LoadCommandHeader), SeekOrigin.Current);
                }
            }

            segments.Sort((x, y) => x.Address.CompareTo(y.Address));
            _segments = segments.ToArray();
        }

        public int ReadMemory(ulong address, Span<byte> buffer)
        {
            if (address == 0)
                return 0;

            int read = 0;
            while (buffer.Length > 0 && FindSegmentContaining(address, out MachOSegment seg))
            {
                ulong offset = address - seg.Address;
                int len = Math.Min((int)buffer.Length, (int)(seg.Size - offset));

                if (len == 0)
                    break;

                long position = (long)(seg.FileOffset + offset);
                lock (_sync)
                {
                    _stream.Seek(position, SeekOrigin.Begin);
                    int count = _stream.Read(buffer.Slice(0, len));
                    read += count;

                    buffer = buffer.Slice(count);
                }
            }

            return read;
        }

        private bool FindSegmentContaining(ulong address, out MachOSegment seg)
        {
            int lower = 0;
            int upper = _segments.Length - 1;

            while (lower <= upper)
            {
                int mid = (lower + upper) >> 1;
                ref MachOSegment curr = ref _segments[mid];

                if (address < curr.Address)
                {
                    upper = mid - 1;
                }
                else if (address >= curr.Address + curr.Size)
                {
                    lower = mid + 1;
                }
                else
                {
                    seg = curr;
                    return true;
                }
            }

            seg = new MachOSegment();
            return false;
        }


        public void Dispose()
        {
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }
}