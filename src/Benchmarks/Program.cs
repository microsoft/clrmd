using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    internal static class Program
    {
        private const string DumpFileEnv = "ClrmdBenchmarkDumpFile";

        public static string CrashDump => Environment.GetEnvironmentVariable(DumpFileEnv) ?? throw new InvalidOperationException("You must set the 'ClrmdBenchmarkDumpFile' environment variable before running this program.");

        static void Main(string[] args)
        {
            // We want to run this even if we don't use the result to make sure we can successfully load 'CrashDump'.
            int targetPointerSize = GetTargetPointerSize();

            ManualConfig benchmarkConfiguration = ManualConfig.Create(DefaultConfig.Instance);

            string id = $"{RuntimeInformation.OSDescription} {RuntimeInformation.FrameworkDescription} {(targetPointerSize == 4 ? "32bit" : "64bit")}";
            var job = Job.Default.WithId(id)
                        .WithWarmupCount(1)
                        .WithIterationTime(TimeInterval.FromSeconds(1))
                        .WithMinIterationCount(5)
                        .WithMaxIterationCount(10)
                        .DontEnforcePowerPlan() // make sure BDN does not try to enforce High Performance power plan on Windows
                        .AsDefault();

            benchmarkConfiguration.Add(job);


            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, benchmarkConfiguration);
        }

        private static int GetTargetPointerSize()
        {
            using DataTarget dataTarget = DataTarget.LoadDump(CrashDump, new CacheOptions() { UseOSMemoryFeatures = false });
            return dataTarget.DataReader.PointerSize;
        }
    }
}
