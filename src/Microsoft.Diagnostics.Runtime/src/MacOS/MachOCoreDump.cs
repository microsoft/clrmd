using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal unsafe sealed class MachOCoreDump : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        private readonly MachHeader64 _header;
        private readonly MachOSegment[] _segments;
        private readonly MachOModule? _dylinker;

        private ImmutableArray<MachOModule> _modules;

        public ImmutableArray<MachOModule> Modules
        {
            get
            {
                if (_modules.IsDefault)
                {
                    ImmutableArray<MachOModule> modules = ReadModules();
                    _modules = modules;
                    return modules;
                }

                return _modules;
            }
        }

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

            foreach (MachOSegment seg in _segments)
            {
                MachHeader64 header = ReadMemory<MachHeader64>(seg.Address);
                if (header.Magic == MachHeader64.Magic64 && header.FileType == MachOFileType.Dylinker)
                {
                    _dylinker = new MachOModule(this, seg.Address, "dylinker");
                    break;
                }
            }
        }

        public T ReadMemory<T>(ulong address)
            where T: unmanaged
        {
            T t = default;

            int read = ReadMemory(address, new Span<byte>(&t, sizeof(T)));
            if (read == sizeof(T))
                return t;

            return default;
        }

        public int ReadMemory(ulong address, Span<byte> buffer)
        {
            if (address == 0)
                return 0;

            int read = 0;
            while (buffer.Length > 0 && FindSegmentContaining(address, out MachOSegment seg))
            {
                ulong offset = address - seg.Address;
                int len = Math.Min(buffer.Length, (int)(seg.Size - offset));
                if (len == 0)
                    break;

                long position = (long)(seg.FileOffset + offset);
                lock (_sync)
                {
                    _stream.Seek(position, SeekOrigin.Begin);
                    int count = _stream.Read(buffer.Slice(0, len));
                    if (count == 0)
                        break;
                    
                    read += count;
                    buffer = buffer.Slice(count);
                }
            }

            return read;
        }

        internal string ReadAscii(ulong address, int max)
        {
            Span<byte> buffer = new byte[max];
            int count = ReadMemory(address, buffer);
            if (count == 0)
                return "";

            buffer = buffer.Slice(0, count);
            if (buffer[buffer.Length - 1] == 0)
                buffer = buffer.Slice(0, buffer.Length - 1);
            string result = Encoding.ASCII.GetString(buffer);
            return result;
        }

        internal string ReadAscii(ulong address)
        {
            StringBuilder sb = new StringBuilder();

            int read = 0;
            Span<byte> buffer = new byte[32];

            while (true)
            {
                int count = ReadMemory(address + (uint)read, buffer);
                if (count <= 0)
                    return sb.ToString();

                foreach (byte b in buffer)
                    if (b == 0)
                        return sb.ToString();
                    else
                        sb.Append((char)b);

                read += count;
            }
        }

        private ImmutableArray<MachOModule> ReadModules()
        {
            if (_dylinker != null && _dylinker.TryLookupSymbol("_dyld_all_image_infos", out ulong dyld_allImage_address))
            {
                DyldAllImageInfos allImageInfo = ReadMemory<DyldAllImageInfos>(dyld_allImage_address);
                DyldImageInfo[] allImages = new DyldImageInfo[allImageInfo.infoArrayCount];

                fixed (DyldImageInfo* ptr = allImages)
                {
                    int count = ReadMemory(allImageInfo.infoArray.ToUInt64(), new Span<byte>(ptr, sizeof(DyldImageInfo) * allImages.Length)) / sizeof(DyldImageInfo);

                    List<MachOModule> modules = new List<MachOModule>(count);

                    for (int i = 0; i < count; i++)
                    {
                        ref DyldImageInfo image = ref allImages[i];

                        string path = ReadAscii(image.ImageFilePath.ToUInt64());
                        modules.Add(new MachOModule(this, image.ImageLoadAddress.ToUInt64(), path));
                    }

                    return modules.ToImmutableArray();
                }
            }

            return ImmutableArray<MachOModule>.Empty;
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