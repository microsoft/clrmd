using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using System;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    internal static class Program
    {
        public static string CrashDump { get; private set; }
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

        static void Main(string[] _)
        {
            CrashDump = Environment.GetEnvironmentVariable("ClrmdBenchmarkDumpFile");
            if (string.IsNullOrWhiteSpace(CrashDump))
            {
                Console.WriteLine($"You must set the 'ClrmdBenchmarkDumpFile' environment variable before running this program.");
                Environment.Exit(1);
            }

            var settings = NetCoreAppSettings.NetCoreApp31.WithCustomDotNetCliPath(@"C:\Program Files (x86)\dotnet\dotnet.exe");
            var config = ManualConfig.Create(DefaultConfig.Instance);
            Job job = Job.RyuJitX86.With(CsProjCoreToolchain.From(settings))
                        .WithId("32bit")
                        .WithWarmupCount(1) // 1 warmup is enough for our purpose
                        .WithIterationTime(TimeInterval.FromSeconds(25)) // the default is 0.5s per iteration, which is slighlty too much for us
                        .WithMinIterationCount(10)
                        .WithMaxIterationCount(20) // we don't want to run more that 20 iterations
                        .DontEnforcePowerPlan(); // make sure BDN does not try to enforce High Performance power plan on Windows;

            config.Add(job);
            BenchmarkRunner.Run<ParallelHeapBenchmarks>(config);
        }
    }
}
