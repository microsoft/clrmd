// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Runtime;
using System.Collections.Generic;
using System.Linq;

namespace Benchmarks
{
    public class ParallelHeapBenchmarks
    {
        private DataTarget _dataTarget;
        private ClrRuntime _runtime;
        private ThreadParallelRunner<ClrSegment> _runner;

        [ParamsSource(nameof(CacheSizeSource))]
        public long CacheSize { get; set; }

        [ParamsSource(nameof(OSMemoryFeaturesSource))]
        public bool UseOSMemoryFeatures { get; set; }

        [ParamsSource(nameof(ThreadCountSource))]
        public int Threads { get; set; }

        public IEnumerable<bool> OSMemoryFeaturesSource => BenchmarkSwitches.OSMemoryFeatureFlags;
        public IEnumerable<long> CacheSizeSource => BenchmarkSwitches.RelevantCacheSizes;
        public IEnumerable<int> ThreadCountSource => BenchmarkSwitches.Parallelism;

        [GlobalSetup]
        public void Setup()
        {
            CacheOptions options = new CacheOptions()
            {
                CacheFields = true,
                CacheMethods = true,
                CacheTypes = true,

                CacheFieldNames = StringCaching.Cache,
                CacheMethodNames = StringCaching.Cache,
                CacheTypeNames = StringCaching.Cache,

                MaxDumpCacheSize = CacheSize,
                UseOSMemoryFeatures = UseOSMemoryFeatures,
            };

            _dataTarget = DataTarget.LoadDump(Program.CrashDump, options);
            _runtime = _dataTarget.ClrVersions.Single().CreateRuntime();
        }

        [IterationSetup]
        public void InitRunner()
        {
            _runner = new ThreadParallelRunner<ClrSegment>(Threads, _runtime.Heap.Segments);
            _runner.Setup();
        }

        [IterationCleanup]
        public void ClearCached()
        {
            _runtime.FlushCachedData();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _runtime.Dispose();
            _dataTarget?.Dispose();
        }

        [Benchmark]
        public void ParallelEnumerateHeapWithReferences()
        {
            _runner.Run(WalkSegment);
        }

        private static void WalkSegment(ClrSegment seg)
        {
            foreach (ClrObject obj in seg.EnumerateObjects().Take(2048))
            {
                foreach (ClrReference reference in obj.EnumerateReferencesWithFields(carefully: false, considerDependantHandles: true))
                {
                    _ = reference.Object;
                }
            }
        }
    }
}
