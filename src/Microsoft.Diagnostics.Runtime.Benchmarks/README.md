# Microsoft.Diagnostics.Runtime.Benchmarks

[BenchmarkDotNet][bdn] benchmarks comparing ClrMD's default reader against the
opt-in lock-free MMF reader (`DataTargetOptions.UseLockFreeMemoryMapReader`).

## What's measured

`DataReaderBenchmarks` runs three workloads against the `GCRoot` test target's
full dump:

1. **EnumerateRoots** — `ClrHeap.EnumerateRoots()`
2. **EnumerateObjects (sequential)** — full sequential heap walk via
   `ClrHeap.EnumerateObjects()`
3. **Live-object walk (carefully+dependent)** — mark-style traversal seeded
   from every root, following references with `carefully:true` and
   `considerDependantHandles:true`

Each workload is run twice — once per `DataReaderKind` (`Standard`,
`LockFreeMmf`) — with `MemoryDiagnoser` enabled.

## Running

```pwsh
cd src\Microsoft.Diagnostics.Runtime.Benchmarks
dotnet run -c Release -- --filter '*'
```

The first run will trigger generation of the GCRoot test target's dump (via
`TestTargets.GCRoot.EnsureFullDumpPath()`); subsequent runs reuse the cached
dump under `src\TestTargets\bin\<arch>\`.

Filter to a single workload:

```pwsh
dotnet run -c Release -- --filter '*EnumerateRoots*'
```

## Toolchain

The benchmarks use `InProcessNoEmitToolchain` rather than BenchmarkDotNet's
default separate-process toolchain. Arcade auto-generates a 4-part assembly
version where the third part exceeds `ushort.MaxValue`, which trips the
auto-generated boilerplate's `[assembly: AssemblyVersion]` (CS7034). InProcess
sidesteps that pipeline.

If you want true cross-process working-set comparisons (the kind we ran on the
7.6 GB lock-contention dump), use the standalone harness in `.bench/LockFreeBench`
which spawns a fresh process per scenario × reader cell.

[bdn]: https://benchmarkdotnet.org/
