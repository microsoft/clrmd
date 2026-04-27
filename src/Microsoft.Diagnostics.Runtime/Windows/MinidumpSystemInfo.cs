// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// Mirrors MINIDUMP_SYSTEM_INFO up to (but not including) the CPU info
    /// union. The CPU info union is 56 bytes that we currently do not surface
    /// but the parser reads past it via the documented size when needed.
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
        // MINIDUMP_SYSTEM_INFO continues with a 32-byte Cpu union here, which
        // we do not currently surface. The parser uses sizeof to skip it.
    }
}
