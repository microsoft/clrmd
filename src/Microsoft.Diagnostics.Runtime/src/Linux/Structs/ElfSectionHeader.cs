// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfSectionHeader
    {
        public int NameIndex; // sh_name
        public ElfSectionHeaderType Type; // sh_type
        public IntPtr Flags; // sh_flags
        public IntPtr VirtualAddress; // sh_addr
        public IntPtr FileOffset; // sh_offset
        public IntPtr FileSize; // sh_size
        public uint Link; // sh_link
        public uint Info; // sh_info
        public IntPtr Alignment; // sh_addralign
        public IntPtr EntrySize; // sh_entsize
    }
}