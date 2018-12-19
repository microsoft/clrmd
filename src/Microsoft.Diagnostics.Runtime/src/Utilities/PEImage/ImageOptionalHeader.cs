using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A wrapper over IMAGE_OPTIONAL_HEADER.  See https://msdn.microsoft.com/en-us/library/windows/desktop/ms680339(v=vs.85).aspx.
    /// </summary>
    public class ImageOptionalHeader
    {
        private readonly bool _32bit;
        private readonly IMAGE_OPTIONAL_HEADER_AGNOSTIC _optional;
        private readonly Lazy<IMAGE_OPTIONAL_HEADER_SPECIFIC> _specific;
        private readonly Lazy<Interop.IMAGE_DATA_DIRECTORY[]> _directories;

        private IMAGE_OPTIONAL_HEADER_SPECIFIC OptionalSpecific => _specific.Value;

        internal ImageOptionalHeader(IMAGE_OPTIONAL_HEADER_AGNOSTIC optional, Lazy<IMAGE_OPTIONAL_HEADER_SPECIFIC> specific, Lazy<Interop.IMAGE_DATA_DIRECTORY[]> directories, bool is32bit)
        {
            _optional = optional;
            _specific = specific;
            _32bit = is32bit;
            _directories = directories;
        }

        public ushort Magic => _optional.Magic;
        public byte MajorLinkerVersion => _optional.MajorLinkerVersion;
        public byte MinorLinkerVersion => _optional.MinorLinkerVersion;
        public uint SizeOfCode => _optional.SizeOfCode;
        public uint SizeOfInitializedData => _optional.SizeOfInitializedData;
        public uint SizeOfUninitializedData => _optional.SizeOfUninitializedData;
        public uint AddressOfEntryPoint => _optional.AddressOfEntryPoint;
        public uint BaseOfCode => _optional.BaseOfCode;

        /// <summary>
        /// Only valid in 32bit images.
        /// </summary>
        public uint BaseOfData => _32bit ? _optional.BaseOfData : throw new InvalidOperationException();
        public ulong ImageBase => _32bit ? _optional.ImageBase : _optional.ImageBase64;
        public uint SectionAlignment => _optional.SectionAlignment;
        public uint FileAlignment => _optional.FileAlignment;
        public ushort MajorOperatingSystemVersion => _optional.MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion => _optional.MinorOperatingSystemVersion;
        public ushort MajorImageVersion => _optional.MajorImageVersion;
        public ushort MinorImageVersion => _optional.MinorImageVersion;
        public ushort MajorSubsystemVersion => _optional.MajorSubsystemVersion;
        public ushort MinorSubsystemVersion => _optional.MinorSubsystemVersion;
        public uint Win32VersionValue => _optional.Win32VersionValue;
        public uint SizeOfImage => _optional.SizeOfImage;
        public uint SizeOfHeaders => _optional.SizeOfHeaders;
        public uint CheckSum => _optional.CheckSum;
        public ushort Subsystem => _optional.Subsystem;
        public ushort DllCharacteristics => _optional.DllCharacteristics;

        public ulong SizeOfStackReserve => OptionalSpecific.SizeOfStackReserve;
        public ulong SizeOfStackCommit => OptionalSpecific.SizeOfStackCommit;
        public ulong SizeOfHeapReserve => OptionalSpecific.SizeOfHeapReserve;
        public ulong SizeOfHeapCommit => OptionalSpecific.SizeOfHeapCommit;
        public uint LoaderFlags => OptionalSpecific.LoaderFlags;
        public uint NumberOfRvaAndSizes => OptionalSpecific.NumberOfRvaAndSizes;

        public Interop.IMAGE_DATA_DIRECTORY ExportDirectory => _directories.Value[0];
        public Interop.IMAGE_DATA_DIRECTORY ImportDirectory => _directories.Value[1];
        public Interop.IMAGE_DATA_DIRECTORY ResourceDirectory => _directories.Value[2];
        public Interop.IMAGE_DATA_DIRECTORY ExceptionDirectory => _directories.Value[3];
        public Interop.IMAGE_DATA_DIRECTORY CertificatesDirectory => _directories.Value[4];
        public Interop.IMAGE_DATA_DIRECTORY BaseRelocationDirectory => _directories.Value[5];
        public Interop.IMAGE_DATA_DIRECTORY DebugDirectory => _directories.Value[6];
        public Interop.IMAGE_DATA_DIRECTORY ArchitectureDirectory => _directories.Value[7];
        public Interop.IMAGE_DATA_DIRECTORY GlobalPointerDirectory => _directories.Value[8];
        public Interop.IMAGE_DATA_DIRECTORY ThreadStorageDirectory => _directories.Value[9];
        public Interop.IMAGE_DATA_DIRECTORY LoadConfigurationDirectory => _directories.Value[10];
        public Interop.IMAGE_DATA_DIRECTORY BoundImportDirectory => _directories.Value[11];
        public Interop.IMAGE_DATA_DIRECTORY ImportAddressTableDirectory => _directories.Value[12];
        public Interop.IMAGE_DATA_DIRECTORY DelayImportDirectory => _directories.Value[13];
        public Interop.IMAGE_DATA_DIRECTORY ComDescriptorDirectory => _directories.Value[14];
    }
}
