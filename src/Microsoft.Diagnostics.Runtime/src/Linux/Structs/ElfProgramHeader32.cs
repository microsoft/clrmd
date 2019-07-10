// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfProgramHeader32
    {
        public ElfProgramHeaderType Type;   // p_type
        public int FileOffset;              // p_offset
        public int VirtualAddress;          // p_vaddr
        public int PhysicalAddress;         // p_paddr
        public int FileSize;                // p_filesz
        public int VirtualSize;             // p_memsz
        public uint Flags;                  // p_flags
        public uint Alignment;              // p_align
    }
}