// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

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
            if (buildIdOrUUID.IsDefaultOrEmpty)
                return null;

            string runtimeModuleName;
            if (originalPlatform == OSPlatform.Linux)
                runtimeModuleName = "libcoreclr.so";
            else if (originalPlatform == OSPlatform.OSX)
                runtimeModuleName = "libcoreclr.dylib";
            else
                return null;

            foreach (string dir in EnumerateRuntimeDirectories())
            {
                string potentialClr = Path.Combine(dir, runtimeModuleName);
                if (!File.Exists(potentialClr))
                    continue;

                try
                {
                    using ElfFile elf = new(potentialClr);
                    if (!elf.BuildId.IsDefaultOrEmpty && elf.BuildId.SequenceEqual(buildIdOrUUID))
                    {
                        string potentialDac = Path.Combine(dir, fileName);
                        if (File.Exists(potentialDac))
                            return potentialDac;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateRuntimeDirectories()
        {
            string? sharedFrameworkDir = GetSharedFrameworkDir();
            if (sharedFrameworkDir is not null)
            {
                foreach (string dir in Directory.EnumerateDirectories(sharedFrameworkDir))
                    yield return dir;
            }

            foreach (string dir in EnumerateNuGetRuntimePackDirectories())
                yield return dir;
        }

        private static IEnumerable<string> EnumerateNuGetRuntimePackDirectories()
        {
            string? nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(nugetPackages))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(home))
                    yield break;

                nugetPackages = Path.Combine(home, ".nuget", "packages");
            }

            if (!Directory.Exists(nugetPackages))
                yield break;

            string portableRid = GetPortableRid();

            foreach (string rid in new[] { portableRid, RuntimeInformation.RuntimeIdentifier })
            {
                string runtimePackDir = Path.Combine(nugetPackages, $"microsoft.netcore.app.runtime.{rid}");
                if (!Directory.Exists(runtimePackDir))
                    continue;

                string[] versions;
                try
                {
                    versions = Directory.GetDirectories(runtimePackDir);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string versionDir in versions)
                {
                    string nativeDir = Path.Combine(versionDir, "runtimes", rid, "native");
                    if (Directory.Exists(nativeDir))
                        yield return nativeDir;
                }
            }
        }

        private static string GetPortableRid()
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                os = "linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                os = "osx";
            else
                os = "win";

            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
            };

            return $"{os}-{arch}";
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
