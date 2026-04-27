// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Windows
{
    /// <summary>
    /// MINIDUMP_MISC_INFO_4 layout. Versions are progressive — a v1 dump
    /// stops after <see cref="ProcessKernelTime"/>; later fields are zero. The
    /// <see cref="Flags1"/> bit-mask indicates which fields the writer
    /// populated.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct MinidumpMiscInfo
    {
        // Flags1 bits
        public const uint MINIDUMP_MISC1_PROCESS_ID            = 0x00000001;
        public const uint MINIDUMP_MISC1_PROCESS_TIMES         = 0x00000002;
        public const uint MINIDUMP_MISC1_PROCESSOR_POWER_INFO  = 0x00000004;
        public const uint MINIDUMP_MISC3_PROCESS_INTEGRITY     = 0x00000010;
        public const uint MINIDUMP_MISC3_PROCESS_EXECUTE_FLAGS = 0x00000020;
        public const uint MINIDUMP_MISC3_TIMEZONE              = 0x00000040;
        public const uint MINIDUMP_MISC3_PROTECTED_PROCESS     = 0x00000080;
        public const uint MINIDUMP_MISC4_BUILDSTRING           = 0x00000100;
        public const uint MINIDUMP_MISC5_PROCESS_COOKIE        = 0x00000200;

        public uint SizeOfInfo;
        public uint Flags1;

        // MINIDUMP_MISC_INFO_1
        public uint ProcessId;
        public uint ProcessCreateTime;     // time_t (seconds since 1970-01-01 UTC)
        public uint ProcessUserTime;       // seconds
        public uint ProcessKernelTime;     // seconds

        // MINIDUMP_MISC_INFO_2 (processor power info)
        public uint ProcessorMaxMhz;
        public uint ProcessorCurrentMhz;
        public uint ProcessorMhzLimit;
        public uint ProcessorMaxIdleState;
        public uint ProcessorCurrentIdleState;

        // MINIDUMP_MISC_INFO_3
        public uint ProcessIntegrityLevel;
        public uint ProcessExecuteFlags;
        public uint ProtectedProcess;
        public uint TimeZoneId;
        // We do not unpack TIME_ZONE_INFORMATION (172 bytes) — not needed by
        // the IProcessInfoProvider surface. Skip past it via SizeOfInfo when
        // parsing v4+.

        // The v4 BuildString (520 wchar bytes) and DbgBldStr (80 wchar bytes)
        // are read directly from the stream by the parser using SizeOfInfo
        // and Flags1 to gate.
    }
}
