// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A class to read information out of PE images (dll/exe).
    /// </summary>
    internal sealed unsafe class PEImage : IDisposable
    {
        private const ushort ExpectedDosHeaderMagic = 0x5A4D;   // MZ
        private const int PESignatureOffsetLocation = 0x3C;
        private const uint ExpectedPESignature = 0x00004550;    // PE00
        private const int ImageDataDirectoryCount = 15;
        private const int OptionalMagic32 = 0x010b;

        private int HeaderOffset => _peHeaderOffset + sizeof(uint);
        private int OptionalHeaderOffset => HeaderOffset + sizeof(ImageFileHeader);
        internal int SpecificHeaderOffset => OptionalHeaderOffset + sizeof(ImageOptionalHeader);
        private int DataDirectoryOffset => SpecificHeaderOffset + (IsPE64 ? 5 * 8 : 6 * 4);
        private int ImageDataDirectoryOffset => DataDirectoryOffset + ImageDataDirectoryCount * sizeof(ImageDataDirectory);

        private readonly Stream _stream;
        private int _offset;
        private readonly bool _leaveOpen;
        private readonly bool _isVirtual;

        private readonly int _peHeaderOffset;

        private ImmutableArray<PdbInfo> _pdbs;
        private ResourceEntry? _resources;
        
        private readonly ImageDataDirectory[] _directories = new ImageDataDirectory[ImageDataDirectoryCount];
        private readonly int _sectionCount;
        private ImageSectionHeader[]? _sections;
        private object? _metadata;
        private bool _disposed;

        public PEImage(FileStream stream, bool leaveOpen = false)
            : this(stream, leaveOpen, isVirtual: false)
        {
        }

        public PEImage(ReadVirtualStream stream, bool leaveOpen, bool isVirtual)
            :this((Stream)stream, leaveOpen, isVirtual)
        {
        }

        public PEImage(ReaderStream stream, bool leaveOpen, bool isVirtual)
            : this((Stream)stream, leaveOpen, isVirtual)
        {
        }

        /// <summary>
        /// Constructs a PEImage class for a given PE image (dll/exe) in memory.
        /// </summary>
        /// <param name="stream">A Stream that contains a PE image at its 0th offset.  This stream must be seekable.</param>
        /// <param name="leaveOpen">Whether or not to leave the stream open, if this is set to false stream will be
        /// disposed when this object is.</param>
        /// <param name="isVirtual">Whether stream points to a PE image mapped into an address space (such as in a live process or crash dump).</param>
        private PEImage(Stream stream, bool leaveOpen, bool isVirtual)
        {
            _isVirtual = isVirtual;
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;

            if (!stream.CanSeek)
                throw new ArgumentException($"{nameof(stream)} is not seekable.");

            ushort dosHeaderMagic = Read<ushort>(0);
            if (dosHeaderMagic != ExpectedDosHeaderMagic)
            {
                IsValid = false;
                return;
            }

            _peHeaderOffset = Read<int>(PESignatureOffsetLocation);
            if (_peHeaderOffset == 0)
            {
                IsValid = false;
                return;
            }

            uint peSignature = Read<uint>(_peHeaderOffset);
            if (peSignature != ExpectedPESignature)
            {
                IsValid = false;
                return;
            }

            // Read image_file_header
            ImageFileHeader header = Read<ImageFileHeader>(HeaderOffset);
            _sectionCount = header.NumberOfSections;
            IndexTimeStamp = header.TimeDateStamp;

            if (TryRead(OptionalHeaderOffset, out ImageOptionalHeader optional))
            {
                IsPE64 = optional.Magic != OptionalMagic32;
                IndexFileSize = optional.SizeOfImage;

                // Read directories.  In order for us to read directories, we need to know if
                // this is a x64 image or not, hence why this is wrapped in the above TryRead
                SeekTo(DataDirectoryOffset);
                for (int i = 0; i < _directories.Length; i++)
                    if (!TryRead(out _directories[i]))
                        break;
            }
        }

        internal int ResourceVirtualAddress => ResourceDirectory.VirtualAddress;

        /// <summary>
        /// Gets the root resource node of this PEImage.
        /// </summary>
        public ResourceEntry Resources => _resources ??= CreateResourceRoot();

        /// <summary>
        /// Gets a value indicating whether the given Stream contains a valid DOS header and PE signature.
        /// </summary>
        public bool IsValid { get; } = true;

        /// <summary>
        /// Gets a value indicating whether this image is for a 64bit processor.
        /// </summary>
        public bool IsPE64 { get; }

        /// <summary>
        /// Gets a value indicating whether this image is managed. (.NET image)
        /// </summary>
        public bool IsManaged => ComDescriptorDirectory.VirtualAddress != 0;

        private ImageDataDirectory ExportDirectory => _directories[0];
        private ImageDataDirectory DebugDataDirectory => _directories[6];
        private ImageDataDirectory ComDescriptorDirectory => _directories[14];
        internal ImageDataDirectory MetadataDirectory
        {
            get
            {
                // _metadata is an object to preserve atomicity
                if (_metadata is not null)
                    return (ImageDataDirectory)_metadata;

                ImageDataDirectory result = default;
                ImageDataDirectory corHdr = ComDescriptorDirectory;
                if (corHdr.VirtualAddress != 0 && corHdr.Size != 0)
                {
                    int offset = RvaToOffset(corHdr.VirtualAddress);
                    if (offset > 0)
                        result = Read<ImageCor20Header>(offset).MetaData;
                }

                _metadata = result;
                return result;
            }
        }
        private ImageDataDirectory ResourceDirectory => _directories[2];

        /// <summary>
        /// Gets the timestamp that this PE image is indexed under.
        /// </summary>
        public int IndexTimeStamp { get; }

        /// <summary>
        /// Gets the file size that this PE image is indexed under.
        /// </summary>
        public int IndexFileSize { get; }

        /// <summary>
        /// Gets a list of PDBs associated with this PE image.  PE images can contain multiple PDB entries,
        /// but by convention it's usually the last entry that is the most up to date.  Unless you need to enumerate
        /// all PDBs for some reason, you should use DefaultPdb instead.
        /// Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public ImmutableArray<PdbInfo> Pdbs
        {
            get
            {
                if (!_pdbs.IsDefault)
                    return _pdbs;

                var pdbs = ReadPdbs();
                _pdbs = pdbs;
                return pdbs;
            }
        }

        /// <summary>
        /// Gets the PDB information for this module.  If this image does not contain PDB info (or that information
        /// wasn't included in Stream) this returns <see langword="null"/>.  If multiple PDB streams are present, this method returns the
        /// last entry.
        /// </summary>
        public PdbInfo? DefaultPdb => Pdbs.LastOrDefault();


        public void Dispose()
        {
            if (!_disposed)
            {
                if (!_leaveOpen)
                    _stream.Dispose();

                _disposed = true;
            }
        }

        /// <summary>
        /// Allows you to convert between a virtual address to a stream offset for this module.
        /// </summary>
        /// <param name="virtualAddress">The address to translate.</param>
        /// <returns>The position in the stream of the data, -1 if the virtual address doesn't map to any location of the PE image.</returns>
        public int RvaToOffset(int virtualAddress)
        {
            if (_isVirtual)
                return virtualAddress;

            ImageSectionHeader[] sections = ReadSections();
            for (int i = 0; i < sections.Length; i++)
            {
                ref ImageSectionHeader section = ref sections[i];
                if (section.VirtualAddress <= virtualAddress && virtualAddress < section.VirtualAddress + section.VirtualSize)
                    return (int)(section.PointerToRawData + ((uint)virtualAddress - section.VirtualAddress));
            }

            return -1;
        }

        /// <summary>
        /// Reads data out of PE image into a native buffer.
        /// </summary>
        /// <param name="virtualAddress">The address to read from.</param>
        /// <param name="dest">The location to write the data.</param>
        /// <returns>The number of bytes actually read from the image and written to dest.</returns>
        public int Read(int virtualAddress, Span<byte> dest)
        {
            int offset = RvaToOffset(virtualAddress);
            if (offset == -1)
                return 0;

            SeekTo(offset);
            return _stream.Read(dest);
        }

        internal int ReadFromOffset(int offset, Span<byte> dest)
        {
            SeekTo(offset);
            return _stream.Read(dest);
        }

        /// <summary>
        /// Gets the File Version Information that is stored as a resource in the PE file.  (This is what the
        /// version tab a file's property page is populated with).
        /// </summary>
        public FileVersionInfo? GetFileVersionInfo()
        {
            ResourceEntry? versionNode = Resources.Children.FirstOrDefault(r => r.Name == "Version");
            if (versionNode is null || versionNode.ChildCount != 1)
                return null;

            versionNode = versionNode.Children[0];
            if (!versionNode.IsLeaf && versionNode.ChildCount == 1)
                versionNode = versionNode.Children[0];

            int size = versionNode.Size;
            if (size < 16)  // Arbitrarily small value to ensure it's non-zero and has at least a little data in it
                return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                int count = versionNode.GetData(buffer);
                return new FileVersionInfo(buffer.AsSpan(0, count));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        
        /// <summary>
        /// Returns the address of a module export symbol if found
        /// </summary>
        /// <param name="symbolName">symbol name (without the module name prepended)</param>
        /// <param name="offset">symbol offset returned</param>
        /// <returns>true if found</returns>
        public bool TryGetExportSymbol(string symbolName, out ulong offset)
        {
            try
            {
                ImageDataDirectory exportTableDirectory = ExportDirectory;
                if (exportTableDirectory.VirtualAddress != 0 && exportTableDirectory.Size != 0)
                {
                    if (TryRead(RvaToOffset(exportTableDirectory.VirtualAddress), out ImageExportDirectory exportDirectory))
                    {
                        for (int nameIndex = 0; nameIndex < exportDirectory.NumberOfNames; nameIndex++)
                        {
                            int namePointerRVA = Read<int>(RvaToOffset(exportDirectory.AddressOfNames + (sizeof(uint) * nameIndex)));
                            if (namePointerRVA != 0)
                            {
                                string name = ReadNullTerminatedAscii(namePointerRVA, maxLength: 4096);
                                if (name == symbolName)
                                {
                                    ushort ordinalForNamedExport = Read<ushort>(RvaToOffset(exportDirectory.AddressOfNameOrdinals + (sizeof(ushort) * nameIndex)));
                                    int exportRVA = Read<int>(RvaToOffset(exportDirectory.AddressOfFunctions + (sizeof(uint) * ordinalForNamedExport)));
                                    offset = (uint)RvaToOffset(exportRVA);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidDataException)
            {
            }

            offset = 0;
            return false;
        }

        private string ReadNullTerminatedAscii(int rva, int maxLength)
        {
            StringBuilder builder = new(64);
            Span<byte> bytes = stackalloc byte[64];

            bool done = false;
            int read, totalRead = 0;
            while (!done && (read = Read(rva, bytes)) != 0)
            {
                rva += read;
                for (int i = 0; !done && i < read; i++, totalRead++)
                {
                    if (totalRead < maxLength)
                    {
                        if (bytes[i] != 0)
                            builder.Append((char)bytes[i]);
                        else
                            done = true;
                    }
                    else
                        done = true;
                }
            }

            return builder.ToString();
        }


        private ResourceEntry CreateResourceRoot()
        {
            return new ResourceEntry(this, null, "root", false, RvaToOffset((int)ResourceVirtualAddress));
        }

        internal T Read<T>(int offset) where T : unmanaged => Read<T>(ref offset);

        internal unsafe T Read<T>(ref int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            T t = default;

            SeekTo(offset);
            int read = _stream.Read(new Span<byte>(&t, size));
            offset += read;
            _offset = offset;

            if (read != size)
                return default;

            return t;
        }

        internal unsafe bool TryRead<T>(ref int offset, out T result) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            T t = default;

            SeekTo(offset);
            int read = _stream.Read(new Span<byte>(&t, size));
            offset += read;
            _offset = offset;

            if (read != size)
            {
                result = default;
                return false;
            }

            result = t;
            return true;
        }

        internal unsafe bool TryRead<T>(int offset, out T result) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            T t = default;

            SeekTo(offset);
            int read = _stream.Read(new Span<byte>(&t, size));
            offset += read;
            _offset = offset;

            if (read != size)
            {
                result = default;
                return false;
            }

            result = t;
            return true;
        }

        internal unsafe bool TryRead<T>(out T result) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            T t = default;

            int read = _stream.Read(new Span<byte>(&t, size));
            _offset += read;

            if (read != size)
            {
                result = default;
                return false;
            }

            result = t;
            return true;
        }

        internal T Read<T>() where T : unmanaged => Read<T>(_offset);

        private void SeekTo(int offset)
        {
            if (offset != _offset)
            {
                _stream.Seek(offset, SeekOrigin.Begin);
                _offset = offset;
            }
        }

        private ImageSectionHeader[] ReadSections()
        {
            if (_sections is not null)
                return _sections;


            ImageSectionHeader[] sections = new ImageSectionHeader[_sectionCount];

            SeekTo(ImageDataDirectoryOffset);

            // Sanity check, there's a null row at the end of the data directory table
            if (!TryRead(out ulong zero) || zero != 0)
                return sections;

            for (int i = 0; i < sections.Length; i++)
            {
                if (!TryRead(out sections[i]))
                    break;
            }

            // We don't care about a race here
            _sections = sections;
            return sections;
        }


        private bool Read64Bit()
        {
            if (!IsValid)
                return false;

            int offset = OptionalHeaderOffset;
            if (!TryRead(ref offset, out ushort magic))
                return false;

            return magic != OptionalMagic32;
        }


        private ImmutableArray<PdbInfo> ReadPdbs()
        {
            try
            {
                ImageDataDirectory debugDirectory = DebugDataDirectory;
                if (debugDirectory.VirtualAddress != 0 && debugDirectory.Size != 0)
                {
                    int count = (int)(debugDirectory.Size / sizeof(ImageDebugDirectory));
                    int offset = RvaToOffset(debugDirectory.VirtualAddress);
                    if (offset == -1)
                        return ImmutableArray<PdbInfo>.Empty;

                    SeekTo(offset);
                    var result = ImmutableArray.CreateBuilder<PdbInfo>(count);
                    for (int i = 0; i < count; i++)
                    {
                        if (!TryRead(ref offset, out ImageDebugDirectory directory))
                            break;

                        if (directory.Type == ImageDebugType.CODEVIEW && directory.SizeOfData >= sizeof(CvInfoPdb70))
                        {
                            int ptr = _isVirtual ? directory.AddressOfRawData : directory.PointerToRawData;
                            if (TryRead(ptr, out int sig) && sig == CvInfoPdb70.PDB70CvSignature)
                            {
                                Guid guid = Read<Guid>();
                                int age = Read<int>();

                                // sizeof(sig) + sizeof(guid) + sizeof(age) - [null char] = 0x18 - 1
                                int nameLen = directory.SizeOfData - 0x18 - 1;
                                string? path = ReadString(nameLen);

                                if (path != null)
                                {
                                    PdbInfo pdb = new PdbInfo(path, guid, age);
                                    result.Add(pdb);
                                }
                            }

                        }
                    }

                    return result.MoveOrCopyToImmutable();
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidDataException)
            {
            }

            return ImmutableArray<PdbInfo>.Empty;
        }

        private string? ReadString(int len)
        {
            if (len > 4096)
                len = 4096;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                if (_stream.Read(buffer, 0, len) != len)
                    return null;

                int index = Array.IndexOf(buffer, (byte)'\0', 0, len);
                if (index >= 0)
                    len = index;

                return Encoding.ASCII.GetString(buffer, 0, len);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static T Read<T>(Stream stream) where T: unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            int read = stream.Read(buffer);
            if (read == 0)
                return default;

            if (read < buffer.Length)
                buffer = buffer.Slice(0, read);

            return Unsafe.As<byte, T>(ref buffer[0]);
        }

        private static T Read<T>(Stream stream, int offset) where T : unmanaged
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return Read<T>(stream);
        }
    }
}
