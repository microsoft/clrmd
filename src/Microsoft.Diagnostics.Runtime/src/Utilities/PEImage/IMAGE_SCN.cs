using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms680341(v=vs.85).aspx
    /// </summary>
    [Flags]
    public enum IMAGE_SCN : uint
    {
        /// <summary>
        /// The section should not be padded to the next boundary. This flag is obsolete and is replaced by IMAGE_SCN_ALIGN_1BYTES.
        /// </summary>
        NO_PAD = 0x00000008,

        /// <summary>
        /// Section contains executable code.
        /// </summary>
        CNT_CODE = 0x00000020,

        /// <summary>
        /// The section contains initialized data.
        /// </summary>
        CNT_INITIALIZED_DATA = 0x00000040,

        /// <summary>
        /// The section contains uninitialized data.
        /// </summary>
        CNT_UNINITIALIZED_DATA = 0x00000080,

        /// <summary>
        /// The section contains comments or other information. This is valid only for object files.
        /// </summary>
        LNK_INFO = 0x00000200,

        /// <summary>
        /// The section will not become part of the image. This is valid only for object files.
        /// </summary>
        LNK_REMOVE = 0x00000800,

        /// <summary>
        /// The section contains COMDAT data. This is valid only for object files
        /// </summary>
        LNK_COMDAT = 0x00001000,

        /// <summary>
        /// Reset speculative exceptions handling bits in the TLB entries for this section.
        /// </summary>
        NO_DEFER_SPEC_EXC = 0x00004000,

        /// <summary>
        /// The section contains data referenced through the global pointer.
        /// </summary>
        GPREL = 0x00008000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_1BYTES = 0x00100000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_2BYTES = 0x00200000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_4BYTES = 0x00300000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_8BYTES = 0x00400000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_16BYTES = 0x00500000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_32BYTES = 0x00600000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_64BYTES = 0x00700000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_128BYTES = 0x00800000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_256BYTES = 0x00900000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_512BYTES = 0x00a00000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_1024BYTES = 0x00b00000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_2048BYTES = 0x00c00000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_4096BYTES = 0x00d00000,

        /// <summary>
        /// Alignment.  This is valid only for object files.
        /// </summary>
        ALIGN_8192BYTES = 0x00e00000,

        /// <summary>
        /// The section contains extended relocations. The count of relocations for the section
        /// exceeds the 16 bits that is reserved for it in the section header. If the NumberOfRelocations
        /// field in the section header is 0xffff, the actual relocation count is stored in the
        /// VirtualAddress field of the first relocation. It is an error if IMAGE_SCN_LNK_NRELOC_OVFL
        /// is set and there are fewer than 0xffff relocations in the section.
        /// </summary>
        LNK_NRELOC_OVFL = 0x01000000,

        /// <summary>
        /// The section can be discarded as needed.
        /// </summary>
        MEM_DISCARDABLE = 0x02000000,

        /// <summary>
        /// The section cannot be cached.
        /// </summary>
        MEM_NOT_CACHED = 0x04000000,

        /// <summary>
        /// The section cannot be paged.
        /// </summary>
        MEM_NOT_PAGED = 0x08000000,

        /// <summary>
        /// The section can be shared in memory.
        /// </summary>
        MEM_SHARED = 0x10000000,

        /// <summary>
        /// The section can be executed as code.
        /// </summary>
        MEM_EXECUTE = 0x20000000,

        /// <summary>
        /// The section can be read.
        /// </summary>
        MEM_READ = 0x40000000,

        /// <summary>
        /// The section can be written to.
        /// </summary>
        MEM_WRITE = 0x80000000,
    }
}
