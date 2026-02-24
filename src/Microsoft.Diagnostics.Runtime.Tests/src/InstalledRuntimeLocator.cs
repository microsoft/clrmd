// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// A file locator that searches locally installed .NET runtime directories for DAC files.
    /// Used by tests to find the DAC for single-file app dumps where the DAC isn't next to
    /// the CLR module.
    /// </summary>
    internal sealed class InstalledRuntimeLocator : IFileLocator
    {
        public static InstalledRuntimeLocator Instance { get; } = new();

        public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            string? sharedFrameworkDir = GetSharedFrameworkDir();
            if (sharedFrameworkDir is null)
                return null;

            foreach (string dir in Directory.EnumerateDirectories(sharedFrameworkDir))
            {
                string potentialClr = Path.Combine(dir, "coreclr.dll");
                if (!File.Exists(potentialClr))
                    continue;

                try
                {
                    if (PEMatchesProperties(potentialClr, buildTimeStamp, imageSize))
                    {
                        string potentialDac = Path.Combine(dir, fileName);
                        if (File.Exists(potentialDac))
                            return potentialDac;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            return null;
        }

        public string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            return null;
        }

        private static bool PEMatchesProperties(string path, int expectedTimeStamp, int expectedSize)
        {
            using FileStream fs = File.OpenRead(path);
            Span<byte> buffer = stackalloc byte[256];

            // Read DOS header to get PE offset
            if (fs.Read(buffer.Slice(0, 64)) < 64)
                return false;

            if (buffer[0] != 'M' || buffer[1] != 'Z')
                return false;

            int peOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(60));
            if (peOffset < 0 || peOffset > 1024)
                return false;

            fs.Position = peOffset;
            if (fs.Read(buffer.Slice(0, 8)) < 8)
                return false;

            // Verify PE signature
            if (buffer[0] != 'P' || buffer[1] != 'E' || buffer[2] != 0 || buffer[3] != 0)
                return false;

            // COFF header: TimeDateStamp is at offset 4, SizeOfImage is in optional header
            int timeStamp = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(8));

            // Read rest of COFF header (20 bytes total) + start of optional header
            fs.Position = peOffset + 4; // skip PE\0\0
            if (fs.Read(buffer.Slice(0, 24)) < 24)
                return false;

            int timeDateStamp = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4));
            int sizeOfOptionalHeader = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(16));

            if (sizeOfOptionalHeader < 60)
                return false;

            // Read optional header to get SizeOfImage (offset 56 in optional header)
            fs.Position = peOffset + 4 + 20; // PE sig + COFF header
            if (fs.Read(buffer.Slice(0, 60)) < 60)
                return false;

            int sizeOfImage = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(56));

            return timeDateStamp == expectedTimeStamp && sizeOfImage == expectedSize;
        }

        private static string? GetSharedFrameworkDir()
        {
            string? coreLibPath = typeof(object).Assembly.Location;
            if (string.IsNullOrEmpty(coreLibPath))
                return null;

            string? versionDir = Path.GetDirectoryName(coreLibPath);
            string? sharedDir = versionDir is null ? null : Path.GetDirectoryName(versionDir);
            if (sharedDir is null || !Directory.Exists(sharedDir))
                return null;

            return sharedDir;
        }
    }
}
