using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    internal static class Program
    {
        private const string DumpFileEnv = "ClrmdBenchmarkDumpFile";
        private const string DotnetEnv = "ClrmdBenchmarkDotnet";

        public static string CrashDump => Environment.GetEnvironmentVariable(DumpFileEnv) ?? throw new InvalidOperationException("You must set the 'ClrmdBenchmarkDumpFile' environment variable before running this program.");

        static void Main(string[] args)
        {
            // Set the argument as the crash dump.  We can't just set CrashDump here because it needs to be read from child processes.
            if (args.Length > 0)
                Environment.SetEnvironmentVariable(DumpFileEnv, args[0]);
            
            // We want to run this even if we don't use the result to make sure we can successfully load 'CrashDump'.
            int targetPointerSize = GetTargetPointerSize();


            ManualConfig benchmarkConfiguration = ManualConfig.Create(DefaultConfig.Instance);

            // Windows supports x86 and x64 so we need to choose the correct version of .Net.
            Job job;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string dotnetPath = GetDotnetPath(targetPointerSize);

                if (targetPointerSize == 4)
                    job = Job.RyuJitX86.With(CsProjCoreToolchain.From(NetCoreAppSettings.NetCoreApp31.WithCustomDotNetCliPath(dotnetPath)));
                else
                    job = Job.RyuJitX64.With(CsProjCoreToolchain.From(NetCoreAppSettings.NetCoreApp31.WithCustomDotNetCliPath(dotnetPath)));
            }
            else
            {
                job = Job.Default;
            }

            string id = $"{RuntimeInformation.OSDescription} {RuntimeInformation.FrameworkDescription} {(targetPointerSize == 4 ? "32bit" : "64bit")}";
            job = job.WithId(id)
                        .WithWarmupCount(1)
                        .WithIterationTime(TimeInterval.FromSeconds(1))
                        .WithMinIterationCount(5)
                        .WithMaxIterationCount(10)
                        .DontEnforcePowerPlan(); // make sure BDN does not try to enforce High Performance power plan on Windows

            benchmarkConfiguration.Add(job);


            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, benchmarkConfiguration);
        }

        public static string GetDotnetPath(int pointerSize)
        {
            string dotnetOverride = Environment.GetEnvironmentVariable(DotnetEnv);
            if (!string.IsNullOrWhiteSpace(dotnetOverride))
            {
                if (!File.Exists(dotnetOverride))
                    throw new FileNotFoundException($"File specified by '{DotnetEnv}' not found.", dotnetOverride);

                return dotnetOverride;
            }

            string programFiles = pointerSize == 4 ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string dotnetPath = Path.Combine(programFiles, "dotnet", "dotnet.exe");
            if (!File.Exists(dotnetPath))
                throw new FileNotFoundException($"Could not find `{dotnetPath}`.");

            return dotnetPath;
        }

        private static int GetTargetPointerSize()
        {
            using DataTarget dataTarget = DataTarget.LoadDump(CrashDump, new CacheOptions() { UseOSMemoryFeatures = false });
            return dataTarget.DataReader.PointerSize;
        }
    }
}
