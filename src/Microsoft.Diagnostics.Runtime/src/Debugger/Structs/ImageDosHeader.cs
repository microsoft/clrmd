using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IMAGE_DOS_HEADER
    {
        [FieldOffset(0)]
        public UInt16 e_magic; // Magic number
        [FieldOffset(2)]
        public UInt16 e_cblp; // Bytes on last page of file
        [FieldOffset(4)]
        public UInt16 e_cp; // Pages in file
        [FieldOffset(6)]
        public UInt16 e_crlc; // Relocations
        [FieldOffset(8)]
        public UInt16 e_cparhdr; // Size of header in paragraphs
        [FieldOffset(10)]
        public UInt16 e_minalloc; // Minimum extra paragraphs needed
        [FieldOffset(12)]
        public UInt16 e_maxalloc; // Maximum extra paragraphs needed
        [FieldOffset(14)]
        public UInt16 e_ss; // Initial (relative) SS value
        [FieldOffset(16)]
        public UInt16 e_sp; // Initial SP value
        [FieldOffset(18)]
        public UInt16 e_csum; // Checksum
        [FieldOffset(20)]
        public UInt16 e_ip; // Initial IP value
        [FieldOffset(22)]
        public UInt16 e_cs; // Initial (relative) CS value
        [FieldOffset(24)]
        public UInt16 e_lfarlc; // File address of relocation table
        [FieldOffset(26)]
        public UInt16 e_ovno; // Overlay number
        [FieldOffset(28)]
        public fixed UInt16 e_res[4]; // Reserved words
        [FieldOffset(36)]
        public UInt16 e_oemid; // OEM identifier (for e_oeminfo)
        [FieldOffset(38)]
        public UInt16 e_oeminfo; // OEM information; e_oemid specific
        [FieldOffset(40)]
        public fixed UInt16 e_res2[10]; // Reserved words
        [FieldOffset(60)]
        public UInt32 e_lfanew; // File address of new exe header
    }
}