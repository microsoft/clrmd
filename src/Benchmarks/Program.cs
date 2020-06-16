using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    internal static class Program
    {
        private const string DumpFileEnv = "ClrmdBenchmarkDumpFile";
        private const string DotnetEnv = "ClrmdBenchmarkDotnet";

        public static string CrashDump => Environment.GetEnvironmentVariable(DumpFileEnv) ?? throw new InvalidOperationException("You must set the 'ClrmdBenchmarkDumpFile' environment variable before running this program.");

        public static bool ShouldTestOSMemoryFeatures
        {
            get
            {
                // Currently the only time the CacheOptions.UseOSMemoryFeatures flag makes a behavioral change is
                // on Windows and when AWE is enabled.  We will skip that part of the test if the user isn't running
                // in that environment so that the tests don't run as long and so that the results aren't confused
                // as having tested the AWE reader when we didn't.
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && AweHelpers.IsAweEnabled;
            }
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


        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine($"Must specify a crash dump as the only argument to this program.");
                Environment.Exit(1);
            }
            else if (args.Length == 1)
            {
                // Set the argument as the crash dump.  We can't just set CrashDump here because it needs to be read from child processes.
                Environment.SetEnvironmentVariable(DumpFileEnv, args[0]);
            }

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
                        .WithMinIterationCount(10)
                        .WithMaxIterationCount(20)
                        .DontEnforcePowerPlan(); // make sure BDN does not try to enforce High Performance power plan on Windows

            benchmarkConfiguration.Add(job);

            BenchmarkRunner.Run<ParallelHeapBenchmarks>(benchmarkConfiguration);
        }


        private static int GetTargetPointerSize()
        {
            using DataTarget dataTarget = DataTarget.LoadDump(CrashDump, new CacheOptions() { UseOSMemoryFeatures = false });
            return dataTarget.DataReader.PointerSize;
        }
    }
}
