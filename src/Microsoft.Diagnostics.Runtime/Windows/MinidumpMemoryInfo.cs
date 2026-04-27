// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// MINIDUMP_MEMORY_INFO_LIST header. Followed by NumberOfEntries entries
    /// of <see cref="MinidumpMemoryInfo"/>, each <see cref="SizeOfEntry"/>
    /// bytes long.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct MinidumpMemoryInfoList
    {
        public readonly uint SizeOfHeader;
        public readonly uint SizeOfEntry;
        public readonly ulong NumberOfEntries;
    }

    /// <summary>
    /// Mirrors MINIDUMP_MEMORY_INFO, which itself mirrors
    /// MEMORY_BASIC_INFORMATION returned by <c>VirtualQuery</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct MinidumpMemoryInfo
    {
        public readonly ulong BaseAddress;
        public readonly ulong AllocationBase;
        public readonly uint AllocationProtect;
        public readonly uint Alignment1;
        public readonly ulong RegionSize;
        public readonly uint State;
        public readonly uint Protect;
        public readonly uint Type;
        public readonly uint Alignment2;

        // Win32 MEM_* state values
        public const uint MEM_COMMIT  = 0x1000;
        public const uint MEM_RESERVE = 0x2000;
        public const uint MEM_FREE    = 0x10000;

        // Win32 MEM_* type values
        public const uint MEM_PRIVATE = 0x20000;
        public const uint MEM_MAPPED  = 0x40000;
        public const uint MEM_IMAGE   = 0x1000000;

        // Win32 PAGE_* protect bits
        public const uint PAGE_NOACCESS          = 0x0001;
        public const uint PAGE_READONLY          = 0x0002;
        public const uint PAGE_READWRITE         = 0x0004;
        public const uint PAGE_WRITECOPY         = 0x0008;
        public const uint PAGE_EXECUTE           = 0x0010;
        public const uint PAGE_EXECUTE_READ      = 0x0020;
        public const uint PAGE_EXECUTE_READWRITE = 0x0040;
        public const uint PAGE_EXECUTE_WRITECOPY = 0x0080;
        public const uint PAGE_GUARD             = 0x0100;
        public const uint PAGE_NOCACHE           = 0x0200;
        public const uint PAGE_WRITECOMBINE      = 0x0400;
    }
}
