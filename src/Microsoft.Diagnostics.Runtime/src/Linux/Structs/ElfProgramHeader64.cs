// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfProgramHeader64
    {
        public ElfProgramHeaderType Type;   // p_type
        public uint Flags;                  // p_flags
        public ulong FileOffset;            // p_offset
        public ulong VirtualAddress;        // p_vaddr
        public ulong PhysicalAddress;       // p_paddr
        public ulong FileSize;              // p_filesz
        public ulong VirtualSize;           // p_memsz
        public ulong Alignment;             // p_align
    }
}