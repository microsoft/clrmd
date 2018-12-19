// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A PEHeader is a reader of the data at the begining of a PEFile.    If the header bytes of a
    /// PEFile are read or mapped into memory, this class can parse it when given a pointer to it.
    /// It can read both 32 and 64 bit PE files.
    /// </summary>
    public sealed unsafe class PEHeader
    {
        /// <summary>
        /// Parses the given buffer for the header of a PEFile. If it can be parsed correctly,
        /// a new PEHeader object is constructed from the buffer and returned. Otherwise, null
        /// is returned.
        /// </summary>
        internal static PEHeader FromBuffer(PEBuffer buffer, bool virt)
        {
            byte* ptr = buffer.Fetch(0, 0x300);
            IMAGE_DOS_HEADER* tmpDos = (IMAGE_DOS_HEADER*)ptr;
            int needed = tmpDos->e_lfanew + sizeof(IMAGE_NT_HEADERS);
            if (buffer.Length < needed)
            {
                ptr = buffer.Fetch(0, needed);

                if (buffer.Length < needed)
                    return null;

                tmpDos = (IMAGE_DOS_HEADER*)ptr;
            }

            IMAGE_NT_HEADERS* tmpNt = (IMAGE_NT_HEADERS*)((byte*)tmpDos + tmpDos->e_lfanew);
            needed += tmpNt->FileHeader.SizeOfOptionalHeader + sizeof(IMAGE_SECTION_HEADER) * tmpNt->FileHeader.NumberOfSections;

            if (buffer.Length < needed)
            {
                ptr = buffer.Fetch(0, needed);
                if (buffer.Length < needed)
                    return null;
            }

            return new PEHeader(buffer, virt);
        }

        private PEHeader(PEBuffer buffer, bool virt)
        {
            _virt = virt;

            byte* ptr = buffer.Fetch(0, 0x300);
            _dosHeader = (IMAGE_DOS_HEADER*)ptr;
            _ntHeader = (IMAGE_NT_HEADERS*)(ptr + _dosHeader->e_lfanew);
            _sections = (IMAGE_SECTION_HEADER*)((byte*)_ntHeader + sizeof(IMAGE_NT_HEADERS) + _ntHeader->FileHeader.SizeOfOptionalHeader);

            if (buffer.Length < PEHeaderSize)
                throw new BadImageFormatException();
        }

        /// <summary>
        /// The total s,ize of the header,  including section array of the the PE header.
        /// </summary>
        public int PEHeaderSize => VirtualAddressToRva(_sections) + sizeof(IMAGE_SECTION_HEADER) * _ntHeader->FileHeader.NumberOfSections;

        /// <summary>
        /// Given a virtual address to data in a mapped PE file, return the relative virtual address (displacement from start of the image)
        /// </summary>
        public int VirtualAddressToRva(void* ptr)
        {
            return (int)((byte*)ptr - (byte*)_dosHeader);
        }

        /// <summary>
        /// Given a relative virtual address (displacement from start of the image) return the virtual address to data in a mapped PE file
        /// </summary>
        public void* RvaToVirtualAddress(int rva)
        {
            return (byte*)_dosHeader + rva;
        }

        /// <summary>
        /// Given a relative virtual address (displacement from start of the image) return a offset in the file data for that data.
        /// </summary>
        public int RvaToFileOffset(int rva)
        {
            if (_virt)
                return rva;

            for (int i = 0; i < _ntHeader->FileHeader.NumberOfSections; i++)
            {
                if (_sections[i].VirtualAddress <= rva && rva < _sections[i].VirtualAddress + _sections[i].VirtualSize)
                    return (int)_sections[i].PointerToRawData + (rva - (int)_sections[i].VirtualAddress);
            }

            throw new InvalidOperationException("Illegal RVA 0x" + rva.ToString("x"));
        }

        /// <summary>
        /// Given a relative virtual address (displacement from start of the image) return a offset in the file data for that data, if
        /// the RVA is valid. If the RVA is invalid, the method will return false. Otherwise, the method will return true and store the
        /// offset in the result parameter.
        /// </summary>
        public bool TryGetFileOffsetFromRva(int rva, out int result)
        {
            if (_virt)
            {
                result = rva;
                return true;
            }

            if (rva < (int)((byte*)_sections - (byte*)_dosHeader))
            {
                result = rva;
                return true;
            }

            for (int i = 0; i < _ntHeader->FileHeader.NumberOfSections; i++)
            {
                if (_sections[i].VirtualAddress <= rva && rva < _sections[i].VirtualAddress + _sections[i].VirtualSize)
                {
                    result = (int)_sections[i].PointerToRawData + (rva - (int)_sections[i].VirtualAddress);
                    return true;
                }
            }

            result = 0;
            return false;
        }

        /// <summary>
        /// Returns true if this is PE file for a 64 bit architecture.
        /// </summary>
        public bool IsPE64 => OptionalHeader32->Magic == 0x20b;
        /// <summary>
        /// Returns true if this file contains managed code (might also contain native code).
        /// </summary>
        public bool IsManaged => ComDescriptorDirectory.VirtualAddress != 0;

        // fields of code:IMAGE_NT_HEADERS
        /// <summary>
        /// Returns the 'Signture' of the PE HEader PE\0\0 = 0x4550, used for sanity checking.
        /// </summary>
        public uint Signature => _ntHeader->Signature;

        // fields of code:IMAGE_FILE_HEADER
        /// <summary>
        /// The machine this PE file is intended to run on
        /// </summary>
        public MachineType Machine => (MachineType)_ntHeader->FileHeader.Machine;
        /// <summary>
        /// PE files have a number of sections that represent regions of memory with the access permisions.  This is the nubmer of such sections.
        /// </summary>
        public ushort NumberOfSections => _ntHeader->FileHeader.NumberOfSections;
        /// <summary>
        /// The the PE file was created represented as the number of seconds since Jan 1 1970
        /// </summary>
        public int TimeDateStampSec => (int)_ntHeader->FileHeader.TimeDateStamp;
        /// <summary>
        /// The the PE file was created represented as a DateTime object
        /// </summary>
        public DateTime TimeDateStamp => TimeDateStampToDate(TimeDateStampSec);

        /// <summary>
        /// PointerToSymbolTable (see IMAGE_FILE_HEADER in PE File spec)
        /// </summary>
        public ulong PointerToSymbolTable => _ntHeader->FileHeader.PointerToSymbolTable;
        /// <summary>
        /// NumberOfSymbols (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ulong NumberOfSymbols => _ntHeader->FileHeader.NumberOfSymbols;
        /// <summary>
        /// SizeOfOptionalHeader (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ushort SizeOfOptionalHeader => _ntHeader->FileHeader.SizeOfOptionalHeader;
        /// <summary>
        /// Characteristics (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ushort Characteristics => _ntHeader->FileHeader.Characteristics;

        // fields of code:IMAGE_OPTIONAL_HEADER32 (or code:IMAGE_OPTIONAL_HEADER64)
        /// <summary>
        /// Magic (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort Magic => OptionalHeader32->Magic;
        /// <summary>
        /// MajorLinkerVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public byte MajorLinkerVersion => OptionalHeader32->MajorLinkerVersion;
        /// <summary>
        /// MinorLinkerVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public byte MinorLinkerVersion => OptionalHeader32->MinorLinkerVersion;
        /// <summary>
        /// SizeOfCode (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfCode => OptionalHeader32->SizeOfCode;
        /// <summary>
        /// SizeOfInitializedData (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfInitializedData => OptionalHeader32->SizeOfInitializedData;
        /// <summary>
        /// SizeOfUninitializedData (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfUninitializedData => OptionalHeader32->SizeOfUninitializedData;
        /// <summary>
        /// AddressOfEntryPoint (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint AddressOfEntryPoint => OptionalHeader32->AddressOfEntryPoint;
        /// <summary>
        /// BaseOfCode (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint BaseOfCode => OptionalHeader32->BaseOfCode;

        // These depend on the whether you are PE32 or PE64
        /// <summary>
        /// ImageBase (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong ImageBase
        {
            get
            {
                if (IsPE64) return OptionalHeader64->ImageBase;

                return OptionalHeader32->ImageBase;
            }
        }
        /// <summary>
        /// SectionAlignment (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SectionAlignment
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SectionAlignment;

                return OptionalHeader32->SectionAlignment;
            }
        }
        /// <summary>
        /// FileAlignment (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint FileAlignment
        {
            get
            {
                if (IsPE64) return OptionalHeader64->FileAlignment;

                return OptionalHeader32->FileAlignment;
            }
        }
        /// <summary>
        /// MajorOperatingSystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorOperatingSystemVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MajorOperatingSystemVersion;

                return OptionalHeader32->MajorOperatingSystemVersion;
            }
        }
        /// <summary>
        /// MinorOperatingSystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorOperatingSystemVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MinorOperatingSystemVersion;

                return OptionalHeader32->MinorOperatingSystemVersion;
            }
        }
        /// <summary>
        /// MajorImageVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorImageVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MajorImageVersion;

                return OptionalHeader32->MajorImageVersion;
            }
        }
        /// <summary>
        /// MinorImageVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorImageVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MinorImageVersion;

                return OptionalHeader32->MinorImageVersion;
            }
        }
        /// <summary>
        /// MajorSubsystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorSubsystemVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MajorSubsystemVersion;

                return OptionalHeader32->MajorSubsystemVersion;
            }
        }
        /// <summary>
        /// MinorSubsystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorSubsystemVersion
        {
            get
            {
                if (IsPE64) return OptionalHeader64->MinorSubsystemVersion;

                return OptionalHeader32->MinorSubsystemVersion;
            }
        }
        /// <summary>
        /// Win32VersionValue (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint Win32VersionValue
        {
            get
            {
                if (IsPE64) return OptionalHeader64->Win32VersionValue;

                return OptionalHeader32->Win32VersionValue;
            }
        }
        /// <summary>
        /// SizeOfImage (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfImage
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfImage;

                return OptionalHeader32->SizeOfImage;
            }
        }
        /// <summary>
        /// SizeOfHeaders (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfHeaders
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfHeaders;

                return OptionalHeader32->SizeOfHeaders;
            }
        }
        /// <summary>
        /// CheckSum (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint CheckSum
        {
            get
            {
                if (IsPE64) return OptionalHeader64->CheckSum;

                return OptionalHeader32->CheckSum;
            }
        }
        /// <summary>
        /// Subsystem (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort Subsystem
        {
            get
            {
                if (IsPE64) return OptionalHeader64->Subsystem;

                return OptionalHeader32->Subsystem;
            }
        }
        /// <summary>
        /// DllCharacteristics (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort DllCharacteristics
        {
            get
            {
                if (IsPE64) return OptionalHeader64->DllCharacteristics;

                return OptionalHeader32->DllCharacteristics;
            }
        }
        /// <summary>
        /// SizeOfStackReserve (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfStackReserve
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfStackReserve;

                return OptionalHeader32->SizeOfStackReserve;
            }
        }
        /// <summary>
        /// SizeOfStackCommit (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfStackCommit
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfStackCommit;

                return OptionalHeader32->SizeOfStackCommit;
            }
        }
        /// <summary>
        /// SizeOfHeapReserve (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfHeapReserve
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfHeapReserve;

                return OptionalHeader32->SizeOfHeapReserve;
            }
        }
        /// <summary>
        /// SizeOfHeapCommit (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfHeapCommit
        {
            get
            {
                if (IsPE64) return OptionalHeader64->SizeOfHeapCommit;

                return OptionalHeader32->SizeOfHeapCommit;
            }
        }
        /// <summary>
        /// LoaderFlags (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint LoaderFlags
        {
            get
            {
                if (IsPE64) return OptionalHeader64->LoaderFlags;

                return OptionalHeader32->LoaderFlags;
            }
        }
        /// <summary>
        /// NumberOfRvaAndSizes (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint NumberOfRvaAndSizes
        {
            get
            {
                if (IsPE64) return OptionalHeader64->NumberOfRvaAndSizes;

                return OptionalHeader32->NumberOfRvaAndSizes;
            }
        }

        // Well known data blobs (directories)  
        /// <summary>
        /// returns the data directory (virtual address an blob, of a data directory with index 'idx'.   14 are currently defined.
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY Directory(int idx)
        {
            if (idx >= NumberOfRvaAndSizes)
                return new Interop.IMAGE_DATA_DIRECTORY();

            return NTDirectories[idx];
        }

        /// <summary>
        /// Return the data directory for DLL Exports see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ExportDirectory => Directory(0);
        /// <summary>
        /// Return the data directory for DLL Imports see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ImportDirectory => Directory(1);
        /// <summary>
        /// Return the data directory for DLL Resources see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ResourceDirectory => Directory(2);
        /// <summary>
        /// Return the data directory for DLL Exceptions see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ExceptionDirectory => Directory(3);
        /// <summary>
        /// Return the data directory for DLL securiy certificates (Authenticode) see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY CertificatesDirectory => Directory(4);
        /// <summary>
        /// Return the data directory Image Base Relocations (RELOCS) see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY BaseRelocationDirectory => Directory(5);
        /// <summary>
        /// Return the data directory for Debug information see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY DebugDirectory => Directory(6);
        /// <summary>
        /// Return the data directory for DLL Exports see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ArchitectureDirectory => Directory(7);
        /// <summary>
        /// Return the data directory for GlobalPointer (IA64) see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY GlobalPointerDirectory => Directory(8);
        /// <summary>
        /// Return the data directory for THread local storage see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ThreadStorageDirectory => Directory(9);
        /// <summary>
        /// Return the data directory for Load Configuration see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY LoadConfigurationDirectory => Directory(10);
        /// <summary>
        /// Return the data directory for Bound Imports see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY BoundImportDirectory => Directory(11);
        /// <summary>
        /// Return the data directory for the DLL Import Address Table (IAT) see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ImportAddressTableDirectory => Directory(12);
        /// <summary>
        /// Return the data directory for Delayed Imports see PE file spec for more
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY DelayImportDirectory => Directory(13);
        /// <summary>
        /// see PE file spec for more .NET Runtime infomration.
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ComDescriptorDirectory => Directory(14);

        internal static DateTime TimeDateStampToDate(int timeDateStampSec)
        {
            // Convert seconds from Jan 1 1970 to DateTime ticks.  
            // The 621356004000000000L represents Jan 1 1970 as DateTime 100ns ticks.  
            DateTime ret = new DateTime((long)timeDateStampSec * 10000000 + 621356004000000000L, DateTimeKind.Utc).ToLocalTime();

            // From what I can tell TimeDateSec does not take into account daylight savings time when
            // computing the UTC time. Because of this we adjust here to get the proper local time.  
            if (ret.IsDaylightSavingTime())
                ret = ret.AddHours(-1.0);
            return ret;
        }

        internal int FileOffsetOfResources
        {
            get
            {
                if (ResourceDirectory.VirtualAddress == 0)
                    return 0;

                return RvaToFileOffset((int)ResourceDirectory.VirtualAddress);
            }
        }

        private IMAGE_OPTIONAL_HEADER32* OptionalHeader32 => (IMAGE_OPTIONAL_HEADER32*)((byte*)_ntHeader + sizeof(IMAGE_NT_HEADERS));
        private IMAGE_OPTIONAL_HEADER64* OptionalHeader64 => (IMAGE_OPTIONAL_HEADER64*)((byte*)_ntHeader + sizeof(IMAGE_NT_HEADERS));
        private Interop.IMAGE_DATA_DIRECTORY* NTDirectories
        {
            get
            {
                if (IsPE64)
                    return (Interop.IMAGE_DATA_DIRECTORY*)((byte*)_ntHeader + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER64));

                return (Interop.IMAGE_DATA_DIRECTORY*)((byte*)_ntHeader + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER32));
            }
        }

        private readonly IMAGE_DOS_HEADER* _dosHeader;
        private readonly IMAGE_NT_HEADERS* _ntHeader;
        private readonly IMAGE_SECTION_HEADER* _sections;
        private readonly bool _virt;
    }
}