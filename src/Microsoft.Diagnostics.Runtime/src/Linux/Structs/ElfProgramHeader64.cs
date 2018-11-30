// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfProgramHeader64
    {
        public ElfProgramHeaderType Type; // p_type
        public uint Flags; // p_flags
        public long FileOffset; // p_offset
        public long VirtualAddress; // p_vaddr
        public long PhysicalAddress; // p_paddr
        public long FileSize; // p_filesz
        public long VirtualSize; // p_memsz
        public long Alignment; // p_align
    }
}