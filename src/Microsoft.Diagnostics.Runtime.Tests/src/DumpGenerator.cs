// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Generates crash dumps from test target executables. Handles both .NET Core
    /// (via DOTNET_DbgEnableMiniDump env vars) and .NET Framework (via DbgEng) paths.
    /// Builds test targets for specific architectures on demand.
    /// </summary>
    internal static class DumpGenerator
    {
        private static readonly object _lock = new();

        /// <summary>
        /// Ensures a dump file exists for the given test target. If the dump doesn't exist,
        /// builds the target and generates the dump.
        /// </summary>
        /// <summary>
        /// Target Framework Moniker used for .NET Core test targets. Kept in sync with
        /// TestTargets project TargetFramework.
        /// </summary>
        public const string CoreTfm = "net10.0";

        /// <summary>
        /// Target Framework Moniker used for .NET Framework test targets.
        /// </summary>
        public const string FrameworkTfm = "net48";

        /// <summary>
        /// Selects the TFM used for a given flavor.
        /// </summary>
        public static string GetTfm(bool isFramework) => isFramework ? FrameworkTfm : CoreTfm;

        /// <summary>
        /// Returns the per-TFM output directory beneath the shared architecture bin
        /// directory. Each TFM gets its own subdirectory so .NET Core and .NET Framework
        /// builds of the same target don't overwrite each other.
        /// </summary>
        public static string GetBuildOutputDir(string binDir, bool isFramework)
            => Path.Combine(binDir, GetTfm(isFramework));

        public static void EnsureDump(string name, string projectDir, string dumpPath, string architecture, bool isFramework, GCMode gcMode, bool full, bool singleFile = false, bool highBit = false, string[] companionTargets = null)
        {
            if (File.Exists(dumpPath))
                return;

            lock (_lock)
            {
                // Double-check after acquiring lock
                if (File.Exists(dumpPath))
                    return;

                string outputDir = Path.GetDirectoryName(dumpPath);
                Directory.CreateDirectory(outputDir);

                // Build outputs live in a per-TFM subdirectory so Core and Framework
                // builds of the same target don't overwrite each other's binaries.
                string buildDir = singleFile ? outputDir : GetBuildOutputDir(outputDir, isFramework);
                if (!singleFile)
                    Directory.CreateDirectory(buildDir);

                string exePath;
                if (singleFile)
                {
                    exePath = PublishSingleFile(name, projectDir, outputDir, architecture);
                }
                else
                {
                    exePath = BuildTarget(name, projectDir, buildDir, architecture, isFramework, companionTargets);
                }

                string processOutput = null;
                if (highBit && isFramework)
                {
                    GenerateHighBitFrameworkDump(exePath, dumpPath, gcMode, full, architecture);
                }
                else if (highBit)
                {
                    processOutput = GenerateHighBitCoreDump(exePath, dumpPath, gcMode, full, architecture);
                }
                else if (isFramework && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    GenerateFrameworkDump(exePath, dumpPath, gcMode, full);
                }
                else if (singleFile && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Single-file self-contained apps don't include createdump, so
                    // DOTNET_DbgEnableMiniDump won't work. Use DbgEng instead.
                    GenerateDbgEngDump(exePath, dumpPath, gcMode, full);
                }
                else
                {
                    processOutput = GenerateCoreDump(exePath, dumpPath, gcMode, full, singleFile);
                }

                if (!File.Exists(dumpPath))
                {
                    string msg = $"Failed to generate dump: {dumpPath}";
                    if (singleFile)
                        msg += $"\n  exePath: {exePath}\n  exists: {File.Exists(exePath)}";
                    if (processOutput != null)
                        msg += $"\n  process: {processOutput}";
                    throw new InvalidOperationException(msg);
                }
            }
        }

        /// <summary>
        /// Builds a test target for a specific architecture and framework.
        /// Returns the path to the built executable.
        /// </summary>
        private static string BuildTarget(string name, string projectDir, string outputDir, string architecture, bool isFramework, string[] companionTargets = null)
        {
            string csprojPath = Path.Combine(projectDir, name + ".csproj");
            if (!File.Exists(csprojPath))
                throw new FileNotFoundException($"Could not find project file: {csprojPath}");

            string tfm = isFramework ? "net48" : "net10.0";
            string platform = architecture == "x86" ? "x86" : "x64";

            // Build the project (including its dependencies like SharedLibrary).
            // Don't use -o to avoid file locking between projects sharing an output dir.
            RunDotnetBuild(csprojPath, tfm, platform);

            // Find the build output in the project's own bin directory
            string projectBinDir = Path.Combine(projectDir, "bin", "Debug", tfm);
            Directory.CreateDirectory(outputDir);

            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + ".exe" : name;
            CopyBuildOutput(name, projectBinDir, outputDir);

            // Build and copy companion targets (e.g., NestedException for AppDomains)
            if (companionTargets is { Length: > 0 })
            {
                string testTargetsRoot = Path.GetDirectoryName(projectDir)!;
                foreach (string companion in companionTargets)
                {
                    string companionProjectDir = Path.Combine(testTargetsRoot, companion);
                    string companionCsproj = Path.Combine(companionProjectDir, companion + ".csproj");
                    if (!File.Exists(companionCsproj))
                        throw new FileNotFoundException($"Could not find companion project file: {companionCsproj}");

                    RunDotnetBuild(companionCsproj, tfm, platform);

                    string companionBinDir = Path.Combine(companionProjectDir, "bin", "Debug", tfm);
                    CopyBuildOutput(companion, companionBinDir, outputDir);
                }
            }

            string destExe = Path.Combine(outputDir, exeName);
            if (File.Exists(destExe))
                return destExe;

            // .NET Core on non-Windows may produce only a DLL
            string destDll = Path.Combine(outputDir, name + ".dll");
            if (File.Exists(destDll))
                return destDll;

            throw new InvalidOperationException($"Build succeeded but could not find output for {name} in {projectBinDir}");
        }

        /// <summary>
        /// Publishes a test target as a single-file executable.
        /// Returns the path to the published executable.
        /// </summary>
        private static string PublishSingleFile(string name, string projectDir, string outputDir, string architecture)
        {
            string csprojPath = Path.Combine(projectDir, name + ".csproj");
            if (!File.Exists(csprojPath))
                throw new FileNotFoundException($"Could not find project file: {csprojPath}");

            string rid = GetRuntimeIdentifier(architecture);
            string publishDir = Path.Combine(outputDir, "singlefile");
            Directory.CreateDirectory(publishDir);

            RunDotnetPublish(csprojPath, "net10.0", rid, publishDir);

            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + ".exe" : name;
            string publishedExe = Path.Combine(publishDir, exeName);

            if (File.Exists(publishedExe))
                return publishedExe;

            throw new InvalidOperationException($"Single-file publish succeeded but could not find output for {name} in {publishDir}");
        }

        /// <summary>
        /// Returns the RID for the given architecture on the current OS.
        /// </summary>
        private static string GetRuntimeIdentifier(string architecture)
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                os = "win";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                os = "osx";
            else
                os = "linux";

            return $"{os}-{architecture}";
        }

        private static void CopyFileIfNewer(string src, string dest)
        {
            if (File.Exists(dest) && File.GetLastWriteTimeUtc(src) <= File.GetLastWriteTimeUtc(dest))
                return;

            // Retry with backoff to handle transient file locks from dump generation
            // child processes that haven't fully exited yet.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Copy(src, dest, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 3)
                {
                    System.Threading.Thread.Sleep(100 * (attempt + 1));
                }
            }
        }

        /// <summary>
        /// Copies the build output (exe, dll, pdb, runtimeconfig, deps, SharedLibrary) for a project
        /// from its bin directory to the shared output directory.
        /// </summary>
        private static void CopyBuildOutput(string name, string projectBinDir, string outputDir)
        {
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + ".exe" : name;
            string srcExe = Path.Combine(projectBinDir, exeName);
            string srcDll = Path.Combine(projectBinDir, name + ".dll");

            // .NET Core produces both exe and dll; .NET Framework produces exe only
            if (File.Exists(srcExe))
                CopyFileIfNewer(srcExe, Path.Combine(outputDir, exeName));

            if (File.Exists(srcDll))
                CopyFileIfNewer(srcDll, Path.Combine(outputDir, name + ".dll"));

            // Copy PDB
            string srcPdb = Path.Combine(projectBinDir, name + ".pdb");
            if (File.Exists(srcPdb))
                CopyFileIfNewer(srcPdb, Path.Combine(outputDir, name + ".pdb"));

            // Copy SharedLibrary and its PDB if present
            string srcShared = Path.Combine(projectBinDir, "SharedLibrary.dll");
            if (File.Exists(srcShared))
            {
                CopyFileIfNewer(srcShared, Path.Combine(outputDir, "SharedLibrary.dll"));
                string srcSharedPdb = Path.Combine(projectBinDir, "SharedLibrary.pdb");
                if (File.Exists(srcSharedPdb))
                    CopyFileIfNewer(srcSharedPdb, Path.Combine(outputDir, "SharedLibrary.pdb"));
            }

            // Copy the runtimeconfig.json for .NET Core targets
            string runtimeConfig = Path.Combine(projectBinDir, name + ".runtimeconfig.json");
            if (File.Exists(runtimeConfig))
                CopyFileIfNewer(runtimeConfig, Path.Combine(outputDir, name + ".runtimeconfig.json"));

            // Copy deps.json for .NET Core targets
            string depsJson = Path.Combine(projectBinDir, name + ".deps.json");
            if (File.Exists(depsJson))
                CopyFileIfNewer(depsJson, Path.Combine(outputDir, name + ".deps.json"));
        }

        private static void RunDotnetPublish(string csprojPath, string tfm, string rid, string outputDir)
        {
            ProcessStartInfo psi = new("dotnet")
            {
                Arguments = $"publish \"{csprojPath}\" --nologo -f {tfm} -r {rid} -p:PublishSingleFile=true -p:SelfContained=true -p:IsPublishable=true -o \"{outputDir}\" -v:q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"dotnet publish (single-file) failed for {csprojPath}:\n{output}\n{error}");
        }

        private static void RunDotnetBuild(string csprojPath, string tfm, string platform)
        {
            ProcessStartInfo psi = new("dotnet")
            {
                Arguments = $"build \"{csprojPath}\" --nologo -f {tfm} -p:PlatformTarget={platform} -v:q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"dotnet build failed for {csprojPath}:\n{output}\n{error}");
        }

        /// <summary>
        /// Generates a dump under the high-bit host for a .NET Framework target. DbgEng
        /// launches HighBitHost.exe, which reserves low memory, then hosts CLR v4 and
        /// runs the target .exe's entry point. When the target throws, DbgEng captures
        /// the dump just as it does in the non-HighBit Framework path.
        /// </summary>
        private static void GenerateHighBitFrameworkDump(string exePath, string dumpPath, GCMode gcMode, bool full, string architecture)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("High-bit dump generation is only supported on Windows.");
            if (architecture != "x86" && architecture != "x64")
                throw new PlatformNotSupportedException($"High-bit dump generation requires x86 (or x64 for parity); got {architecture}.");
            if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"High-bit framework host requires a .NET Framework .exe, got {exePath}");

            string host = BuildHighBitHost(architecture);

            const uint ClrExceptionCode = 0xe0434352;

            DebuggerStartInfo info = new();
            if (gcMode == GCMode.Server)
            {
                info.SetEnvironmentVariable("COMPlus_BuildFlavor", "SVR");
            }

            string commandLine = $"\"{host}\" --mode=framework \"{exePath}\"";
            LaunchAndCaptureDump(info, commandLine, dumpPath, full, ClrExceptionCode);
        }

        /// <summary>
        /// Generates a dump under the high-bit host. The host reserves memory below 0x80000000
        /// before starting the CLR, forcing heap allocations into the upper 2 GB of the 32-bit
        /// address space. Windows x86 only.
        /// </summary>
        private static string GenerateHighBitCoreDump(string exePath, string dumpPath, GCMode gcMode, bool full, string architecture)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("High-bit dump generation is only supported on Windows.");
            if (architecture != "x86" && architecture != "x64")
                throw new PlatformNotSupportedException($"High-bit dump generation requires x86 (or x64 for parity); got {architecture}.");

            // The high-bit host loads and runs a managed DLL via hostfxr. Prefer the .dll; if only
            // the .exe exists, swap the extension.
            string dllPath = exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(exePath, ".dll")
                : exePath;
            if (!File.Exists(dllPath))
                throw new FileNotFoundException($"High-bit host requires a managed DLL, not found: {dllPath}");

            string host = BuildHighBitHost(architecture);

            ProcessStartInfo psi = new(host)
            {
                Arguments = $"\"{dllPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            psi.Environment["DOTNET_DbgEnableMiniDump"] = "1";
            psi.Environment["COMPlus_DbgEnableMiniDump"] = "1";

            string dumpType = full ? "4" : "1";
            psi.Environment["DOTNET_DbgMiniDumpType"] = dumpType;
            psi.Environment["COMPlus_DbgMiniDumpType"] = dumpType;

            psi.Environment["DOTNET_DbgMiniDumpName"] = dumpPath;
            psi.Environment["COMPlus_DbgMiniDumpName"] = dumpPath;

            psi.Environment["DOTNET_EnableCrashReport"] = "1";
            psi.Environment["COMPlus_EnableCrashReport"] = "1";

            if (gcMode == GCMode.Server)
            {
                psi.Environment["DOTNET_gcServer"] = "1";
                psi.Environment["COMPlus_gcServer"] = "1";
            }

            using Process process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(120_000))
            {
                process.Kill();
                throw new TimeoutException($"HighBitHost for {dllPath} did not exit within 120 seconds.\nstdout: {stdout}\nstderr: {stderr}");
            }

            return $"exit={process.ExitCode}\nhost: {host}\nstdout: {stdout}\nstderr: {stderr}";
        }

        /// <summary>
        /// Lazily builds HighBitHost.exe for the given architecture using MSBuild with the C++
        /// toolset. The built executable lives at src/TestTargets/bin/HighBitHost/{Platform}/.
        /// Requires Visual Studio with the C++ workload on PATH via vswhere.
        /// </summary>
        private static string BuildHighBitHost(string architecture)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("HighBitHost is only supported on Windows.");

            string platform = architecture == "x86" ? "Win32" : "x64";

            // Walk up to the repo root (marker: .gitignore) so we can locate the host project.
            DirectoryInfo info = new(Environment.CurrentDirectory);
            while (info != null && info.GetFiles(".gitignore").Length != 1)
                info = info.Parent;
            if (info is null)
                throw new InvalidOperationException("Could not locate repository root for HighBitHost build.");

            string repoRoot = info.FullName;
            string projectPath = Path.Combine(repoRoot, "src", "TestTargets", "HighBitHost", "HighBitHost.vcxproj");
            string hostExe = Path.Combine(repoRoot, "src", "TestTargets", "bin", "HighBitHost", platform, "HighBitHost.exe");

            if (File.Exists(hostExe))
                return hostExe;

            string msbuild = FindMsBuild();

            ProcessStartInfo psi = new(msbuild)
            {
                Arguments = $"\"{projectPath}\" /p:Configuration=Debug /p:Platform={platform} /nologo /v:minimal",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(hostExe))
                throw new InvalidOperationException($"Failed to build HighBitHost ({platform}):\n{stdout}\n{stderr}");

            return hostExe;
        }

        /// <summary>
        /// Locates MSBuild.exe using vswhere. Required for the C++ HighBitHost build, since
        /// 'dotnet build' does not drive vcxproj.
        /// </summary>
        private static string FindMsBuild()
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
                                  ?? Environment.GetEnvironmentVariable("ProgramFiles");
            string vswhere = Path.Combine(programFiles ?? string.Empty, "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (!File.Exists(vswhere))
                throw new FileNotFoundException($"vswhere.exe not found at {vswhere}. Install Visual Studio with the C++ workload.");

            ProcessStartInfo psi = new(vswhere)
            {
                Arguments = "-latest -prerelease -products * -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process process = Process.Start(psi);
            string installDir = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (string.IsNullOrEmpty(installDir))
                throw new InvalidOperationException("vswhere found no Visual Studio installation with the C++ toolset.");

            // vswhere may return multiple lines if -latest is ignored; take the first.
            string firstLine = installDir.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            string msbuild = Path.Combine(firstLine, "MSBuild", "Current", "Bin", "MSBuild.exe");
            if (!File.Exists(msbuild))
                throw new FileNotFoundException($"MSBuild.exe not found at {msbuild}.");

            return msbuild;
        }

        /// <summary>
        /// Generates a dump for a .NET Core target using DOTNET_DbgEnableMiniDump environment variables.
        /// The target is expected to crash with an unhandled exception.
        /// </summary>
        private static string GenerateCoreDump(string exePath, string dumpPath, GCMode gcMode, bool full, bool singleFile = false)
        {
            bool isDll = exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

            ProcessStartInfo psi;
            if (isDll)
            {
                psi = new ProcessStartInfo("dotnet")
                {
                    Arguments = $"\"{exePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
            }
            else
            {
                psi = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
            }

            // Configure crash dump collection via environment variables
            psi.Environment["DOTNET_DbgEnableMiniDump"] = "1";
            psi.Environment["COMPlus_DbgEnableMiniDump"] = "1";

            // Type 4 = full heap dump, Type 1 = mini dump
            string dumpType = full ? "4" : "1";
            psi.Environment["DOTNET_DbgMiniDumpType"] = dumpType;
            psi.Environment["COMPlus_DbgMiniDumpType"] = dumpType;

            psi.Environment["DOTNET_DbgMiniDumpName"] = dumpPath;
            psi.Environment["COMPlus_DbgMiniDumpName"] = dumpPath;

            // Enable crash reports (not supported for single-file apps)
            if (!singleFile)
            {
                psi.Environment["DOTNET_EnableCrashReport"] = "1";
                psi.Environment["COMPlus_EnableCrashReport"] = "1";
            }
            else
            {
                psi.Environment["DOTNET_EnableCrashReport"] = "0";
                psi.Environment["COMPlus_EnableCrashReport"] = "0";
            }

            if (gcMode == GCMode.Server)
            {
                psi.Environment["DOTNET_gcServer"] = "1";
                psi.Environment["COMPlus_gcServer"] = "1";
            }

            using Process process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(60_000))
            {
                process.Kill();
                throw new TimeoutException($"Test target {exePath} did not exit within 60 seconds.\nstdout: {stdout}\nstderr: {stderr}");
            }

            return $"exit={process.ExitCode}\nstdout: {stdout}\nstderr: {stderr}";
        }

        /// <summary>
        /// Generates a dump for a .NET Framework target using DbgEng on Windows.
        /// Launches the target under the debugger and captures a dump on the unhandled CLR exception.
        /// </summary>
        private static void GenerateFrameworkDump(string exePath, string dumpPath, GCMode gcMode, bool full)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("Framework dump generation is only supported on Windows.");

            const uint ClrExceptionCode = 0xe0434352;

            DebuggerStartInfo info = new();
            if (gcMode == GCMode.Server)
            {
                info.SetEnvironmentVariable("COMPlus_BuildFlavor", "SVR");
            }

            LaunchAndCaptureDump(info, exePath, dumpPath, full, ClrExceptionCode);
        }

        /// <summary>
        /// Generates a dump for a .NET Core single-file target using DbgEng on Windows.
        /// Single-file apps don't include createdump, so DOTNET_DbgEnableMiniDump won't work.
        /// </summary>
        private static void GenerateDbgEngDump(string exePath, string dumpPath, GCMode gcMode, bool full)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("DbgEng dump generation is only supported on Windows.");

            const uint ClrExceptionCode = 0xe0434352;

            DebuggerStartInfo info = new();
            if (gcMode == GCMode.Server)
            {
                info.SetEnvironmentVariable("DOTNET_gcServer", "1");
                info.SetEnvironmentVariable("COMPlus_gcServer", "1");
            }

            LaunchAndCaptureDump(info, exePath, dumpPath, full, ClrExceptionCode);
        }

        private static void LaunchAndCaptureDump(DebuggerStartInfo info, string exePath, string dumpPath, bool full, uint exceptionCode)
        {
            using Debugger debugger = info.LaunchProcess(exePath, Path.GetDirectoryName(exePath));

            string miniDumpPath = full ? null : dumpPath;
            string fullDumpPath = full ? dumpPath : null;

            debugger.OnException += (dbg, exception, firstChance) =>
            {
                if (!firstChance && exception.ExceptionCode == exceptionCode)
                {
                    if (fullDumpPath != null)
                        dbg.WriteDumpFile(fullDumpPath, DEBUG_DUMP.DEFAULT);
                    if (miniDumpPath != null)
                        dbg.WriteDumpFile(miniDumpPath, DEBUG_DUMP.SMALL);
                }
            };

            DEBUG_STATUS status;
            do
            {
                status = debugger.ProcessEvents(TimeSpan.MaxValue);
            } while (status != DEBUG_STATUS.NO_DEBUGGEE);
        }

        /// <summary>
        /// Returns the architecture string for the current process or for the test being run.
        /// </summary>
        public static string GetArchitecture()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "x86",
                Architecture.X64 => "x64",
                Architecture.Arm => "arm",
                Architecture.Arm64 => "arm64",
                _ => "x64"
            };
        }

        /// <summary>
        /// Returns the "other" architecture for cross-bitness testing.
        /// On x64 returns x86, on arm64 returns arm, etc.
        /// </summary>
        public static string GetOtherArchitecture()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "x64",
                Architecture.X64 => "x86",
                Architecture.Arm => "arm64",
                Architecture.Arm64 => "arm",
                _ => "x86"
            };
        }
    }

    /// <summary>
    /// Lightweight debugger wrapper for generating dumps on Windows.
    /// Adapted from TestTasks/Debugger.cs to run at test time instead of MSBuild time.
    /// </summary>
    internal sealed class DebuggerStartInfo
    {
        private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);

        public DebuggerStartInfo()
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Value is string strValue)
                    _environment[(string)entry.Key] = strValue;
            }
        }

        public void SetEnvironmentVariable(string variable, string value)
        {
            _environment[variable] = value;
        }

        public Debugger LaunchProcess(string commandLine, string workingDirectory)
        {
            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = Environment.CurrentDirectory;

            return new Debugger(null, commandLine, workingDirectory, _environment);
        }
    }

    internal sealed class Debugger : IDebugOutputCallbacks, IDebugEventCallbacks, IDisposable
    {
        private bool _exited;
        private bool _processing;
        private readonly IDisposable _dbgeng;
        private readonly IDebugClient _client;
        private readonly IDebugControl _control;

        public delegate void ExceptionEventHandler(Debugger debugger, EXCEPTION_RECORD64 ex, bool firstChance);
        public event ExceptionEventHandler OnException;

        public Debugger(string dbgEngDirectory, string commandLine, string workingDirectory, IEnumerable<KeyValuePair<string, string>> env)
        {
            _dbgeng = IDebugClient.Create(dbgEngDirectory);
            _client = (IDebugClient)_dbgeng;
            _control = (IDebugControl)_client;

            DEBUG_CREATE_PROCESS_OPTIONS options = default;
            options.CreateFlags = DEBUG_CREATE_PROCESS.DEBUG_PROCESS;
            int hr = _client.CreateProcessAndAttach(commandLine, workingDirectory, env, DEBUG_ATTACH.DEFAULT, options);

            if (hr < 0)
                throw new Exception($"IDebugClient::CreateProcessAndAttach failed with hresult={hr:X8}");

            _client.SetEventCallbacks(this);
            _client.SetOutputCallbacks(this);
        }

        public DEBUG_STATUS ProcessEvents(TimeSpan timeout)
        {
            if (_processing)
                throw new InvalidOperationException("Cannot call ProcessEvents reentrantly.");

            if (_exited)
                return DEBUG_STATUS.NO_DEBUGGEE;

            _processing = true;
            int hr = _control.WaitForEvent(timeout);
            _processing = false;

            if (hr < 0 && (uint)hr != 0x8000000A)
                throw new Exception($"IDebugControl::WaitForEvent failed with hresult={hr:X8}");

            _control.GetExecutionStatus(out DEBUG_STATUS status);
            return status;
        }

        public int WriteDumpFile(string dump, DEBUG_DUMP type)
        {
            if (type == DEBUG_DUMP.DEFAULT)
                return _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, $".dump /ma {dump}", DEBUG_EXECUTE.DEFAULT);

            return _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, $".dump /m {dump}", DEBUG_EXECUTE.DEFAULT);
        }

        public void Dispose()
        {
            _client.SetEventCallbacks(null);
            _client.SetOutputCallbacks(null);
            _dbgeng.Dispose();
        }

        public DEBUG_EVENT EventInterestMask => DEBUG_EVENT.BREAKPOINT | DEBUG_EVENT.EXCEPTION | DEBUG_EVENT.EXIT_PROCESS;

        void IDebugOutputCallbacks.OnText(DEBUG_OUTPUT flags, string text, ulong args) { }

        DEBUG_STATUS IDebugEventCallbacks.OnBreakpoint(nint bp) => DEBUG_STATUS.GO;

        DEBUG_STATUS IDebugEventCallbacks.OnException(in EXCEPTION_RECORD64 exception, bool firstChance)
        {
            OnException?.Invoke(this, exception, firstChance);
            return DEBUG_STATUS.BREAK;
        }

        DEBUG_STATUS IDebugEventCallbacks.OnExitProcess(int exitCode)
        {
            _exited = true;
            return DEBUG_STATUS.BREAK;
        }
    }
}
