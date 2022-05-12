// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A wrapper over IMAGE_OPTIONAL_HEADER.  See https://msdn.microsoft.com/en-us/library/windows/desktop/ms680339(v=vs.85).aspx.
    /// </summary>
    internal class ImageOptionalHeader
    {
        private readonly PEImage _parent;
        private readonly IMAGE_OPTIONAL_HEADER_AGNOSTIC _optional;
        private readonly IMAGE_OPTIONAL_HEADER_SPECIFIC _specific;
        private readonly IMAGE_DATA_DIRECTORY[] _directories;


        internal ImageOptionalHeader(PEImage parent, in IMAGE_OPTIONAL_HEADER_AGNOSTIC optional, IMAGE_DATA_DIRECTORY[] directories)
        {
            _parent = parent;
            _optional = optional;
            _directories = directories;

            _specific = parent.Read<IMAGE_OPTIONAL_HEADER_SPECIFIC>(parent.SpecificHeaderOffset);
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
        public uint BaseOfData => !_parent.IsPE64 ? _optional.BaseOfData : throw new InvalidOperationException();
        public ulong ImageBase => !_parent.IsPE64 ? _optional.ImageBase : _optional.ImageBase64;
        public uint SectionAlignment => _optional.SectionAlignment;
        public uint FileAlignment => _optional.FileAlignment;
        public ushort MajorOperatingSystemVersion => _optional.MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion => _optional.MinorOperatingSystemVersion;
        public ushort MajorImageVersion => _optional.MajorImageVersion;
        public ushort MinorImageVersion => _optional.MinorImageVersion;
        public ushort MajorSubsystemVersion => _optional.MajorSubsystemVersion;
        public ushort MinorSubsystemVersion => _optional.MinorSubsystemVersion;
        public uint Win32VersionValue => _optional.Win32VersionValue;
        public int SizeOfImage => (int)_optional.SizeOfImage;
        public uint SizeOfHeaders => _optional.SizeOfHeaders;
        public uint CheckSum => _optional.CheckSum;
        public ushort Subsystem => _optional.Subsystem;
        public ushort DllCharacteristics => _optional.DllCharacteristics;

        public ulong SizeOfStackReserve => _specific.SizeOfStackReserve;
        public ulong SizeOfStackCommit => _specific.SizeOfStackCommit;
        public ulong SizeOfHeapReserve => _specific.SizeOfHeapReserve;
        public ulong SizeOfHeapCommit => _specific.SizeOfHeapCommit;
        public uint LoaderFlags => _specific.LoaderFlags;
        public uint NumberOfRvaAndSizes => _specific.NumberOfRvaAndSizes;

        public IMAGE_DATA_DIRECTORY ExportDirectory => _directories[0];
        public IMAGE_DATA_DIRECTORY ImportDirectory => _directories[1];
        public IMAGE_DATA_DIRECTORY ResourceDirectory => _directories[2];
        public IMAGE_DATA_DIRECTORY ExceptionDirectory => _directories[3];
        public IMAGE_DATA_DIRECTORY CertificatesDirectory => _directories[4];
        public IMAGE_DATA_DIRECTORY BaseRelocationDirectory => _directories[5];
        public IMAGE_DATA_DIRECTORY DebugDirectory => _directories[6];
        public IMAGE_DATA_DIRECTORY ArchitectureDirectory => _directories[7];
        public IMAGE_DATA_DIRECTORY GlobalPointerDirectory => _directories[8];
        public IMAGE_DATA_DIRECTORY ThreadStorageDirectory => _directories[9];
        public IMAGE_DATA_DIRECTORY LoadConfigurationDirectory => _directories[10];
        public IMAGE_DATA_DIRECTORY BoundImportDirectory => _directories[11];
        public IMAGE_DATA_DIRECTORY ImportAddressTableDirectory => _directories[12];
        public IMAGE_DATA_DIRECTORY DelayImportDirectory => _directories[13];
        public IMAGE_DATA_DIRECTORY ComDescriptorDirectory => _directories[14];
    }
}
