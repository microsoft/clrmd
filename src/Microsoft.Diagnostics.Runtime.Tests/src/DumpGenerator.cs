// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        public static void EnsureDump(string name, string projectDir, string dumpPath, string architecture, bool isFramework, GCMode gcMode, bool full, bool singleFile = false, bool highBit = false, string[] companionTargets = null, IReadOnlyDictionary<string, string> extraEnv = null)
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
                    GenerateHighBitFrameworkDump(exePath, dumpPath, gcMode, full, architecture, extraEnv);
                }
                else if (highBit)
                {
                    processOutput = GenerateHighBitCoreDump(exePath, dumpPath, gcMode, full, architecture, extraEnv);
                }
                else if (isFramework && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    GenerateFrameworkDump(exePath, dumpPath, gcMode, full, extraEnv);
                }
                else if (singleFile && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Single-file self-contained apps don't include createdump, so
                    // DOTNET_DbgEnableMiniDump won't work. Use DbgEng instead.
                    GenerateDbgEngDump(exePath, dumpPath, gcMode, full, extraEnv);
                }
                else
                {
                    processOutput = GenerateCoreDump(exePath, dumpPath, gcMode, full, singleFile, extraEnv);
                }

                if (!File.Exists(dumpPath))
                {
                    string msg = $"Failed to generate dump: {dumpPath}";
                    if (singleFile)
                        msg += $"\n  exePath: {exePath}\n  exists: {File.Exists(exePath)}";
                    if (processOutput != null)
                        msg += $"\n  process: {processOutput}";
                    else
                        msg += $"\n  exePath: {exePath}\n  highBit: {highBit}\n  isFramework: {isFramework}";
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
                catch (IOException) when (attempt < 5)
                {
                    System.Threading.Thread.Sleep(200 * (attempt + 1));
                }
                catch (IOException) when (File.Exists(dest))
                {
                    // Destination already exists and is locked (commonly because another
                    // ClrMD DataTarget has memory-mapped it for module metadata). Since
                    // SharedLibrary.dll and companion binaries are byte-identical between
                    // builds within the same TFM/arch, a stale copy is equivalent. Tolerate
                    // the collision rather than failing the test that triggered the build.
                    return;
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
                Arguments = $"publish \"{csprojPath}\" --nologo --disable-build-servers -f {tfm} -r {rid} -p:PublishSingleFile=true -p:SelfContained=true -p:IsPublishable=true -p:UseSharedCompilation=false -o \"{outputDir}\" -v:q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            int exitCode = RunAndCapture(psi, out string output, out string error);
            if (exitCode != 0)
                throw new InvalidOperationException($"dotnet publish (single-file) failed for {csprojPath}:\n{output}\n{error}");
        }

        private static void RunDotnetBuild(string csprojPath, string tfm, string platform)
        {
            ProcessStartInfo psi = new("dotnet")
            {
                Arguments = $"build \"{csprojPath}\" --nologo --disable-build-servers -f {tfm} -p:PlatformTarget={platform} -p:UseSharedCompilation=false -v:q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            int exitCode = RunAndCapture(psi, out string output, out string error);
            if (exitCode != 0)
                throw new InvalidOperationException($"dotnet build failed for {csprojPath}:\n{output}\n{error}");
        }

        /// <summary>
        /// Runs a process and captures stdout/stderr without deadlocking when child
        /// processes (e.g., MSBuild/Roslyn build servers) inherit pipe handles and keep
        /// them open past the parent's exit. Synchronous ReadToEnd() blocks forever in
        /// that case; we drain both pipes asynchronously and wait for EOF explicitly.
        /// </summary>
        private static int RunAndCapture(ProcessStartInfo psi, out string stdout, out string stderr)
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;

            StringBuilder outBuf = new();
            StringBuilder errBuf = new();
            using ManualResetEventSlim outDone = new(false);
            using ManualResetEventSlim errDone = new(false);

            using Process process = new() { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    outDone.Set();
                else
                    lock (outBuf) outBuf.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    errDone.Set();
                else
                    lock (errBuf) errBuf.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process exit, then for both pipes to signal EOF. If a child
            // process inherits the pipes and lingers, WaitForExit returns promptly but
            // the EOF callback may be delayed; bound the wait so we never hang forever.
            process.WaitForExit();
            outDone.Wait(TimeSpan.FromSeconds(30));
            errDone.Wait(TimeSpan.FromSeconds(30));

            lock (outBuf) stdout = outBuf.ToString();
            lock (errBuf) stderr = errBuf.ToString();
            return process.ExitCode;
        }

        /// <summary>
        /// Generates a dump under the high-bit host for a .NET Framework target. DbgEng
        /// launches HighBitHost.exe, which reserves low memory, then hosts CLR v4 and
        /// runs the target .exe's entry point. When the target throws, DbgEng captures
        /// the dump just as it does in the non-HighBit Framework path.
        /// </summary>
        private static void GenerateHighBitFrameworkDump(string exePath, string dumpPath, GCMode gcMode, bool full, string architecture, IReadOnlyDictionary<string, string> extraEnv = null)
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

            if (extraEnv != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraEnv)
                    info.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            string commandLine = $"\"{host}\" --mode=framework \"{exePath}\"";
            LaunchAndCaptureDump(info, commandLine, workingDirectory: Path.GetDirectoryName(exePath), dumpPath, full, ClrExceptionCode);
        }

        /// <summary>
        /// Generates a dump under the high-bit host. The host reserves memory below 0x80000000
        /// before starting the CLR, forcing heap allocations into the upper 2 GB of the 32-bit
        /// address space. Windows x86 only.
        /// </summary>
        private static string GenerateHighBitCoreDump(string exePath, string dumpPath, GCMode gcMode, bool full, string architecture, IReadOnlyDictionary<string, string> extraEnv = null)
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

            // See SetDotNetRoot; HighBitHost uses hostfxr to load the managed DLL and
            // therefore also needs to find the shared framework via DOTNET_ROOT.
            SetDotNetRoot(psi);

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

            if (extraEnv != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraEnv)
                    psi.Environment[kvp.Key] = kvp.Value;
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
        /// Points the spawned process at the same .NET install that's hosting the test
        /// runner by setting DOTNET_ROOT (and the arch-specific variants).  Prefers
        /// DOTNET_HOST_PATH / already-set DOTNET_ROOT variables forwarded from the
        /// test host's environment so hostfxr probes a real shared-framework layout.
        /// Environment.ProcessPath (typically the vstest testhost.exe) is only a fallback.
        /// </summary>
        private static void SetDotNetRoot(ProcessStartInfo psi)
        {
            string dotnetRoot = ResolveDotNetRoot();
            if (string.IsNullOrEmpty(dotnetRoot))
                return;

            // Generic fallback read by all apphosts.
            psi.Environment["DOTNET_ROOT"] = dotnetRoot;

            // Arch-specific overrides (the apphost and nethost consult these before DOTNET_ROOT).
            // When running an x86 test host, we want DOTNET_ROOT(x86); x64 gets DOTNET_ROOT_X64.
            if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                psi.Environment["DOTNET_ROOT(x86)"] = dotnetRoot;
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                psi.Environment["DOTNET_ROOT_X64"] = dotnetRoot;
        }

        private static string ResolveDotNetRoot()
        {
            // Arch-specific env var set by the launching dotnet.exe.
            string archVar = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "DOTNET_ROOT(x86)",
                Architecture.X64 => "DOTNET_ROOT_X64",
                _ => "DOTNET_ROOT"
            };

            foreach (string var in new[] { archVar, "DOTNET_ROOT" })
            {
                string v = Environment.GetEnvironmentVariable(var);
                if (!string.IsNullOrEmpty(v) && HasSharedFramework(v))
                    return v;
            }

            // DOTNET_HOST_PATH is set by `dotnet exec/test` and points at the dotnet.exe
            // used to launch the test host. On mixed-arch runs (x64 dotnet launching an
            // x86 testhost) this is the wrong arch, so we only trust it if the arch
            // matches our process.
            string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(hostPath))
            {
                string dir = Path.GetDirectoryName(hostPath);
                if (HasSharedFramework(dir) && DotNetRootMatchesArch(dir))
                    return dir;
            }

            // Standard install locations. Windows: Program Files (x86)\dotnet (x86),
            // Program Files\dotnet (x64).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string candidate = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                    Architecture.X64 => Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetEnvironmentVariable("ProgramFiles"),
                    _ => null
                };
                if (!string.IsNullOrEmpty(candidate))
                {
                    string root = Path.Combine(candidate, "dotnet");
                    if (HasSharedFramework(root))
                        return root;
                }
            }

            // Last resort: ProcessPath's directory. Only useful when the test host is
            // literally dotnet.exe (e.g., `dotnet exec xunit.console.dll`).
            string processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                string dir = Path.GetDirectoryName(processPath);
                if (HasSharedFramework(dir))
                    return dir;
            }

            return null;
        }

        private static bool HasSharedFramework(string root)
        {
            if (string.IsNullOrEmpty(root))
                return false;

            string shared = Path.Combine(root, "shared", "Microsoft.NETCore.App");
            return Directory.Exists(shared);
        }

        /// <summary>
        /// Sanity-check that the given dotnet root hosts a runtime matching our process
        /// architecture by opening one of the hostfxr.dll binaries and reading the PE
        /// machine field. This avoids picking an x64 install when spawning an x86 child.
        /// </summary>
        private static bool DotNetRootMatchesArch(string root)
        {
            try
            {
                string hostfxrRoot = Path.Combine(root, "host", "fxr");
                if (!Directory.Exists(hostfxrRoot))
                    return true; // can't verify; assume it's fine

                string[] matches = Directory.GetFiles(hostfxrRoot, "hostfxr.dll", SearchOption.AllDirectories);
                string hostfxr = matches.Length > 0 ? matches[0] : null;
                if (hostfxr is null)
                    return true;

                ushort machine = ReadPEMachine(hostfxr);
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => machine == 0x014c,
                    Architecture.X64 => machine == 0x8664,
                    Architecture.Arm => machine == 0x01c4,
                    Architecture.Arm64 => machine == 0xAA64,
                    _ => true
                };
            }
            catch
            {
                return true;
            }
        }

        private static ushort ReadPEMachine(string path)
        {
            using FileStream fs = File.OpenRead(path);
            using BinaryReader br = new(fs);
            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = br.ReadInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) // "PE\0\0"
                return 0;
            return br.ReadUInt16();
        }

        /// <summary>
        /// Lazily builds HighBitHost.exe for the given architecture using MSBuild with the C++
        /// toolset. The built executable lives at src/TestTargets/bin/HighBitHost/{Platform}/.
        /// Requires Visual Studio with the C++ workload on PATH via vswhere.
        /// </summary>
        private static readonly Dictionary<string, Exception> s_highBitHostBuildFailures = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object s_highBitHostBuildLock = new();

        private static string BuildHighBitHost(string architecture)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("HighBitHost is only supported on Windows.");

            string platform = architecture == "x86" ? "Win32" : "x64";

            lock (s_highBitHostBuildLock)
            {
                // Cache negative results: MSBuild can take many minutes to fail on CI when the
                // required toolset is missing. Without this cache every highbit test repeats
                // the slow failure, which will eventually exceed the CI job timeout.
                if (s_highBitHostBuildFailures.TryGetValue(platform, out Exception cached))
                    throw new InvalidOperationException("HighBitHost previously failed to build; see inner exception.", cached);

                try
                {
                    return BuildHighBitHostCore(platform);
                }
                catch (Exception ex)
                {
                    s_highBitHostBuildFailures[platform] = ex;
                    throw;
                }
            }
        }

        private static string BuildHighBitHostCore(string platform)
        {
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

            string msbuild = FindMsBuild(out string vsInstallPath);

            // Select a platform toolset that's actually installed under this VS.
            // Microsoft.Cpp.Default.props will pick DefaultPlatformToolset automatically,
            // but CI images sometimes advertise a VS without the matching toolset folder,
            // so we probe the installation and pass the answer through explicitly.
            string toolset = ResolvePlatformToolset(vsInstallPath);

            ProcessStartInfo psi = new(msbuild)
            {
                Arguments = $"\"{projectPath}\" /p:Configuration=Debug /p:Platform={platform} /p:PlatformToolset={toolset} /nologo /v:minimal /nodeReuse:false /p:UseSharedCompilation=false",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            int exitCode = RunAndCapture(psi, out string stdout, out string stderr);

            if (exitCode != 0 || !File.Exists(hostExe))
                throw new InvalidOperationException($"Failed to build HighBitHost ({platform}):\n{stdout}\n{stderr}");

            return hostExe;
        }

        /// <summary>
        /// Locates MSBuild.exe using vswhere. Required for the C++ HighBitHost build, since
        /// 'dotnet build' does not drive vcxproj.
        /// </summary>
        private static string FindMsBuild() => FindMsBuild(out _);

        private static string FindMsBuild(out string vsInstallPath)
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

            RunAndCapture(psi, out string installDir, out _);
            installDir = installDir.Trim();

            if (string.IsNullOrEmpty(installDir))
                throw new InvalidOperationException("vswhere found no Visual Studio installation with the C++ toolset.");

            // vswhere may return multiple lines if -latest is ignored; take the first.
            string firstLine = installDir.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            string msbuild = Path.Combine(firstLine, "MSBuild", "Current", "Bin", "MSBuild.exe");
            if (!File.Exists(msbuild))
                throw new FileNotFoundException($"MSBuild.exe not found at {msbuild}.");

            vsInstallPath = firstLine;
            return msbuild;
        }

        /// <summary>
        /// Picks the newest platform toolset actually installed under the given VS instance
        /// by probing VC\Auxiliary\Build\Microsoft.VCToolsVersion.v14X.default.txt.
        /// </summary>
        private static string ResolvePlatformToolset(string vsInstallPath)
        {
            string auxBuild = Path.Combine(vsInstallPath, "VC", "Auxiliary", "Build");
            foreach (string toolset in new[] { "v143", "v142" })
            {
                if (File.Exists(Path.Combine(auxBuild, $"Microsoft.VCToolsVersion.{toolset}.default.txt")))
                    return toolset;
            }

            // Fallback: directory-based check under VC\Tools\MSVC\<version>.
            if (Directory.Exists(auxBuild))
            {
                foreach (string file in Directory.EnumerateFiles(auxBuild, "Microsoft.VCToolsVersion.v14*.default.txt"))
                {
                    string name = Path.GetFileName(file);
                    // name = Microsoft.VCToolsVersion.v14X.default.txt
                    string[] parts = name.Split('.');
                    if (parts.Length >= 3)
                        return parts[2];
                }
            }

            throw new InvalidOperationException($"No v142/v143 C++ toolset found under '{auxBuild}'.");
        }

        /// <summary>
        /// Generates a dump for a .NET Core target using DOTNET_DbgEnableMiniDump environment variables.
        /// The target is expected to crash with an unhandled exception.
        /// </summary>
        private static string GenerateCoreDump(string exePath, string dumpPath, GCMode gcMode, bool full, bool singleFile = false, IReadOnlyDictionary<string, string> extraEnv = null)
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

            // Point the spawned apphost at the same .NET install that's hosting the test
            // runner.  Hosted CI agents often only have older runtimes in
            // C:\Program Files\dotnet\, so we rely on the repo-local SDK that Arcade
            // installed in .dotnet\ (or .dotnet\x86\).  Environment.ProcessPath is the
            // dotnet.exe that started the test host, so its directory is exactly the
            // DOTNET_ROOT we want the child to use.
            SetDotNetRoot(psi);

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

            if (extraEnv != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraEnv)
                {
                    psi.Environment[kvp.Key] = kvp.Value;
                }
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
        private static void GenerateFrameworkDump(string exePath, string dumpPath, GCMode gcMode, bool full, IReadOnlyDictionary<string, string> extraEnv = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("Framework dump generation is only supported on Windows.");

            const uint ClrExceptionCode = 0xe0434352;

            DebuggerStartInfo info = new();
            if (gcMode == GCMode.Server)
            {
                info.SetEnvironmentVariable("COMPlus_BuildFlavor", "SVR");
            }

            if (extraEnv != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraEnv)
                {
                    info.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }

            LaunchAndCaptureDump(info, exePath, dumpPath, full, ClrExceptionCode);
        }

        /// <summary>
        /// Generates a dump for a .NET Core single-file target using DbgEng on Windows.
        /// Single-file apps don't include createdump, so DOTNET_DbgEnableMiniDump won't work.
        /// </summary>
        private static void GenerateDbgEngDump(string exePath, string dumpPath, GCMode gcMode, bool full, IReadOnlyDictionary<string, string> extraEnv = null)
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

            if (extraEnv != null)
            {
                foreach (KeyValuePair<string, string> kvp in extraEnv)
                {
                    info.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }

            LaunchAndCaptureDump(info, exePath, dumpPath, full, ClrExceptionCode);
        }

        private static void LaunchAndCaptureDump(DebuggerStartInfo info, string exePath, string dumpPath, bool full, uint exceptionCode)
            => LaunchAndCaptureDump(info, exePath, workingDirectory: Path.GetDirectoryName(exePath), dumpPath, full, exceptionCode);

        private static void LaunchAndCaptureDump(DebuggerStartInfo info, string commandLine, string workingDirectory, string dumpPath, bool full, uint exceptionCode)
        {
            using Debugger debugger = info.LaunchProcess(commandLine, workingDirectory);

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
            try
            {
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
            catch
            {
                // DbgEng only tolerates one live instance per process. If construction fails
                // (e.g., bad command line or working directory) we must dispose the wrapper
                // here or every subsequent DbgEng-using test in this run will fault with
                // "DbgEng doesn't properly handle multiple instances simultaneously".
                _dbgeng.Dispose();
                throw;
            }
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
