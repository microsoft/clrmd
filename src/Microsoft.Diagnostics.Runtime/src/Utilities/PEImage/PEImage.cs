// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A class to read information out of PE images (dll/exe).
    /// </summary>
    public sealed unsafe class PEImage : IDisposable
    {
        private const ushort ExpectedDosHeaderMagic = 0x5A4D;   // MZ
        private const int PESignatureOffsetLocation = 0x3C;
        private const uint ExpectedPESignature = 0x00004550;    // PE00

        private readonly bool _isVirtual;
        private int _offset = 0;

        private readonly Stream _stream;
        private readonly PEHeaders? _peHeaders;
        private CoffHeader? _coffHeader;
        private PEHeader? _peHeader;
        private CorHeader? _corHeader;
        private ImmutableArray<SectionHeader> _sections;
        private ImmutableArray<PdbInfo> _pdbs;
        private ResourceEntry? _resources;

        private bool _disposed;

        /// <summary>
        /// Constructs a PEImage class for a given PE image (dll/exe) on disk.
        /// </summary>
        /// <param name="stream">A Stream that contains a PE image at its 0th offset.  This stream must be seekable.</param>
        /// <param name="leaveOpen">Whether or not to leave the stream open, if this is set to false stream will be
        /// disposed when this object is.</param>
        public PEImage(Stream stream, bool leaveOpen = false)
            : this(stream, leaveOpen, isVirtual: false)
        {
        }

        /// <summary>
        /// Constructs a PEImage class for a given PE image (dll/exe) in memory.
        /// </summary>
        /// <param name="stream">A Stream that contains a PE image at its 0th offset.  This stream must be seekable.</param>
        /// <param name="leaveOpen">Whether or not to leave the stream open, if this is set to false stream will be
        /// disposed when this object is.</param>
        /// <param name="isVirtual">Whether stream points to a PE image mapped into an address space (such as in a live process or crash dump).</param>
        public PEImage(Stream stream, bool leaveOpen, bool isVirtual)
        {
            _isVirtual = isVirtual;
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            if (!stream.CanSeek)
                throw new ArgumentException($"{nameof(stream)} is not seekable.");

            ushort dosHeaderMagic = Read<ushort>(0);
            if (dosHeaderMagic != ExpectedDosHeaderMagic)
            {
                if (!leaveOpen)
                    stream.Dispose();
                
                return;
            }

            int peHeaderOffset = Read<int>(PESignatureOffsetLocation);
            if (peHeaderOffset == 0)
            {
                if (!leaveOpen)
                    stream.Dispose();

                return;
            }

            uint peSignature = Read<uint>(peHeaderOffset);
            if (peSignature != ExpectedPESignature)
            {
                if (!leaveOpen)
                    stream.Dispose();

                return;
            }

            SeekTo(0);

            PEStreamOptions options = PEStreamOptions.Default;
            if (leaveOpen)
                options |= PEStreamOptions.LeaveOpen;

            if (isVirtual)
                options |= PEStreamOptions.IsLoadedImage;

            try
            {
                var reader = new PEReader(stream, options);
                _peHeaders = reader.PEHeaders;
                Reader = reader;
            }
            catch (BadImageFormatException)
            {
            }
        }

        internal int ResourceVirtualAddress => PEHeader?.ResourceTableDirectory.RelativeVirtualAddress ?? 0;

        /// <summary>
        /// Gets the root resource node of this PEImage.
        /// </summary>
        public ResourceEntry Resources => _resources ??= CreateResourceRoot();

        /// <summary>
        /// Gets a value indicating whether the given Stream contains a valid DOS header and PE signature.
        /// </summary>
        public bool IsValid => Reader != null;

        /// <summary>
        /// Gets a value indicating whether this image is for a 64bit processor.
        /// </summary>
        public bool IsPE64 => PEHeader != null && PEHeader.Magic != PEMagic.PE32;

        /// <summary>
        /// Gets a value indicating whether this image is managed. (.NET image)
        /// </summary>
        public bool IsManaged => PEHeader != null && PEHeader.CorHeaderTableDirectory.RelativeVirtualAddress != 0;

        /// <summary>
        /// Gets the timestamp that this PE image is indexed under.
        /// </summary>
        public int IndexTimeStamp => CoffHeader?.TimeDateStamp ?? 0;

        /// <summary>
        /// Gets the file size that this PE image is indexed under.
        /// </summary>
        public int IndexFileSize => PEHeader?.SizeOfImage ?? 0;

        /// <summary>
        /// Gets the managed header information for this image.  Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public CorHeader? CorHeader => _corHeader ??= ReadCorHeader();

        /// <summary>
        /// Gets a wrapper over this PE image's IMAGE_FILE_HEADER structure.  Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public CoffHeader? CoffHeader => _coffHeader ??= ReadCoffHeader();

        /// <summary>
        /// Gets a wrapper over this PE image's IMAGE_OPTIONAL_HEADER.  Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public PEHeader? PEHeader => _peHeader ??= ReadPEHeader();

        /// <summary>
        /// Gets a collection of IMAGE_SECTION_HEADERs in the PE iamge.  Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public ImmutableArray<SectionHeader> Sections => !_sections.IsDefault ? _sections : (_sections = ReadSections());

        /// <summary>
        /// Gets a list of PDBs associated with this PE image.  PE images can contain multiple PDB entries,
        /// but by convention it's usually the last entry that is the most up to date.  Unless you need to enumerate
        /// all PDBs for some reason, you should use DefaultPdb instead.
        /// Undefined behavior if IsValid is <see langword="false"/>.
        /// </summary>
        public ImmutableArray<PdbInfo> Pdbs => !_pdbs.IsDefault ? _pdbs : (_pdbs = ReadPdbs());

        /// <summary>
        /// Gets the PDB information for this module.  If this image does not contain PDB info (or that information
        /// wasn't included in Stream) this returns <see langword="null"/>.  If multiple PDB streams are present, this method returns the
        /// last entry.
        /// </summary>
        public PdbInfo DefaultPdb => Pdbs.LastOrDefault();

        public PEReader? Reader { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                Reader?.Dispose();
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

            ImmutableArray<SectionHeader> sections = Sections;
            for (int i = 0; i < sections.Length; i++)
            {
                SectionHeader section = sections[i];
                if (section.VirtualAddress <= virtualAddress && virtualAddress < section.VirtualAddress + section.VirtualSize)
                    return section.PointerToRawData + (virtualAddress - section.VirtualAddress);
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
            if (size < 16)  // Arbtirarily small value to ensure it's non-zero and has at least a little data in it
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

        private ResourceEntry CreateResourceRoot()
        {
            return new ResourceEntry(this, null, "root", false, RvaToOffset(ResourceVirtualAddress));
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

        internal T Read<T>() where T : unmanaged => Read<T>(_offset);

        private void SeekTo(int offset)
        {
            if (offset != _offset)
            {
                _stream.Seek(offset, SeekOrigin.Begin);
                _offset = offset;
            }
        }

        private CoffHeader? ReadCoffHeader() => _peHeaders?.CoffHeader;

        private PEHeader? ReadPEHeader() => _peHeaders?.PEHeader;

        private CorHeader? ReadCorHeader() => _peHeaders?.CorHeader;

        private ImmutableArray<SectionHeader> ReadSections() => _peHeaders?.SectionHeaders ?? ImmutableArray<SectionHeader>.Empty;

        private ImmutableArray<PdbInfo> ReadPdbs()
        {
            if (Reader == null)
                return default;

            ImmutableArray<DebugDirectoryEntry> debugDirectories = Reader.ReadDebugDirectory();
            if (debugDirectories.IsEmpty)
                return ImmutableArray<PdbInfo>.Empty;

            ImmutableArray<PdbInfo>.Builder result = ImmutableArray.CreateBuilder<PdbInfo>(debugDirectories.Length);

            foreach (DebugDirectoryEntry entry in debugDirectories)
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView)
                {
                    CodeViewDebugDirectoryData data = Reader.ReadCodeViewDebugDirectoryData(entry);
                    PdbInfo pdb = new PdbInfo(data.Path, data.Guid, data.Age);
                    result.Add(pdb);
                }
            }

            return result.MoveOrCopyToImmutable();
        }
    }
}
