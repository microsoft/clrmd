// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MinidumpBoundsTests
    {
        // Minidump magic values
        private const uint Magic1 = 0x504d444d;
        private const ushort Magic2 = 0xa793;

        [Fact]
        public void RejectsExcessiveStreamCount()
        {
            // A minidump header claiming int.MaxValue streams should be rejected.
            using MemoryStream stream = CreateMinidumpWithStreamCount(uint.MaxValue);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() => DataTarget.LoadDump("test.dmp", stream));
            Assert.Contains("streams", ex.Message);
            Assert.Contains("exceeds the maximum", ex.Message);
        }

        [Fact]
        public void RejectsExcessiveModuleCount()
        {
            // Build a minidump with valid header + directories but absurd module count.
            using MemoryStream stream = CreateMinidumpWithModuleCount(uint.MaxValue);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() => DataTarget.LoadDump("test.dmp", stream));
            Assert.Contains("modules", ex.Message);
            Assert.Contains("exceeds the maximum", ex.Message);
        }

        [Fact]
        public void AcceptsReasonableStreamCount()
        {
            // A minidump with a reasonable stream count should not throw due to stream count limits.
            // It will likely fail for other reasons (missing data), but not due to the stream cap.
            using MemoryStream stream = CreateMinidumpWithStreamCount(3);

            // We expect some error because our minimal dump is incomplete, but NOT the stream count error.
            try
            {
                using DataTarget dt = DataTarget.LoadDump("test.dmp", stream);
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("streams") && ex.Message.Contains("exceeds"))
            {
                Assert.Fail("Should not reject a dump with only 3 streams as exceeding the stream limit.");
            }
            catch
            {
                // Other errors are expected for an incomplete dump — that's fine.
            }
        }

        private static MemoryStream CreateMinidumpWithStreamCount(uint streamCount)
        {
            MemoryStream ms = new();
            BinaryWriter bw = new(ms);

            // MinidumpHeader: Magic1(4) + Magic2(2) + Version(2) + NumberOfStreams(4) + StreamDirectoryRva(4) + CheckSum(4) + TimeDateStamp(4) + Flags(8)
            bw.Write(Magic1);        // Magic1
            bw.Write(Magic2);        // Magic2
            bw.Write((ushort)0);     // Version
            bw.Write(streamCount);   // NumberOfStreams
            bw.Write((uint)32);      // StreamDirectoryRva (right after header)
            bw.Write((uint)0);       // CheckSum
            bw.Write((uint)0);       // TimeDateStamp
            bw.Write((ulong)0);      // Flags

            bw.Flush();
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateMinidumpWithModuleCount(uint moduleCount)
        {
            // We need: header + 3 directory entries (SystemInfo, ModuleList, MemoryList)
            // plus SystemInfo data and the module-list count.
            MemoryStream ms = new();
            BinaryWriter bw = new(ms);

            uint headerSize = 32; // MinidumpHeader
            uint directoryEntrySize = 12; // MinidumpDirectory: StreamType(4) + DataSize(4) + Rva(4)
            uint numStreams = 3;
            uint directoryRva = headerSize;
            uint directorySize = numStreams * directoryEntrySize;
            uint systemInfoRva = directoryRva + directorySize;
            // MINIDUMP_SYSTEM_INFO starts with ProcessorArchitecture (ushort). Amd64 = 9.
            uint systemInfoSize = 2;
            uint moduleListRva = systemInfoRva + systemInfoSize;

            // Header
            bw.Write(Magic1);
            bw.Write(Magic2);
            bw.Write((ushort)0);      // Version
            bw.Write(numStreams);      // NumberOfStreams
            bw.Write(directoryRva);    // StreamDirectoryRva
            bw.Write((uint)0);        // CheckSum
            bw.Write((uint)0);        // TimeDateStamp
            bw.Write((ulong)0);       // Flags

            // Directory[0]: SystemInfoStream (type=7)
            bw.Write((uint)7);        // StreamType = SystemInfoStream
            bw.Write(systemInfoSize);  // DataSize
            bw.Write(systemInfoRva);   // Rva

            // Directory[1]: ModuleListStream (type=4)
            bw.Write((uint)4);        // StreamType = ModuleListStream
            bw.Write((uint)4);        // DataSize (just the count uint)
            bw.Write(moduleListRva);   // Rva

            // Directory[2]: MemoryListStream (type=5) — required to avoid other issues
            bw.Write((uint)5);        // StreamType = MemoryListStream
            bw.Write((uint)4);
            bw.Write(moduleListRva + 4); // Rva (after module count, we'll put 0 memory ranges)

            // SystemInfo: ProcessorArchitecture = Amd64 (9)
            bw.Write((ushort)9);

            // Module list: count = moduleCount (absurdly high)
            bw.Write(moduleCount);

            // Memory list: 0 ranges
            bw.Write((uint)0);

            bw.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
