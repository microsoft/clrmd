# Copilot Instructions for ClrMD

## Project Overview

Microsoft.Diagnostics.Runtime (ClrMD) is a .NET library for inspecting crash dumps and live processes. It provides programmatic access to CLR internals (heap, types, threads, GC roots, exceptions) similar to SOS debugging extensions.

## Build & Test

```bash
# Build the solution (does NOT build test targets or generate dumps):
dotnet build Microsoft.Diagnostics.Runtime.sln

# Run all tests (both 32-bit and 64-bit):
./Test.cmd          # Windows
./test.sh           # Linux/macOS

# Run only 64-bit or 32-bit tests:
./Test.cmd x64
./Test.cmd x86      # also accepts: x32, arm, amd64, arm64

# Run with a test filter:
./Test.cmd x64 --filter TypeTests
./Test.cmd x86 --filter "FullyQualifiedName~HeapTests.ServerNoCorruption"

# Or use dotnet directly (runs in current process architecture):
dotnet test src/Microsoft.Diagnostics.Runtime.Tests/Microsoft.Diagnostics.Runtime.Tests.csproj
dotnet test src/Microsoft.Diagnostics.Runtime.Tests --filter ClassName=Microsoft.Diagnostics.Runtime.Tests.TypeTests
```

**Important:** ClrMD loads architecture-specific native components (DAC), so the test host must match the dump architecture. Use `--arch x86` to test 32-bit dumps and `--arch x64` for 64-bit. The `Test.cmd`/`test.sh` scripts handle this automatically.

**Dump generation:** Tests generate crash dumps lazily on first run — no manual pre-build step needed. `DumpGenerator.cs` in the test project builds test targets for the required architecture and captures dumps automatically:
- **.NET Core targets:** Built with `dotnet build`, dumps captured via `DOTNET_DbgEnableMiniDump` environment variables
- **.NET Framework 4.8 targets (Windows):** Built with `dotnet build -f net48`, dumps captured via DbgEng

Dumps are cached in `src/TestTargets/bin/{x86|x64}/` and reused across test runs. Delete the `bin/` directory to force regeneration.

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

- **Microsoft.Diagnostics.Runtime** — Core library. Multi-targets `netstandard2.0` + `net10.0`.
- **Microsoft.Diagnostics.Runtime.Utilities** — DbgEng COM wrappers. Targets `net10.0`.
- **Microsoft.Diagnostics.Runtime.Tests** — xUnit tests. Targets `net10.0`. Includes `DumpGenerator.cs` for lazy dump creation and `TestTargets.cs` for loading dumps.
- **TestTargets** (solution folder, not built by default) — Small test programs that crash with unhandled exceptions to produce dumps. Multi-targeted `net10.0;net48`. Each target has its own `.csproj` and references `SharedLibrary`.

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
- Platform-specific tests use `[WindowsFact]`, `[LinuxFact]`, `[FrameworkFact]`, or `[CoreFact]` attributes instead of `[Fact]`.
  - `[FrameworkFact]` — Only runs on Windows (tests .NET Framework 4.8 dumps, e.g., AppDomains)
  - `[WindowsFact]` — Only runs on Windows (tests Windows-specific features)
- Test targets are loaded via the `TestTargets` static class. Dumps are generated lazily on first access:
  ```csharp
  using DataTarget dt = TestTargets.Types.LoadFullDump();
  using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
  ```
- Fixture-based tests use `IClassFixture<ObjectConnection<T>>` to share a loaded dump across tests in a class.
- Dump file naming: `{TargetName}_{wks|svr}[_mini][_net48].dmp` (in `src/TestTargets/bin/{arch}/`)
### IDisposable
- `DataTarget` and `ClrRuntime` must be disposed. Check `_disposed` and throw `ObjectDisposedException` on use-after-dispose.

### Immutability
- Heap value wrappers (`ClrObject`, `ClrValueType`, `ClrReference`) are `readonly struct` implementing `IEquatable<T>`.
- Stateful classes (`ClrHeap`, `ClrRuntime`) are `sealed` with lazy initialization via `Lazy<T>` or `volatile` fields.
