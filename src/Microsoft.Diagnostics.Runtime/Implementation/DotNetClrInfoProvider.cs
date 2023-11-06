// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacImplementation;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal class DotNetClrInfoProvider : IClrInfoProvider
    {
        private const string c_desktopModuleName = "clr.dll";
        private const string c_coreModuleName = "coreclr.dll";
        private const string c_linuxCoreModuleName = "libcoreclr.so";
        private const string c_macOSCoreModuleName = "libcoreclr.dylib";

        private const string c_desktopDacFileNameBase = "mscordacwks";
        private const string c_coreDacFileNameBase = "mscordaccore";
        private const string c_desktopDacFileName = c_desktopDacFileNameBase + ".dll";
        private const string c_coreDacFileName = c_coreDacFileNameBase + ".dll";
        private const string c_linuxCoreDacFileName = "libmscordaccore.so";
        private const string c_macOSCoreDacFileName = "libmscordaccore.dylib";

        private const string c_windowsDbiFileName = "mscordbi.dll";
        private const string c_linuxCoreDbiFileName = "libmscordbi.so";
        private const string c_macOSCoreDbiFileName = "libmscordbi.dylib";

        public IServiceProvider GetDacServices(ClrInfo clrInfo, string? providedPath, bool ignoreMismatch)
        {
            DacLibrary library = GetDacLibraryFromPath(clrInfo, providedPath, ignoreMismatch);
            return new DacServiceProvider(clrInfo, library);
        }

        private static DacLibrary GetDacLibraryFromPath(ClrInfo clrInfo, string? dacPath, bool ignoreMismatch)
        {
            OSPlatform currentPlatform = GetCurrentPlatform();
            Architecture currentArch = RuntimeInformation.ProcessArchitecture;

            if (dacPath is not null)
                return CreateDacFromPath(clrInfo, dacPath, ignoreMismatch);

            bool foundOne = false;
            Exception? exception = null;

            IFileLocator? locator = clrInfo.DataTarget.FileLocator;

            foreach (DebugLibraryInfo dac in clrInfo.DebuggingLibraries.Where(r => r.Kind == DebugLibraryKind.Dac && r.Platform == currentPlatform && r.TargetArchitecture == currentArch))
            {
                foundOne = true;

                // If we have a full path, use it.  We already validated that the CLR matches.
                if (Path.GetFileName(dac.FileName) != dac.FileName)
                {
                    dacPath = dac.FileName;
                }
                else
                {
                    // The properties we are requesting under may not be the actual file properties, so don't request them.

                    if (locator != null)
                    {
                        if (!dac.IndexBuildId.IsDefaultOrEmpty)
                        {
                            if (dac.Platform == OSPlatform.Windows)
                                dacPath = locator.FindPEImage(dac.FileName, SymbolProperties.Coreclr, dac.IndexBuildId, clrInfo.DataTarget.DataReader.TargetPlatform, checkProperties: false);
                            else if (dac.Platform == OSPlatform.Linux)
                                dacPath = locator.FindElfImage(dac.FileName, SymbolProperties.Coreclr, dac.IndexBuildId, checkProperties: false);
                            else if (dac.Platform == OSPlatform.OSX)
                                dacPath = locator.FindMachOImage(dac.FileName, SymbolProperties.Coreclr, dac.IndexBuildId, checkProperties: false);
                        }
                        else if (dac.IndexTimeStamp != 0 && dac.IndexFileSize != 0)
                        {
                            if (dac.Platform == OSPlatform.Windows)
                                dacPath = clrInfo.DataTarget.FileLocator?.FindPEImage(dac.FileName, dac.IndexTimeStamp, dac.IndexFileSize, checkProperties: false);
                        }
                    }
                }

                if (dacPath is not null && File.Exists(dacPath))
                {
                    try
                    {
                        // If we get the file from the symbol server, assume mismatches are expected.  Sometimes we replace dacs on the symbol
                        // server to fix bugs.  If it's archived under the right path, use it.
                        return CreateDacFromPath(clrInfo, dacPath, ignoreMismatch: true);
                    }
                    catch (Exception ex)
                    {
                        exception ??= ex;
                        dacPath = null;
                    }
                }
            }

            if (exception is not null)
                throw exception;

            // We should have had at least one dac enumerated if this is a supported scenario.
            if (!foundOne)
                throw new InvalidOperationException($"Debugging a '{clrInfo.DataTarget.DataReader.TargetPlatform}' crash is not supported on '{currentPlatform}'.");

            throw new FileNotFoundException("Could not find matching DAC for this runtime.");
        }

        private static DacLibrary CreateDacFromPath(ClrInfo clrInfo, string dacPath, bool ignoreMismatch)
        {
            if (!File.Exists(dacPath))
                throw new FileNotFoundException(dacPath);

            if (!ignoreMismatch && !clrInfo.IsSingleFile)
            {
                DataTarget.PlatformFunctions.GetFileVersion(dacPath, out int major, out int minor, out int revision, out int patch);
                if (major != clrInfo.Version.Major || minor != clrInfo.Version.Minor || revision != clrInfo.Version.Build || patch != clrInfo.Version.Revision)
                    throw new ClrDiagnosticsException($"Mismatched dac. Dac version: {major}.{minor}.{revision}.{patch}, expected: {clrInfo.Version}.");
            }

            return new(clrInfo.DataTarget, dacPath, clrInfo.ModuleInfo.ImageBase);
        }

        public virtual ClrInfo? ProvideClrInfoForModule(DataTarget dataTarget, ModuleInfo module)
        {
            ulong runtimeInfo = 0;
            if (IsSupportedRuntime(module, out ClrFlavor flavor))
                return CreateClrInfo(dataTarget, module, runtimeInfo, flavor);

            return null;
        }

        protected ClrInfo CreateClrInfo(DataTarget dataTarget, ModuleInfo module, ulong runtimeInfo, ClrFlavor flavor)
        {
            Version version;
            int indexTimeStamp = 0;
            int indexFileSize = 0;
            ImmutableArray<byte> buildId = ImmutableArray<byte>.Empty;
            List<DebugLibraryInfo> artifacts = new(8);

            OSPlatform currentPlatform = GetCurrentPlatform();
            OSPlatform targetPlatform = dataTarget.DataReader.TargetPlatform;
            Architecture currentArch = RuntimeInformation.ProcessArchitecture;
            Architecture targetArch = dataTarget.DataReader.Architecture;

            string? dacCurrentPlatform = GetDacFileName(flavor, currentPlatform);
            string? dacTargetPlatform = GetDacFileName(flavor, targetPlatform);
            string? dbiCurrentPlatform = GetDbiFileName(flavor, currentPlatform);
            string? dbiTargetPlatform = GetDbiFileName(flavor, targetPlatform);
            if (runtimeInfo != 0)
            {
                if (ClrRuntimeInfo.TryReadClrRuntimeInfo(dataTarget.DataReader, runtimeInfo, out ClrRuntimeInfo info, out version))
                {
                    if (dataTarget.DataReader.TargetPlatform == OSPlatform.Windows)
                    {
                        indexTimeStamp = info.RuntimePEProperties.TimeStamp;
                        indexFileSize = info.RuntimePEProperties.FileSize;

                        if (dacTargetPlatform is not null)
                        {
                            (int timeStamp, int fileSize) = info.DacPEProperties;
                            if (timeStamp != 0 && fileSize != 0)
                                artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacTargetPlatform, targetArch, SymbolProperties.Self, fileSize, timeStamp));
                        }

                        if (dbiTargetPlatform is not null)
                        {
                            (int timeStamp, int fileSize) = info.DbiPEProperties;
                            if (timeStamp != 0 && fileSize != 0)
                                artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiTargetPlatform, targetArch, SymbolProperties.Self, fileSize, timeStamp));
                        }
                    }
                    else
                    {
                        buildId = info.RuntimeBuildId;

                        if (dacTargetPlatform is not null)
                        {
                            ImmutableArray<byte> dacBuild = info.DacBuildId;
                            if (!dacBuild.IsDefaultOrEmpty)
                                artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacTargetPlatform, targetArch, targetPlatform, SymbolProperties.Self, dacBuild));
                        }

                        if (dbiTargetPlatform is not null)
                        {
                            ImmutableArray<byte> dbiBuild = info.DbiBuildId;
                            if (!dbiBuild.IsDefaultOrEmpty)
                                artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiTargetPlatform, targetArch, targetPlatform, SymbolProperties.Self, dbiBuild));
                        }
                    }
                }
            }
            else
            {
                indexTimeStamp = module.IndexTimeStamp;
                indexFileSize = module.IndexFileSize;
                buildId = module.BuildId;
                version = module.Version;
            }

            // Long-name dac
            if (dataTarget.DataReader.TargetPlatform == OSPlatform.Windows && version.Major != 0)
                artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, GetWindowsLongNameDac(flavor, currentArch, targetArch, version), currentArch, SymbolProperties.Coreclr, indexFileSize, indexTimeStamp));

            // Short-name dac under CLR's properties
            if (targetPlatform == currentPlatform)
            {
                // We are debugging the process on the same operating system.
                if (dacCurrentPlatform is not null)
                {
                    bool foundLocalDac = false;

                    // Check if the user has the same CLR installed locally, and if so
                    string? directory = Path.GetDirectoryName(module.FileName);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        string potentialClr = Path.Combine(directory, Path.GetFileName(module.FileName));
                        if (File.Exists(potentialClr))
                        {
                            try
                            {
                                using PEImage peimage = new(File.OpenRead(potentialClr));
                                if (peimage.IndexFileSize == indexFileSize && peimage.IndexTimeStamp == indexTimeStamp)
                                {
                                    string dacFound = Path.Combine(directory, dacCurrentPlatform);
                                    if (File.Exists(dacFound))
                                    {
                                        dacCurrentPlatform = dacFound;
                                        foundLocalDac = true;
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }

                    if (indexFileSize != 0 && indexTimeStamp != 0)
                    {
                        DebugLibraryInfo dacLibraryInfo = new(DebugLibraryKind.Dac, dacCurrentPlatform, targetArch, SymbolProperties.Coreclr, indexFileSize, indexTimeStamp);
                        if (foundLocalDac)
                            artifacts.Insert(0, dacLibraryInfo);
                        else
                            artifacts.Add(dacLibraryInfo);
                    }

                    if (!buildId.IsDefaultOrEmpty)
                    {
                        DebugLibraryInfo dacLibraryInfo = new(DebugLibraryKind.Dac, dacCurrentPlatform, targetArch, targetPlatform, SymbolProperties.Coreclr, buildId);
                        if (foundLocalDac)
                            artifacts.Insert(0, dacLibraryInfo);
                        else
                            artifacts.Add(dacLibraryInfo);
                    }
                }

                if (dbiCurrentPlatform is not null)
                {
                    if (indexFileSize != 0 && indexTimeStamp != 0)
                    {
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiCurrentPlatform, targetArch, SymbolProperties.Coreclr, indexFileSize, indexTimeStamp));
                    }

                    if (!buildId.IsDefaultOrEmpty)
                    {
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiCurrentPlatform, targetArch, targetPlatform, SymbolProperties.Coreclr, buildId));
                    }
                }
            }
            else
            {
                // We are debugging the process on a different operating system.
                if (indexFileSize != 0 && indexTimeStamp != 0)
                {
                    // We currently only support cross-os debugging on windows targeting linux or os x runtimes.  So if we have windows properties,
                    // then we only generate one artifact (the target one).
                    if (dacTargetPlatform is not null)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacTargetPlatform, targetArch, SymbolProperties.Coreclr, indexFileSize, indexTimeStamp));

                    if (dbiTargetPlatform is not null)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiTargetPlatform, targetArch, SymbolProperties.Coreclr, indexFileSize, indexTimeStamp));
                }

                if (!buildId.IsDefaultOrEmpty)
                {
                    if (dacTargetPlatform is not null)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacTargetPlatform, targetArch, targetPlatform, SymbolProperties.Coreclr, buildId));

                    if (dbiTargetPlatform is not null)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiTargetPlatform, targetArch, targetPlatform, SymbolProperties.Coreclr, buildId));

                    if (currentPlatform == OSPlatform.Windows)
                    {
                        // If we are running from Windows, we can target Linux and OS X dumps. We do build cross-os, cross-architecture debug libraries to run on Windows x64 or x86
                        if (dacCurrentPlatform is not null)
                            artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacCurrentPlatform, currentArch, currentPlatform, SymbolProperties.Coreclr, buildId));

                        if (dbiCurrentPlatform is not null)
                            artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiCurrentPlatform, currentArch, currentPlatform, SymbolProperties.Coreclr, buildId));
                    }
                }
            }

            // Windows CLRDEBUGINFO resource
            IResourceNode? resourceNode = module.ResourceRoot?.GetChild("RCData")?.GetChild("CLRDEBUGINFO")?.Children.FirstOrDefault();
            if (resourceNode is not null)
            {
                CLR_DEBUG_RESOURCE resource = resourceNode.Read<CLR_DEBUG_RESOURCE>(0);
                if (resource.dwVersion == 0)
                {
                    if (dacTargetPlatform is not null && resource.dwDacTimeStamp != 0 && resource.dwDacSizeOfImage != 0)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dac, dacTargetPlatform, targetArch, SymbolProperties.Self, resource.dwDacSizeOfImage, resource.dwDacTimeStamp));

                    if (dbiTargetPlatform is not null && resource.dwDbiTimeStamp != 0 && resource.dwDbiSizeOfImage != 0)
                        artifacts.Add(new DebugLibraryInfo(DebugLibraryKind.Dbi, dbiTargetPlatform, targetArch, SymbolProperties.Self, resource.dwDbiSizeOfImage, resource.dwDbiTimeStamp));
                }
            }

            // Do NOT take a dependency on the order of enumerated libraries.  I reserve the right to change this at any time.
            IOrderedEnumerable<DebugLibraryInfo> orderedDebugLibraries = from artifact in EnumerateUnique(artifacts)
                                                                         orderby artifact.Kind,
                                                                                 Path.GetFileName(artifact.FileName) == artifact.FileName, // if we have a full local path, put it first
                                                                                 artifact.ArchivedUnder
                                                                         select artifact;

            ClrInfo result = new(dataTarget, module, version, this)
            {
                Flavor = flavor,
                DebuggingLibraries = orderedDebugLibraries.ToImmutableArray(),
                IndexFileSize = indexFileSize,
                IndexTimeStamp = indexTimeStamp,
                BuildId = buildId,
            };

            return result;
        }

        private static IEnumerable<DebugLibraryInfo> EnumerateUnique(List<DebugLibraryInfo> artifacts)
        {
            HashSet<DebugLibraryInfo> seen = new();

            foreach (DebugLibraryInfo library in artifacts)
                if (seen.Add(library))
                    yield return library;
        }

        private static string GetWindowsLongNameDac(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, Version version)
        {
            string dacNameBase = flavor == ClrFlavor.Core ? c_coreDacFileNameBase : c_desktopDacFileNameBase;
            return $"{dacNameBase}_{ArchitectureToName(currentArchitecture)}_{ArchitectureToName(targetArchitecture)}_{version.Major}.{version.Minor}.{version.Build}.{version.Revision:D2}.dll".ToLowerInvariant();
        }

        private static string ArchitectureToName(Architecture arch)
        {
            return arch switch
            {
                Architecture.X64 => "amd64",
                _ => arch.ToString()
            };
        }

        private static string? GetDbiFileName(ClrFlavor flavor, OSPlatform targetPlatform)
        {
            if (flavor == ClrFlavor.Core)
            {
                if (targetPlatform == OSPlatform.Windows)
                    return c_windowsDbiFileName;
                else if (targetPlatform == OSPlatform.Linux)
                    return c_linuxCoreDbiFileName;
                else if (targetPlatform == OSPlatform.OSX)
                    return c_macOSCoreDbiFileName;
            }

            if (flavor == ClrFlavor.Desktop)
            {
                if (targetPlatform == OSPlatform.Windows)
                    return c_windowsDbiFileName;
            }

            return null;
        }

        private static string? GetDacFileName(ClrFlavor flavor, OSPlatform targetPlatform)
        {
            if (flavor == ClrFlavor.Core)
            {
                if (targetPlatform == OSPlatform.Windows)
                    return c_coreDacFileName;
                else if (targetPlatform == OSPlatform.Linux)
                    return c_linuxCoreDacFileName;
                else if (targetPlatform == OSPlatform.OSX)
                    return c_macOSCoreDacFileName;
            }

            if (flavor == ClrFlavor.Desktop)
            {
                if (targetPlatform == OSPlatform.Windows)
                    return c_desktopDacFileName;
            }

            return null;
        }

        private static bool IsSupportedRuntime(ModuleInfo module, out ClrFlavor flavor)
        {
            flavor = default;

            string moduleName = Path.GetFileName(module.FileName);
            if (moduleName.Equals(c_desktopModuleName, StringComparison.OrdinalIgnoreCase))
            {
                flavor = ClrFlavor.Desktop;
                return true;
            }

            if (moduleName.Equals(c_coreModuleName, StringComparison.OrdinalIgnoreCase))
            {
                flavor = ClrFlavor.Core;
                return true;
            }

            if (moduleName.Equals(c_macOSCoreModuleName, StringComparison.OrdinalIgnoreCase))
            {
                flavor = ClrFlavor.Core;
                return true;
            }

            if (moduleName.Equals(c_linuxCoreModuleName, StringComparison.Ordinal))
            {
                flavor = ClrFlavor.Core;
                return true;
            }

            return false;
        }
        private static OSPlatform GetCurrentPlatform()
        {
            OSPlatform currentPlatform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                currentPlatform = OSPlatform.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                currentPlatform = OSPlatform.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                currentPlatform = OSPlatform.OSX;
            else
                throw new PlatformNotSupportedException();
            return currentPlatform;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CLR_DEBUG_RESOURCE
        {
            public uint dwVersion;
            public Guid signature;
            public int dwDacTimeStamp;
            public int dwDacSizeOfImage;
            public int dwDbiTimeStamp;
            public int dwDbiSizeOfImage;
        }
    }
}