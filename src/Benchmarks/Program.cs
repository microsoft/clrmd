// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using System;

namespace Benchmarks
{
    internal static class Program
    {
        private const string DumpFileEnv = "ClrmdBenchmarkDumpFile";

        public static string CrashDump => Environment.GetEnvironmentVariable(DumpFileEnv) ?? throw new InvalidOperationException($"You must set the '{DumpFileEnv}' environment variable before running this program.");

        static void Main(string[] args)
        {
            var job = Job.Default
                .WithWarmupCount(1)
                .WithIterationTime(TimeInterval.FromSeconds(1))
                .WithMinIterationCount(5)
                .WithMaxIterationCount(10)
                .AsDefault(); // AsDefault tells BDN ConfigParser that this job should be extended with setting specified via console line args

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, DefaultConfig.Instance.AddJob(job));
        }
    }
}
