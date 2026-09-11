// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.MacOS;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MachOCoreBoundsTests
    {
        private const uint MachHeader64Magic = 0xfeedfacf;
        private const uint Arm64CpuType = 0x0100000c;
        private const uint MachOCoreFileType = 4;
        private const uint MachODylinkerFileType = 7;
        private const uint Segment64Command = 0x19;

        private const uint MachHeaderSize = 32;
        private const uint SegmentCommandSize = 72;
        private const ulong SegmentAddress = 0x1_0000_0000;
        private const ulong SegmentFileOffset = MachHeaderSize + SegmentCommandSize;
        private const ulong SegmentFileSize = 0x1000;
        private const ulong SegmentVmSize = 0x8000;

        [Fact]
        public void DylinkerScanDoesNotReadPastSegmentFileSize()
        {
            using MemoryStream stream = CreateCoreWithDylinkerHeaderPastSegmentFileSize();
            using MachOCoreReader reader = new("test.core", stream, leaveOpen: false);

            Assert.Empty(reader.EnumerateModules());
        }

        private static MemoryStream CreateCoreWithDylinkerHeaderPastSegmentFileSize()
        {
            MemoryStream stream = new();
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            WriteMachHeader(
                writer,
                fileType: MachOCoreFileType,
                commandCount: 1,
                commandBytes: SegmentCommandSize);

            writer.Write(Segment64Command);
            writer.Write(SegmentCommandSize);
            writer.Write(new byte[16]);
            writer.Write(SegmentAddress);
            writer.Write(SegmentVmSize);
            writer.Write(SegmentFileOffset);
            writer.Write(SegmentFileSize);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);

            writer.Write(new byte[checked((int)SegmentFileSize)]);

            WriteMachHeader(
                writer,
                fileType: MachODylinkerFileType,
                commandCount: uint.MaxValue,
                commandBytes: 0);

            stream.Position = 0;
            return stream;
        }

        private static void WriteMachHeader(
            BinaryWriter writer,
            uint fileType,
            uint commandCount,
            uint commandBytes)
        {
            writer.Write(MachHeader64Magic);
            writer.Write(Arm64CpuType);
            writer.Write(0u);
            writer.Write(fileType);
            writer.Write(commandCount);
            writer.Write(commandBytes);
            writer.Write(0u);
            writer.Write(0u);
        }
    }
}
