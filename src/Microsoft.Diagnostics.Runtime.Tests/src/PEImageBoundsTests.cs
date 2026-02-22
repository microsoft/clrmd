// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PEImageBoundsTests : IDisposable
    {
        private const ushort MZMagic = 0x5A4D;
        private const uint PESignature = 0x00004550;
        private const int PESignatureOffsetLocation = 0x3C;
        private const int PEHeaderStart = 0x80;

        private readonly string _tempFile;

        public PEImageBoundsTests()
        {
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            try { File.Delete(_tempFile); } catch { }
        }

        [Fact]
        public void RejectsExcessiveSectionCount()
        {
            // A PE with NumberOfSections = ushort.MaxValue should be rejected.
            WritePEWithSectionCount(_tempFile, ushort.MaxValue);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            {
                using PEImage image = new(new FileStream(_tempFile, FileMode.Open, FileAccess.Read));
            });
            Assert.Contains("sections", ex.Message);
            Assert.Contains("exceeds the maximum", ex.Message);
        }

        [Fact]
        public void AcceptsReasonableSectionCount()
        {
            // A PE with a small section count should not trigger the section cap.
            WritePEWithSectionCount(_tempFile, 4);
            using PEImage image = new(new FileStream(_tempFile, FileMode.Open, FileAccess.Read));
            Assert.True(image.IsValid);
        }

        private static void WritePEWithSectionCount(string path, ushort sectionCount)
        {
            using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
            using BinaryWriter bw = new(fs);

            // DOS header stub: only MZ magic and PE signature offset matter.
            bw.Write(MZMagic);

            // Pad to PESignatureOffsetLocation (0x3C).
            fs.Position = PESignatureOffsetLocation;
            bw.Write(PEHeaderStart);

            // Pad to PE header start.
            fs.Position = PEHeaderStart;
            bw.Write(PESignature);

            // ImageFileHeader
            bw.Write((ushort)0x8664);    // Machine = AMD64
            bw.Write(sectionCount);       // NumberOfSections
            bw.Write(0);                  // TimeDateStamp
            bw.Write((uint)0);            // PointerToSymbolTable
            bw.Write((uint)0);            // NumberOfSymbols
            bw.Write((ushort)0);          // SizeOfOptionalHeader
            bw.Write((ushort)0);          // Characteristics

            bw.Flush();
        }
    }
}
