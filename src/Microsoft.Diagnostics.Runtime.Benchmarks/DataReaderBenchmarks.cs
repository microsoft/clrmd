// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Diagnostics.Runtime.Tests;

namespace Microsoft.Diagnostics.Runtime.Benchmarks;

/// <summary>
/// Compares ClrMD's default reader against the opt-in lock-free MMF reader on
/// the three workloads measured by hand on the 7.6 GB dump:
///   - Root enumeration (<see cref="ClrHeap.EnumerateRoots"/>)
///   - Sequential heap walk (<see cref="ClrHeap.EnumerateObjects()"/>)
///   - Live-object walk (mark-style traversal from every root, following
///     references carefully and via dependent handles)
///
/// The GCRoot test target is used because it ships a small, deterministic
/// object graph that exercises every code path under both readers without
/// requiring a multi-GB dump on the developer's machine.
///
/// Uses the InProcess toolchain because Arcade's auto-generated assembly
/// version (4.0.&lt;long-build-counter&gt;.0) trips up BenchmarkDotNet's separate-
/// process boilerplate. InProcess gives us correct numbers; for cross-process
/// memory isolation (working-set comparisons) the standalone harness in
/// .bench/LockFreeBench is the better tool.
/// </summary>
[Config(typeof(InProcessConfig))]
[MemoryDiagnoser]
public class DataReaderBenchmarks
{
    private class InProcessConfig : BenchmarkDotNet.Configs.ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithStrategy(RunStrategy.Throughput)
                .WithWarmupCount(2)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
        }
    }
    private string _dumpPath = null!;
    private DataTarget _dataTarget = null!;
    private ClrRuntime _runtime = null!;
    private ClrHeap _heap = null!;

    [Params(DataReaderKind.Standard, DataReaderKind.LockFreeMmf)]
    public DataReaderKind Reader { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Generate the GCRoot dump on disk if needed (first run only).
        _dumpPath = TestTargets.GCRoot.EnsureFullDumpPath();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        DataTargetOptions opts = new()
        {
            UseLockFreeMemoryMapReader = Reader == DataReaderKind.LockFreeMmf,
            VerifyDacOnWindows = false,
        };

        _dataTarget = DataTarget.LoadDump(_dumpPath, opts);
        _runtime = _dataTarget.ClrVersions.Single().CreateRuntime();
        _heap = _runtime.Heap;
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _runtime.Dispose();
        _dataTarget.Dispose();
    }

    [Benchmark(Description = "EnumerateRoots")]
    public int RootEnumeration()
    {
        int count = 0;
        foreach (ClrRoot _ in _heap.EnumerateRoots())
            count++;
        return count;
    }

    [Benchmark(Description = "EnumerateObjects (sequential)")]
    public long SequentialHeapWalk()
    {
        long bytes = 0;
        foreach (ClrObject obj in _heap.EnumerateObjects())
            bytes += (long)obj.Size;
        return bytes;
    }

    [Benchmark(Description = "Live-object walk (carefully+dependent)")]
    public int LiveObjectWalk()
    {
        HashSet<ulong> seen = new();
        Stack<ulong> stack = new();

        foreach (ClrRoot root in _heap.EnumerateRoots())
        {
            ulong addr = root.Object.Address;
            if (addr != 0 && seen.Add(addr))
                stack.Push(addr);
        }

        while (stack.Count > 0)
        {
            ulong addr = stack.Pop();
            ClrObject obj = _heap.GetObject(addr);
            if (!obj.IsValid)
                continue;

            foreach (ClrObject child in obj.EnumerateReferences(carefully: true, considerDependantHandles: true))
            {
                if (child.Address != 0 && seen.Add(child.Address))
                    stack.Push(child.Address);
            }
        }

        return seen.Count;
    }
}
