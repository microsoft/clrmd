# Copilot Instructions for ClrMD

## Project Overview

Microsoft.Diagnostics.Runtime (ClrMD) is a .NET library for inspecting crash dumps and live processes. It provides programmatic access to CLR internals (heap, types, threads, GC roots, exceptions) similar to SOS debugging extensions.

## Build & Test

```bash
# Restore (also installs the local .dotnet SDK):
./restore.sh

# Build the solution:
./build.sh

# Run all tests:
./test.sh

# Or use dotnet directly (SDK is in .dotnet/):
export PATH="$(git rev-parse --show-toplevel)/.dotnet:$PATH"
dotnet build Microsoft.Diagnostics.Runtime.sln
dotnet test src/Microsoft.Diagnostics.Runtime.Tests/Microsoft.Diagnostics.Runtime.Tests.csproj

# Run a single test:
dotnet test src/Microsoft.Diagnostics.Runtime.Tests --filter "FullyQualifiedName~TypeTests.GetObjectMethodTableTest"

# Run a test class:
dotnet test src/Microsoft.Diagnostics.Runtime.Tests --filter ClassName=Microsoft.Diagnostics.Runtime.Tests.TypeTests
```

**Test prerequisites:** Tests generate crash dump files on demand when first run. No manual pre-build step is needed — `DumpGenerator.cs` in the test project handles building test targets and capturing dumps lazily.

On Windows, dumps are generated for both .NET Core (via `DOTNET_DbgEnableMiniDump` env vars) and .NET Framework 4.8 (via DbgEng). Test target projects are multi-targeted (`net10.0;net48`).

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
- **Microsoft.Diagnostics.Runtime.Tests** — xUnit tests. Targets `net10.0`. Includes `DumpGenerator.cs` for lazy dump creation.
- **TestTargets** — Small test programs (solution folder, not built by default) that crash with unhandled exceptions to produce dumps. Multi-targeted `net10.0;net48`.

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
