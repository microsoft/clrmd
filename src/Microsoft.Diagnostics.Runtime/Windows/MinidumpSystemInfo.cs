// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// Mirrors MINIDUMP_SYSTEM_INFO up to (but not including) the trailing
    /// <c>CPU_INFORMATION</c> union. The union is 24 bytes (the larger of
    /// X86CpuInfo's 6 ULONG32 fields vs. OtherCpuInfo's 2 ULONG64 fields),
    /// bringing the full struct to 56 bytes. We do not currently surface the
    /// CPU info; the parser uses sizeof(MinidumpSystemInfo) and skips past
    /// the union when needed.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct MinidumpSystemInfo
    {
        public readonly ushort ProcessorArchitecture;
        public readonly ushort ProcessorLevel;
        public readonly ushort ProcessorRevision;
        public readonly byte NumberOfProcessors;
        public readonly byte ProductType;
        public readonly uint MajorVersion;
        public readonly uint MinorVersion;
        public readonly uint BuildNumber;
        public readonly uint PlatformId;
        public readonly uint CSDVersionRva;
        public readonly ushort SuiteMask;
        public readonly ushort Reserved2;
        // Followed in MINIDUMP_SYSTEM_INFO by a 24-byte CPU_INFORMATION union
        // (see struct doc comment) which we do not currently surface.
    }
}
