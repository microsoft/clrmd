# Migrating from ClrMD 3.x to 4.0

This guide covers the breaking changes introduced in ClrMD 4.0 and how to update
your code.

---

## `CustomDataTarget` removed — use `DataTargetOptions`

The `CustomDataTarget` class has been removed.  All configuration is now done through
`DataTargetOptions`, which is passed to `DataTarget.LoadDump`, `DataTarget.AttachToProcess`,
`DataTarget.CreateSnapshotAndAttach`, and `DataTarget.CreateFromDbgEng`.

**v3:**
```csharp
CustomDataTarget customTarget = new(myDataReader)
{
    FileLocator = myLocator,
};
customTarget.CacheOptions.CacheTypes = false;

using DataTarget dt = customTarget.CreateDataTarget();
```

**v4:**
```csharp
DataTargetOptions options = new()
{
    FileLocator = myLocator,
    CacheOptions = new CacheOptions { CacheTypes = false },
};

using DataTarget dt = new DataTarget(myDataReader, options);
```

---

## `DataTarget` factory methods now take `DataTargetOptions`

All `DataTarget` static factory methods now accept `DataTargetOptions?` instead of
`CacheOptions?`.

**v3:**
```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new CacheOptions { ... });
```

**v4:**
```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    CacheOptions = new CacheOptions { ... },
    SymbolPaths = new[] { "https://msdl.microsoft.com/download/symbols" },
});
```

The `DataTargetOptions` class provides:

| Property | Description | Default |
|----------|-------------|---------|
| `CacheOptions` | Controls what ClrMD caches in memory | Default cache settings |
| `SymbolPaths` | Symbol server URLs to contact | `["https://msdl.microsoft.com/download/symbols"]` |
| `SymbolCachePath` | Local directory for downloaded symbols | `%TEMP%\symbols` |
| `SymbolTokenCredential` | `Azure.Core.TokenCredential` for authenticated symbol servers | `null` |
| `VerifyDacSignature` | Verify DAC Authenticode signature on Windows before loading | `true` |
| `FileLocator` | Custom `IFileLocator` (overrides built-in symbol chain) | Built-in symbol server chain |
| `ForceCompleteRuntimeEnumeration` | Search all modules for single-file runtimes | `false` |
| `TraceSymbolRequests` | Emit diagnostic traces for symbol resolution | `false` |
| `Limits` | Configurable parsing and network bounds (see below) | Default limits |

---

## Symbol server configuration changes

In v3, symbol server configuration was done via `_NT_SYMBOL_PATH`, `DataTarget.SetSymbolPath`,
or by providing a custom `IFileLocator`.

In v4, the primary way to configure symbol servers is through `DataTargetOptions`:

**v3:**
```csharp
// Option 1: environment variable
Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", "srv*C:\\symbols*https://msdl.microsoft.com/download/symbols");

// Option 2: programmatic
using DataTarget dt = DataTarget.LoadDump("crash.dmp");
dt.SetSymbolPath("srv*C:\\symbols*https://msdl.microsoft.com/download/symbols");
```

**v4:**
```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    SymbolPaths = new[] { "https://msdl.microsoft.com/download/symbols" },
    SymbolCachePath = @"C:\symbols",
});
```

`_NT_SYMBOL_PATH` is still supported as a fallback if the consumer does not provide
explicit symbol paths.

---

## Azure-based symbol server authentication

In v3, authenticated symbol servers (e.g. Symweb) used `DefaultAzureCredential`
automatically.  In v4, Symweb authentication uses `InteractiveBrowserCredential` by
default.  You can provide any `Azure.Core.TokenCredential` via
`DataTargetOptions.SymbolTokenCredential`:

**v4:**
```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    SymbolPaths = new[] { "https://symweb.azurefd.net/" },
    SymbolTokenCredential = new InteractiveBrowserCredential(),
});
```

---

## `IFileLocator` simplified — `FindElfImage` and `FindMachOImage` removed

The `IFileLocator` interface no longer has `FindElfImage` or `FindMachOImage` methods.
ClrMD does not download ELF or Mach-O binaries from symbol servers because there is
no mechanism to verify their integrity.  Only `FindPEImage` methods remain.

If you implemented `IFileLocator` in v3, remove those two methods from your
implementation.

**v3:**
```csharp
public class MyLocator : IFileLocator
{
    public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties) => ...;
    public string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties) => ...;
    public string? FindElfImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildId, bool checkProperties) => ...;
    public string? FindMachOImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> uuid, bool checkProperties) => ...;
}
```

