using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Linux
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ElfHeader
    {
        const int EI_NIDENT = 16;

        const byte Magic0 = 0x7f;
        const byte Magic1 = (byte)'E';
        const byte Magic2 = (byte)'L';
        const byte Magic3 = (byte)'F';


        [MarshalAs(UnmanagedType.ByValArray, SizeConst = EI_NIDENT)]
        public byte[] Ident;
        public ELFHeaderType Type;
        public ushort Machine;
        public uint Version;
        
        public IntPtr Entry;
        public IntPtr ProgramHeaderOffset;
        public IntPtr SectionHeaderOffset;

        public uint Flags;
        public ushort EHSize;
        public ushort ProgramHeaderEntrySize;
        public ushort ProgramHeaderCount;
        public ushort SectionHeaderEntrySize;
        public ushort SectionHeaderCount;
        public ushort SectionHeaderStringIndex;


        public ELFClass Class
        {
            get
            {
                return (ELFClass)Ident[4];
            }
        }

        public ELFData Data
        {
            get
            {
                return (ELFData)Ident[5];
            }
        }

        public void Validate(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                filename = "This coredump";
            else
                filename = $"'{filename}'";

            if (Ident[0] != Magic0 ||
                Ident[1] != Magic1 ||
                Ident[2] != Magic2 ||
                Ident[3] != Magic3)
                throw new InvalidDataException($"{filename} does not contain a valid ELF header.");

            if (Data != ELFData.LittleEndian)
                throw new InvalidDataException($"{filename} is BigEndian, which is unsupported");
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ElfSectionHeader
    {
        public int NameIndex;                  // sh_name
        public ELFSectionHeaderType Type;       // sh_type
        public IntPtr Flags;                    // sh_flags
        public IntPtr VirtualAddress;           // sh_addr
        public IntPtr FileOffset;               // sh_offset
        public IntPtr FileSize;                 // sh_size
        public uint Link;                       // sh_link
        public uint Info;                       // sh_info
        public IntPtr Alignment;                // sh_addralign
        public IntPtr EntrySize;                // sh_entsize
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ELFProgramHeader64
    {
        public ELFProgramHeaderType Type;      // p_type
        public uint Flags;                     // p_flags
        public long FileOffset;                // p_offset
        public long VirtualAddress;            // p_vaddr
        public long PhysicalAddress;           // p_paddr
        public long FileSize;                  // p_filesz
        public long VirtualSize;               // p_memsz
        public long Alignment;                 // p_align
    }
    
    enum ElfMachine : ushort
    {
        EM_PARISC = 15, /* HPPA */
        EM_SPARC32PLUS = 18, /* Sun's "v8plus" */
        EM_PPC = 20, /* PowerPC */
        EM_PPC64 = 21	 , /* PowerPC64 */
        EM_SPU = 23, /* Cell BE SPU */
        EM_SH = 42, /* SuperH */
        EM_SPARCV9 = 43, /* SPARC v9 64-bit */
        EM_IA_64 = 50, /* HP/Intel IA-64 */
        EM_X86_64 = 62, /* AMD x86-64 */
        EM_S390 = 22, /* IBM S/390 */
        EM_CRIS = 76, /* Axis Communications 32-bit embedded processor */
        EM_V850 = 87, /* NEC v850 */
        EM_M32R = 88, /* Renesas M32R */
        EM_H8_300 = 46, /* Renesas H8/300,300H,H8S */
        EM_MN10300 = 89, /* Panasonic/MEI MN10300, AM33 */
        EM_BLACKFIN = 106, /* ADI Blackfin Processor */
        EM_FRV = 0x5441, /* Fujitsu FR-V */
        EM_AVR32 = 0x18ad, /* Atmel AVR32 */
    }

    enum ELFNoteType
    {
        PrpsStatus = 1,
        PrpsFpreg = 2,
        PrpsInfo = 3,
        TASKSTRUCT = 4,
        Aux = 6,

        File = 0x46494c45 // "FILE" in ascii
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ELFNoteHeader
    {
        public uint NameSize;
        public uint ContentSize;
        public ELFNoteType Type;
    }

    enum ELFProgramHeaderType : uint
    {
        Null = 0,
        Load = 1,
        Dynamic = 2,
        Interp = 3,
        Note = 4,
        Shlib = 5,
        Phdr = 6
    }


    enum ELFSectionHeaderType : uint
    {
        Null = 0,
        ProgBits = 1,
        SymTab = 2,
        StrTab = 3,
        Rela = 4,
        Hash = 5,
        Dynamic = 6,
        Note = 7,
        NoBits = 8,
        Rel = 9,
        ShLib = 10,
        DynSym = 11,
        InitArray = 14,
        FiniArray = 15,
        PreInitArray = 16,
        Group = 17,
        SymTabIndexes = 18,
        Num = 19,
        GnuAttributes = 0x6ffffff5,
        GnuHash = 0x6ffffff6,
        GnuLibList = 0x6ffffff7,
        CheckSum = 0x6ffffff8,
        GnuVerDef = 0x6ffffffd,
        GnuVerNeed = 0x6ffffffe,
        GnuVerSym = 0x6fffffff,
    }

    enum ELFHeaderType : ushort
    {
        Relocatable = 1,
        Executable = 2,
        Shared = 3,
        Core = 4
    }

    enum ELFClass : byte
    {
        None = 0,
        Class32 = 1,
        Class64 = 2
    }

    enum ELFData : byte
    {
        None = 0,
        LittleEndian = 1,
        BigEndian = 2
    }



    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ELFSignalInfo
    {
        public uint Number;
        public uint Code;
        public uint Errno;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TimeVal
    {
        public long Seconds;
        public long Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct RegSetX64
    {
        public ulong R15;
        public ulong R14;
        public ulong R13;
        public ulong R12;
        public ulong Rbp;
        public ulong Rbx;
        public ulong R11;
        public ulong R10;
        public ulong R8;
        public ulong R9;
        public ulong Rax;
        public ulong Rcx;
        public ulong Rdx;
        public ulong Rsi;
        public ulong Rdi;
        public ulong OrigRax;
        public ulong Rip;
        public ulong CS;
        public ulong EFlags;
        public ulong Rsp;
        public ulong SS;
        public ulong FS;
        public ulong GS;
        public ulong DS;
        public ulong ES;
        public ulong Reg25;
        public ulong Reg26;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ELFPRStatus
    {
        public ELFSignalInfo SignalInfo;
        public short CurrentSignal;
        public long SignalsPending;
        public long SignalsHeld;

        public uint Pid;
        public uint PPid;
        public uint PGrp;
        public uint Sid;

        public TimeVal UserTime;
        public TimeVal SystemTime;
        public TimeVal CUserTime;
        public TimeVal CSystemTime;

        public RegSetX64 RegisterSet;

        public int FPValid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ELFFileTableHeader
    {
        public IntPtr EntryCount;
        public IntPtr PageSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ELFFileTableEntryPointers
    {
        public IntPtr Start;
        public IntPtr Stop;
        public IntPtr PageOffset;
    }
}
