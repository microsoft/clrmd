using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A class to read information out of PE images (dll/exe).
    /// </summary>
    public unsafe class PEImage
    {
        private const ushort ExpectedDosHeaderMagic = 0x5A4D;   // MZ
        private const int PESignatureOffsetLocation = 0x3C;
        private const uint ExpectedPESignature = 0x00004550;    // PE00
        private const int ImageDataDirectoryCount = 15;
        private const int ComDataDirectory = 14;
        private const int DebugDataDirectory = 6;
        
        private readonly bool _virt;
        private readonly byte[] _buffer = new byte[260];
        private int _offset = 0;
        private readonly int _peHeaderOffset;

        private readonly Lazy<ImageFileHeader> _imageFileHeader;
        private readonly Lazy<ImageOptionalHeader> _imageOptionalHeader;
        private readonly Lazy<CorHeader> _corHeader;
        private readonly Lazy<List<SectionHeader>> _sections;
        private readonly Lazy<List<PdbInfo>> _pdbs;
        private readonly Lazy<Interop.IMAGE_DATA_DIRECTORY[]> _directories;

        private Interop.IMAGE_DATA_DIRECTORY GetDirectory(int index) => _directories.Value[index];
        private int HeaderOffset => _peHeaderOffset + sizeof(uint);
        private int OptionalHeaderOffset => HeaderOffset + sizeof(IMAGE_FILE_HEADER);
        private int SpecificHeaderOffset => OptionalHeaderOffset + sizeof(IMAGE_OPTIONAL_HEADER_AGNOSTIC);
        private int DataDirectoryOffset => SpecificHeaderOffset + (IsPE64 ? 5 * 8 : 6 * 4);
        private int ImageDataDirectoryOffset => DataDirectoryOffset + ImageDataDirectoryCount * sizeof(Interop.IMAGE_DATA_DIRECTORY);

        /// <summary>
        /// Constructs a PEImage class for a given PE image (dll/exe) on disk.
        /// </summary>
        /// <param name="stream">A Stream that contains a PE image at its 0th offset.  This stream must be seekable.</param>
        public PEImage(Stream stream)
            : this(stream, false)
        {
        }

        /// <summary>
        /// Constructs a PEImage class for a given PE image (dll/exe) on disk.
        /// </summary>
        /// <param name="stream">A Stream that contains a PE image at its 0th offset.  This stream must be seekable.</param>
        /// <param name="isVirtual">Whether stream points to a PE image mapped into an address space (such as in a live process or crash dump).</param>
        public PEImage(Stream stream, bool isVirtual)
        {
            if (!stream.CanSeek)
                throw new ArgumentException($"{nameof(stream)} is not seekable.");

            _virt = isVirtual;
            Stream = stream;

            ushort? dosHeaderMagic = Read<ushort>(0);
            if (dosHeaderMagic != ExpectedDosHeaderMagic)
            {
                IsValid = false;
            }
            else
            {
                _peHeaderOffset = Read<int>(PESignatureOffsetLocation) ?? 0;
                uint? peSignature = null;

                if (_peHeaderOffset != 0)
                    peSignature = Read<uint>(_peHeaderOffset);

                IsValid = peSignature.HasValue && peSignature.Value == ExpectedPESignature;
            }

            _imageFileHeader = new Lazy<ImageFileHeader>(ReadImageFileHeader);
            _imageOptionalHeader = new Lazy<ImageOptionalHeader>(ReadImageOptionalHeader);
            _corHeader = new Lazy<CorHeader>(ReadCorHeader);
            _directories = new Lazy<Interop.IMAGE_DATA_DIRECTORY[]>(ReadDataDirectories);
            _sections = new Lazy<List<SectionHeader>>(ReadSections);
            _pdbs = new Lazy<List<PdbInfo>>(ReadPdbs);
        }

        /// <summary>
        /// The underlying stream.
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Returns true if the given Stream contains a valid DOS header and PE signature.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Returns true if this image is for a 64bit processor.
        /// </summary>
        public bool IsPE64 => OptionalHeader != null ? OptionalHeader.Magic != 0x010b : false;

        /// <summary>
        /// Returns true if this image is managed.  (.Net image)
        /// </summary>
        public bool IsManaged => GetDirectory(14).VirtualAddress != 0;

        /// <summary>
        /// Returns the timestamp that this PE image is indexed under.
        /// </summary>
        public int IndexTimeStamp => (int)(Header?.TimeDateStamp ?? 0);

        /// <summary>
        /// Returns the file size that this PE image is indexed under.
        /// </summary>
        public int IndexFileSize => (int)(OptionalHeader?.SizeOfImage ?? 0);

        /// <summary>
        /// Returns the managed header information for this image.  Undefined behavior if IsValid is false.
        /// </summary>
        public CorHeader CorHeader => _corHeader.Value;

        /// <summary>
        /// Returns a wrapper over this PE image's IMAGE_FILE_HEADER structure.  Undefined behavior if IsValid is false.
        /// </summary>
        public ImageFileHeader Header => _imageFileHeader.Value;

        /// <summary>
        /// Returns a wrapper over this PE image's IMAGE_OPTIONAL_HEADER.  Undefined behavior if IsValid is false.
        /// </summary>
        public ImageOptionalHeader OptionalHeader => _imageOptionalHeader.Value;

        /// <summary>
        /// Returns a collection of IMAGE_SECTION_HEADERs in the PE iamge.  Undefined behavior if IsValid is false.
        /// </summary>
        public ReadOnlyCollection<SectionHeader> Sections => _sections.Value.AsReadOnly();

        /// <summary>
        /// Enumerates a list of PDBs associated with this PE image.  PE images can contain multiple PDB entries,
        /// but by convention it's usually the last entry that is the most up to date.  Unless you need to enumerate
        /// all PDBs for some reason, you should use DefaultPdb instead.
        /// Undefined behavior if IsValid is false.
        /// </summary>
        public ReadOnlyCollection<PdbInfo> Pdbs => _pdbs.Value.AsReadOnly();

        /// <summary>
        /// Returns the PDB information for this module.  If this image does not contain PDB info (or that information
        /// wasn't included in Stream) this returns null.  If multiple PDB streams are present, this method returns the
        /// last entry.
        /// </summary>
        public PdbInfo DefaultPdb => Pdbs.LastOrDefault();

        /// <summary>
        /// Allows you to convert between a virtual address to a stream offset for this module.
        /// </summary>
        /// <param name="virtualAddress">The address to translate.</param>
        /// <returns>The position in the stream of the data, -1 if the virtual address doesn't map to any location of the PE image.</returns>
        public int RvaToOffset(int virtualAddress)
        {
            if (_virt)
                return virtualAddress;

            List<SectionHeader> sections = _sections.Value;
            for (int i = 0; i < sections.Count; i++)
                if (sections[i].VirtualAddress <= virtualAddress && virtualAddress < sections[i].VirtualAddress + sections[i].VirtualSize)
                    return (int)sections[i].PointerToRawData + (virtualAddress - (int)sections[i].VirtualAddress);

            return -1;
        }

        /// <summary>
        /// Reads data out of PE image into a native buffer.
        /// </summary>
        /// <param name="dest">The location to write the data.</param>
        /// <param name="virtualAddress">The address to read from.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read from the image and written to dest.</returns>
        public int Read(IntPtr dest, int virtualAddress, int bytesRequested)
        {
            byte[] buffer = GetBuffer(bytesRequested);

            int offset = RvaToOffset(virtualAddress);
            if (offset == -1)
                return 0;

            SeekTo(offset);
            int read = Stream.Read(buffer, 0, bytesRequested);
            if (read > 0)
                Marshal.Copy(buffer, 0, dest, read);

            return read;
        }

        /// <summary>
        /// Reads data out of PE image into a byte array.
        /// </summary>
        /// <param name="dest">The location to write the data.</param>
        /// <param name="virtualAddress">The address to read from.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read from the image and written to dest.</returns>
        public int Read(byte[] dest, int virtualAddress, int bytesRequested)
        {
            int offset = RvaToOffset(virtualAddress);
            if (offset == -1)
                return 0;

            SeekTo(offset);
            int read = Stream.Read(dest, 0, bytesRequested);
            return read;
        }


        private List<SectionHeader> ReadSections()
        {
            List<SectionHeader> sections = new List<SectionHeader>();
            if (!IsValid)
                return sections;

            ImageFileHeader header = Header;
            if (header == null)
                return sections;

            SeekTo(ImageDataDirectoryOffset);

            // Sanity check, there's a null row at the end of the data directory table
            ulong? zero = Read<ulong>();
            if (zero != 0)
                return sections;

            for (int i = 0; i < header.NumberOfSections; i++)
            {
                IMAGE_SECTION_HEADER? sectionHdr = Read<IMAGE_SECTION_HEADER>();
                if (sectionHdr.HasValue)
                    sections.Add(new SectionHeader(sectionHdr.Value));
            }

            return sections;
        }

        private List<PdbInfo> ReadPdbs()
        {
            int offs = _offset;
            List<PdbInfo> result = new List<PdbInfo>();

            var debugData = GetDirectory(DebugDataDirectory);
            if (debugData.VirtualAddress != 0 && debugData.Size != 0)
            {
                if ((debugData.Size % sizeof(IMAGE_DEBUG_DIRECTORY)) != 0)
                    return result;

                int offset = RvaToOffset((int)debugData.VirtualAddress);
                if (offset == -1)
                    return result;

                int count = (int)debugData.Size / sizeof(IMAGE_DEBUG_DIRECTORY);
                List<Tuple<int, int>> entries = new List<Tuple<int, int>>(count);

                SeekTo(offset);
                for (int i = 0; i < count; i++)
                {
                    IMAGE_DEBUG_DIRECTORY? entryRead = Read<IMAGE_DEBUG_DIRECTORY>();
                    if (entryRead.HasValue)
                    {
                        IMAGE_DEBUG_DIRECTORY tmp = entryRead.Value;
                        if (tmp.Type == IMAGE_DEBUG_TYPE.CODEVIEW && tmp.SizeOfData >= sizeof(CV_INFO_PDB70))
                            entries.Add(Tuple.Create(_virt ? tmp.AddressOfRawData : tmp.PointerToRawData, tmp.SizeOfData));
                    }
                }

                foreach (Tuple<int, int> tmp in entries.OrderBy(e => e.Item1))
                {
                    int ptr = tmp.Item1;
                    int size = tmp.Item2;

                    int? cvSig = Read<int>(ptr);
                    if (cvSig.HasValue && cvSig.Value == CV_INFO_PDB70.PDB70CvSignature)
                    {
                        Guid guid = Read<Guid>() ?? default;
                        int age = Read<int>() ?? -1;

                        // sizeof(sig) + sizeof(guid) + sizeof(age) - [null char] = 0x18 - 1
                        int nameLen = size - 0x18 - 1;
                        string filename = ReadString(nameLen);
                        
                        PdbInfo pdb = new PdbInfo(filename, guid, age);
                        result.Add(pdb);
                    }
                }
            }

            return result;
        }

        private string ReadString(int len) => ReadString(_offset, len);
        

        private string ReadString(int offset, int len)
        {
            if (len > 4096)
                len = 4096;

            SeekTo(offset);

            byte[] buffer = GetBuffer(len);
            if (Stream.Read(buffer, 0, len) != len)
                return null;

            for (int i = 0; i < len; i++)
            {
                if (buffer[i] == 0)
                {
                    len = i;
                    break;
                }
            }
            
            return Encoding.ASCII.GetString(buffer, 0, len);
        }

        private T? Read<T>() where T : struct
        {
            return Read<T>(_offset);
        }

        private T? Read<T>(int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] buffer = GetBuffer(size);

            SeekTo(offset);
            if (Stream.Read(buffer, 0, size) != size)
                return null;

            _offset = offset + size;
            fixed (byte* tmp = buffer)
                return (T)Marshal.PtrToStructure(new IntPtr(tmp), typeof(T));
        }
        
        private byte[] GetBuffer(int size)
        {
            if (size <= _buffer.Length)
                return _buffer;

            return new byte[size];
        }

        private void SeekTo(int offset)
        {
            if (offset != _offset)
            {
                Stream.Seek(offset, SeekOrigin.Begin);
                _offset = offset;
            }
        }

        private ImageFileHeader ReadImageFileHeader()
        {
            if (!IsValid)
                return null;

            IMAGE_FILE_HEADER? header = Read<IMAGE_FILE_HEADER>(HeaderOffset);
            return header.HasValue ? new ImageFileHeader(header.Value) : null;
        }

        private Interop.IMAGE_DATA_DIRECTORY[] ReadDataDirectories()
        {
            Interop.IMAGE_DATA_DIRECTORY[] directories = new Interop.IMAGE_DATA_DIRECTORY[ImageDataDirectoryCount];

            if (!IsValid)
                return directories;

            SeekTo(DataDirectoryOffset);
            for (int i = 0; i < directories.Length; i++)
                directories[i] = Read<Interop.IMAGE_DATA_DIRECTORY>() ?? default;

            return directories;
        }

        private ImageOptionalHeader ReadImageOptionalHeader()
        {
            if (!IsValid)
                return null;

            IMAGE_OPTIONAL_HEADER_AGNOSTIC? optional = Read<IMAGE_OPTIONAL_HEADER_AGNOSTIC>(OptionalHeaderOffset);
            if (!optional.HasValue)
                return null;

            bool is32Bit = optional.Value.Magic == 0x010b;
            Lazy<IMAGE_OPTIONAL_HEADER_SPECIFIC> specific = new Lazy<IMAGE_OPTIONAL_HEADER_SPECIFIC>(() =>
            {
                SeekTo(SpecificHeaderOffset);
                return new IMAGE_OPTIONAL_HEADER_SPECIFIC()
                {
                    SizeOfStackReserve = (is32Bit ? Read<uint>() : Read<ulong>()) ?? 0,
                    SizeOfStackCommit = (is32Bit ? Read<uint>() : Read<ulong>()) ?? 0,
                    SizeOfHeapReserve = (is32Bit ? Read<uint>() : Read<ulong>()) ?? 0,
                    SizeOfHeapCommit = (is32Bit ? Read<uint>() : Read<ulong>()) ?? 0,
                    LoaderFlags = (Read<uint>()) ?? 0,
                    NumberOfRvaAndSizes = (Read<uint>()) ?? 0
                };
            });

            return new ImageOptionalHeader(optional.Value, specific, _directories, is32Bit);
        }

        private CorHeader ReadCorHeader()
        {
            var clrDataDirectory = GetDirectory(ComDataDirectory);

            int offset = RvaToOffset((int)clrDataDirectory.VirtualAddress);
            if (offset == -1)
                return null;

            IMAGE_COR20_HEADER? corHdr = Read<IMAGE_COR20_HEADER>(offset);
            return corHdr.HasValue ? new CorHeader(corHdr.Value) : null;
        }
    }
}
