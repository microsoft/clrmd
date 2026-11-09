// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// A file locator that searches locally installed .NET runtime directories for DAC files.
    /// Matches the runtime's PE properties, ELF build ID, or Mach-O UUID without downloading binaries.
    /// </summary>
    internal sealed class InstalledRuntimeLocator : IFileLocator
    {
        public static InstalledRuntimeLocator Instance { get; } = new();

        public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            fileName = Path.GetFileName(fileName);
            foreach (string dir in EnumerateRuntimeDirectories())
            {
                string potentialFile = Path.Combine(dir, fileName);
                if (!File.Exists(potentialFile))
                    continue;

                try
                {
                    foreach (string indexedFile in new[] { potentialFile, Path.Combine(dir, "coreclr.dll"), Path.Combine(dir, "clr.dll") })
                    {
                        if (!File.Exists(indexedFile))
                            continue;

                        using PEImage image = new(File.OpenRead(indexedFile));
                        if (image.IsValid && image.IndexTimeStamp == buildTimeStamp && image.IndexFileSize == imageSize)
                            return potentialFile;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                }
            }

            return null;
        }

        public string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties)
        {
            if (buildIdOrUUID.IsDefaultOrEmpty || archivedUnder == SymbolProperties.None)
                return null;

            string runtimeModuleName;
            if (originalPlatform == OSPlatform.Linux)
                runtimeModuleName = "libcoreclr.so";
            else if (originalPlatform == OSPlatform.OSX)
                runtimeModuleName = "libcoreclr.dylib";
            else
                return null;

            fileName = Path.GetFileName(fileName);
            foreach (string dir in EnumerateRuntimeDirectories())
            {
                string potentialFile = Path.Combine(dir, fileName);
                string indexedFile = archivedUnder == SymbolProperties.Self ? potentialFile : Path.Combine(dir, runtimeModuleName);
                if (!File.Exists(potentialFile) || !File.Exists(indexedFile))
                    continue;

                try
                {
                    ImmutableArray<byte> buildId;
                    if (originalPlatform == OSPlatform.Linux)
                    {
                        using ElfFile elf = new(indexedFile);
                        buildId = elf.BuildId;
                    }
                    else
                    {
                        MachFile mach = new(indexedFile);
                        buildId = mach.BuildId;
                    }

                    if (!buildId.IsDefaultOrEmpty && buildId.SequenceEqual(buildIdOrUUID))
                        return potentialFile;
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (string dir in EnumerateFrameworkDirectories())
                    yield return dir;
            }
        }

        [SupportedOSPlatform("windows")]
        private static IEnumerable<string> EnumerateFrameworkDirectories()
        {
            string framework = Environment.Is64BitProcess ? "Framework64" : "Framework";
            string frameworkRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", framework);
            if (Directory.Exists(frameworkRoot))
                foreach (string dir in Directory.EnumerateDirectories(frameworkRoot))
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