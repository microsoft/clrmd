using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal unsafe sealed class MachOCoreDump : IDisposable
    {
        const uint X86_THREAD_STATE64 = 4;

        private readonly object _sync = new();
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        private readonly x86_thread_state64_t[] _threadContexts;
        private readonly MachHeader64 _header;
        private readonly MachOSegment[] _segments;
        private readonly MachOModule? _dylinker;

        private volatile Dictionary<ulong, MachOModule>? _modules;

        public uint ProcessId { get; }

        public Architecture Architecture => _header.CpuType switch
        {
            MachOCpuType.X86 => Architecture.X86,
            MachOCpuType.X86_64 => Architecture.Amd64,
            _ => Architecture.Unknown
        };

        public MachOCoreDump(Stream stream, bool leaveOpen, string displayName)
        {
            fixed (MachHeader64* header = &_header)
                if (stream.Read(new Span<byte>(header, sizeof(MachHeader64))) != sizeof(MachHeader64))
                    throw new IOException($"Failed to read header from {displayName}.");

            if (_header.Magic != MachHeader64.Magic64)
                throw new InvalidDataException($"'{displayName}' does not have a valid Mach-O header.");

            _stream = stream;
            _leaveOpen = leaveOpen;

            Dictionary<ulong, uint> threadIds = new Dictionary<ulong, uint>();
            List<x86_thread_state64_t> contexts = new List<x86_thread_state64_t>();
            List<MachOSegment> segments = new List<MachOSegment>((int)_header.NumberCommands);

            Console.WriteLine($"Commands: {_header.NumberCommands}");
            for (int i = 0; i < _header.NumberCommands; i++)
            {
                long position = stream.Position;
                LoadCommandHeader loadCommand = new LoadCommandHeader();
                stream.Read(new Span<byte>(&loadCommand, sizeof(LoadCommandHeader)));
                
                long next = position + loadCommand.Size;

                switch (loadCommand.Kind)
                {
                    case LoadCommandType.Segment64:

                        Segment64LoadCommand seg = new Segment64LoadCommand();
                        stream.Read(new Span<byte>(&seg, sizeof(Segment64LoadCommand)));

                        Console.WriteLine($"    cmd:{i} pos:{position:x} name:{seg.Name} addr:{seg.VMAddr:x} size:{seg.VMSize:x}");

                        if (seg.VMAddr == SpecialThreadInfoHeader.SpecialThreadInfoAddress)
                        {
                            stream.Position = (long)seg.FileOffset;

                            SpecialThreadInfoHeader threadInfo = Read<SpecialThreadInfoHeader>(stream);
                            if (threadInfo.Signature != SpecialThreadInfoHeader.SpecialThreadInfoSignature)
                            {
                                segments.Add(new MachOSegment(seg));
                            }
                            else
                            {
                                for (int j = 0; j < threadInfo.NumberThreadEntries; j++)
                                {
                                    SpecialThreadInfoEntry threadEntry = Read<SpecialThreadInfoEntry>(stream);
                                    threadIds[threadEntry.StackPointer] = threadEntry.ThreadId;
                                }
                            }
                        }
                        else
                        {
                            segments.Add(new MachOSegment(seg));
                        }

                        break;

                    case LoadCommandType.Thread:
                        long threadEnd = stream.Position + loadCommand.Size - sizeof(LoadCommandHeader);

                        switch (_header.CpuType)
                        {
                            case MachOCpuType.X86_64:
                                uint flavor = Read<uint>(stream);
                                uint count = Read<uint>(stream);

                                if (flavor == X86_THREAD_STATE64)
                                {
                                    x86_thread_state64_t threadState = Read<x86_thread_state64_t>(stream);
                                    Console.WriteLine($"flavor:{flavor:x} count:{count} sp:{threadState.__rsp:x} ip:{threadState.__rip:x}");
                                    contexts.Add(threadState);
                                }

                                break;

                            default:
                                Console.WriteLine($"Unsupported CPU type: {_header.CpuType}");
                                break;
                        }
                        break;

                    default:
                        Console.WriteLine($"    cmd:{i} kind:{loadCommand.Kind} size:{loadCommand.Size:x}");
                        break;
                }

                stream.Seek(next, SeekOrigin.Begin);
            }

            segments.Sort((x, y) => x.Address.CompareTo(y.Address));
            _segments = segments.ToArray();
            _threadContexts = contexts.ToArray();

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

        private static T Read<T>(Stream stream) where T: unmanaged
        {
            T value;
            stream.Read(new Span<byte>(&value, sizeof(T)));
            return value;
        }

        public MachOModule? GetModuleByBaseAddress(ulong baseAddress)
        {
            var modules = ReadModules();

            modules.TryGetValue(baseAddress, out MachOModule? result);
            return result;
        }

        public IEnumerable<MachOModule> EnumerateModules()
        {
            return ReadModules().Values;
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

        private Dictionary<ulong, MachOModule> ReadModules()
        {
            if (_modules != null)
                return _modules;

            if (_dylinker != null && _dylinker.TryLookupSymbol("_dyld_all_image_infos", out ulong dyld_allImage_address))
            {
                DyldAllImageInfos allImageInfo = ReadMemory<DyldAllImageInfos>(dyld_allImage_address);
                DyldImageInfo[] allImages = new DyldImageInfo[allImageInfo.infoArrayCount];

                fixed (DyldImageInfo* ptr = allImages)
                {
                    int count = ReadMemory(allImageInfo.infoArray.ToUInt64(), new Span<byte>(ptr, sizeof(DyldImageInfo) * allImages.Length)) / sizeof(DyldImageInfo);

                    Dictionary<ulong, MachOModule> modules = new Dictionary<ulong, MachOModule>(count);
                    for (int i = 0; i < count; i++)
                    {
                        ref DyldImageInfo image = ref allImages[i];

                        string path = ReadAscii(image.ImageFilePath.ToUInt64());
                        ulong baseAddress = image.ImageLoadAddress.ToUInt64();
                        modules[baseAddress] = new MachOModule(this, baseAddress, path);
                    }

                    _modules = modules;
                    return modules;
                }
            }

            return _modules = new Dictionary<ulong, MachOModule>();
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