**v4:**
```csharp
public class MyLocator : IFileLocator
{
    public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties) => ...;
    public string? FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID, OSPlatform originalPlatform, bool checkProperties) => ...;
}
```

On non-Windows platforms, the consumer is responsible for placing the DAC on disk
through whatever mechanism they trust and providing the path via
`ClrInfo.CreateRuntime(dacPath)`.

---

## DAC signature verification is now opt-out

In v3, DAC Authenticode verification was opt-in — the consumer had to explicitly pass
`verifySignature: true` to `CreateRuntime`.  In v4, verification is **on by default**
and controlled by `DataTargetOptions.VerifyDacSignature`.

The `verifySignature` parameter has been removed from all `ClrInfo.CreateRuntime`
overloads.

**v3:**
```csharp
// Opt-in to signature verification
ClrRuntime runtime = clrInfo.CreateRuntime(dacPath, ignoreMismatch: false, verifySignature: true);
```

**v4:**
```csharp
// Verification is on by default — nothing to change for most users.
ClrRuntime runtime = clrInfo.CreateRuntime();

// To disable for local dev builds:
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    VerifyDacSignature = false,
});
ClrRuntime runtime = dt.ClrVersions[0].CreateRuntime();
```

---

## `ClrObject` constructor is now internal

The `ClrObject` constructor was marked `[Obsolete]` in v3.1 and is now `internal` in
v4.  Use `ClrHeap.GetObject(ulong address)` instead.

**v3:**
```csharp
ClrObject obj = new ClrObject(address, type);
```

**v4:**
```csharp
ClrObject obj = heap.GetObject(address);
```

---

## `Command` and `CommandOptions` utilities removed

The `Command` and `CommandOptions` helper classes for launching external processes have
been removed from the library.  Use `System.Diagnostics.Process` directly if you need
this functionality.

---

## `DataTargetLimits` — configurable parsing and network bounds

ClrMD 4.0 introduces `DataTargetLimits`, a class that centralizes all upper bounds for
parsing and network operations.  These limits prevent excessive memory allocation or
processing time when reading untrusted or corrupted dump files.

All limits are configurable via `DataTargetOptions.Limits`:

```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    Limits = new DataTargetLimits
    {
        MaxMinidumpStreams = 50_000,         // Default: 10,000
        MaxFileDownloadSize = 100 * 1024 * 1024,  // Default: 16 MB
        SymbolTimeout = TimeSpan.FromSeconds(300), // Default: 180s
    },
});
```

Key defaults:

| Limit | Default |
|-------|---------|
| `MaxMinidumpStreams` | 10,000 |
| `MaxMinidumpMemoryRanges` | 10,000,000 |
| `MaxElfProgramHeaders` | 10,000 |
| `MaxMachOLoadCommands` | 10,000 |
| `MaxPESections` | 10,000 |
| `MaxThreads` | 20,000 |
| `MaxModules` | 100,000 |
| `MaxFileDownloadSize` | 16 MB |
| `SymbolTimeout` | 180 seconds |

See the `DataTargetLimits` class for the full list.

---

## macOS `CreateSnapshotAndAttach` support

`DataTarget.CreateSnapshotAndAttach` is now supported on macOS in addition to Windows
and Linux.  It creates a temporary coredump from the live process (similar to the
Linux behavior).

---

## cDAC support

ClrMD 4.0 adds support for the cDAC (contract-based DAC).  The universal DAC binary
is named `mscordaccore_universal` and is enumerated alongside the traditional
platform-specific DAC in `ClrInfo.DebuggingLibraries`.  No consumer action is required
— `CreateRuntime()` automatically tries available DAC binaries.

---

## Summary: quick reference

| v3 Pattern | v4 Replacement |
|-----------|---------------|
| `CustomDataTarget` | `DataTargetOptions` + `new DataTarget(reader, options)` |
| `DataTarget.LoadDump(path, cacheOptions)` | `DataTarget.LoadDump(path, dataTargetOptions)` |
| `DataTarget.SetSymbolPath(path)` | `DataTargetOptions.SymbolPaths` |
| `IFileLocator.FindElfImage(...)` | _(removed — not downloaded)_ |
| `IFileLocator.FindMachOImage(...)` | _(removed — not downloaded)_ |
| `CreateRuntime(dac, mismatch, verifySignature)` | `CreateRuntime(dac, mismatch)` + `DataTargetOptions.VerifyDacSignature` |
| `new ClrObject(address, type)` | `heap.GetObject(address)` |
| `Command` / `CommandOptions` | `System.Diagnostics.Process` |
