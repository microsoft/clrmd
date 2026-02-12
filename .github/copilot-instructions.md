# Copilot Instructions for ClrMD

## Project Overview

Microsoft.Diagnostics.Runtime (ClrMD) is a .NET library for inspecting crash dumps and live processes. It provides programmatic access to CLR internals (heap, types, threads, GC roots, exceptions) similar to SOS debugging extensions.

## Build & Test

```bash
# Restore (also installs the local .dotnet SDK):
./restore.sh     # Linux/macOS
.\Restore.cmd    # Windows

# Build the solution:
./build.sh       # Linux/macOS
.\Build.cmd      # Windows

# Run all tests:
./test.sh        # Linux/macOS
.\Test.cmd       # Windows

# Or use dotnet directly (SDK is in .dotnet/):
export PATH="$(git rev-parse --show-toplevel)/.dotnet:$PATH"
dotnet build Microsoft.Diagnostics.Runtime.sln
dotnet test src/Microsoft.Diagnostics.Runtime.Tests/Microsoft.Diagnostics.Runtime.Tests.csproj

# Run a single test:
dotnet test src/Microsoft.Diagnostics.Runtime.Tests --filter "FullyQualifiedName~TypeTests.GetObjectMethodTableTest"

# Run a test class:
dotnet test src/Microsoft.Diagnostics.Runtime.Tests --filter "FullyQualifiedName~MethodTests"
```

**Test prerequisites:** Tests require pre-generated crash dump files. You must build in this order:
```bash
# 1. Build TestTasks (the MSBuild task that captures crash dumps):
dotnet build src/TestTasks/TestTasks.csproj

# 2. Build TestTargets (compiles test programs and generates crash dumps):
#    The /p:Configuration must match the TestTasks build configuration.
dotnet msbuild src/TestTargets/TestTargets.proj /p:Configuration=Debug
```
If PDB-related build errors occur, delete stale files in `src/TestTargets/bin/` and rebuild.
Some dumps are pre-committed in `test_artifacts/`. Many tests will fail without the full set of generated dumps.

## Architecture

### Core object model (top-down)

```
DataTarget          → Entry point: loads a crash dump or attaches to a live process
 └─ ClrRuntime      → One CLR instance within the target
     ├─ ClrHeap     → Managed heap: enumerate objects, segments, roots
     │   └─ ClrObject   → Single heap object (readonly struct, by address + type)
     ├─ ClrAppDomain    → AppDomains (Desktop CLR) / managed context
     ├─ ClrModule       → Loaded assembly with metadata
     │   └─ ClrType     → Managed type: fields, methods, base type, interfaces
     └─ ClrThread       → Thread with stack frames and exception info
```

- `DataTarget` and `ClrRuntime` are `IDisposable` — always use `using`.
- `ClrObject`, `ClrValueType`, `ClrReference` are **readonly structs** (value-type wrappers around an address).
- `ClrHeap`, `ClrRuntime`, `ClrType` implementations are **sealed classes**.

### Platform abstraction

Platform-specific code lives in `src/Microsoft.Diagnostics.Runtime/src/{Windows,Linux,MacOS}/`. The `IDataReader` interface abstracts memory/module reading across:
- **Windows:** `WindowsProcessDataReader`, `MinidumpReader`
- **Linux:** `CoredumpReader` (ELF), `LinuxLiveDataReader`
- **macOS:** `MachOCoreReader`, `MacOSProcessDataReader`

### Key internal layers

- `AbstractDac/` — Platform-independent DAC interface abstraction
- `DacInterface/` — COM interop wrappers for the CLR debugging API (DAC)
- `DacImplementation/` — Concrete DAC implementations
- `Implementation/` — Type factories, symbol servers, PE image readers
- `DataReaders/` — `IDataReader` implementations per format

### Projects

- **Microsoft.Diagnostics.Runtime** — Core library. Multi-targets `netstandard2.0` + `net8.0`.
- **Microsoft.Diagnostics.Runtime.Utilities** — DbgEng COM wrappers. Targets `net8.0`.
- **Microsoft.Diagnostics.Runtime.Tests** — xUnit tests. Targets `net8.0`.
- **TestTasks** — Custom MSBuild task that generates crash dumps from test target executables.
- **TestTargets** — MSBuild project (`.proj`, not `.csproj`) that compiles small test programs and creates dumps from them.

## Conventions

### Naming (enforced by .editorconfig)
- Private/internal fields: `_camelCase`
- Static private/internal fields: `s_camelCase`
- Constants: `PascalCase`

### Code style
- C# latest language version with `unsafe` blocks allowed (required for interop).
- Nullable reference types are enabled globally.
- Prefer `is null` / `is not null` over `== null` / `!= null`.
- Prefer null coalescing (`??`), null propagation (`?.`), and pattern matching.
- Max line length: 180 characters.

### Test patterns
- Tests use **xUnit** with `AutoFixture`. Test parallelization is **disabled** globally.
- Platform-specific tests use `[WindowsFact]`, `[LinuxFact]`, or `[CoreFact]` attributes instead of `[Fact]`.
- Test targets are loaded via the `TestTargets` static class:
  ```csharp
  using DataTarget dt = TestTargets.Types.LoadFullDump();
  using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
  ```
- Fixture-based tests use `IClassFixture<ObjectConnection<T>>` to share a loaded dump across tests in a class.
- Dump file naming: `{TargetName}_{wks|svr}[_mini].dmp`

### IDisposable
- `DataTarget` and `ClrRuntime` must be disposed. Check `_disposed` and throw `ObjectDisposedException` on use-after-dispose.

### Immutability
- Heap value wrappers (`ClrObject`, `ClrValueType`, `ClrReference`) are `readonly struct` implementing `IEquatable<T>`.
- Stateful classes (`ClrHeap`, `ClrRuntime`) are `sealed` with lazy initialization via `Lazy<T>` or `volatile` fields.
