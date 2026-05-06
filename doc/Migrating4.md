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
| `UseLockFreeMemoryMapReader` | Wrap file-based dumps with a lock-free, memory-mapped reader (see below) | `false` |
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

`_NT_SYMBOL_PATH` is **no longer parsed** in v4. If `DataTargetOptions` is not supplied,
ClrMD defaults to a single symbol path pointing at the public Microsoft symbol server
(`https://msdl.microsoft.com/download/symbols`) with a cache under
`Path.Combine(Path.GetTempPath(), "symbols")`.

To restore v3-like behavior (read `_NT_SYMBOL_PATH` and use it as the symbol path),
parse the environment variable yourself and feed the results into `DataTargetOptions`.
A typical sketch:

```csharp
static DataTargetOptions BuildOptionsFromNtSymbolPath()
{
    string? ntSymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");

    // _NT_SYMBOL_PATH is a semicolon-separated list of entries. Each entry can be a
    // local directory or a "srv*[downstream-cache]*<server>" element. ClrMD v4 wants
    // the server URLs in SymbolPaths and a single local cache in SymbolCachePath.
    List<string> servers = new();
    string? cache = null;

    foreach (string entry in (ntSymbolPath ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        if (entry.StartsWith("srv*", StringComparison.OrdinalIgnoreCase) ||
            entry.StartsWith("symsrv*", StringComparison.OrdinalIgnoreCase))
        {
            // srv*[cache1*cache2*...]*<server-url>
            string[] parts = entry.Split('*', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                servers.Add(parts[^1]);
                if (parts.Length >= 3 && cache is null)
                    cache = parts[1]; // first downstream cache wins
            }
        }
        else
        {
            // Plain local directory acts as both a search location and a cache.
            servers.Add(entry);
            cache ??= entry;
        }
    }

    if (servers.Count == 0)
        servers.Add("https://msdl.microsoft.com/download/symbols");

    return new DataTargetOptions
    {
        SymbolPaths = servers.ToArray(),
        SymbolCachePath = cache ?? Path.Combine(Path.GetTempPath(), "symbols"),
    };
}

using DataTarget dt = DataTarget.LoadDump("crash.dmp", BuildOptionsFromNtSymbolPath());
```

If you only need to point at a private/UNC server or a custom cache, set
`DataTargetOptions.SymbolPaths` and `DataTargetOptions.SymbolCachePath` directly — there
is no need to round-trip through `_NT_SYMBOL_PATH`. For full control over how binaries
are resolved (for example, to layer in your own search logic), assign a custom
`DataTargetOptions.FileLocator`; when set, `SymbolPaths` and `SymbolCachePath` are
ignored.

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

## New: lock-free memory-mapped data reader (opt-in)

ClrMD 4.0 adds an opt-in, single-threaded data reader that satisfies memory reads
directly from a memory-mapped view of the dump file. It eliminates per-read locks and
stream seeks compared to the default minidump readers, which can substantially speed up
sequential read patterns such as heap walks and GC root traversal.

This feature is **off by default**. Enable it via `DataTargetOptions.UseLockFreeMemoryMapReader`:

```csharp
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    UseLockFreeMemoryMapReader = true,
});
```

**Trade-offs:**

- The resulting `IDataReader` is **not thread safe**. `ClrRuntime.IsThreadSafe` will
  return `false`, and callers must serialize all access to the `DataTarget`,
  `ClrRuntime`, and `ClrHeap` themselves.
- Only takes effect when loading a dump from a file path (Minidump, ELF coredump, or
  Mach-O coredump). Has no effect for stream-based dump loads or live process targets.
- The entire dump file is memory-mapped into the current process. On a 32-bit host,
  user-mode address space is ~2 GB, so loading a large dump from a 32-bit process will
  fail with `OutOfMemoryException`. Run as a 64-bit process for large dumps, or leave
  this option disabled.

---

## Removed: `CacheOptions.UseOSMemoryFeatures`

The `CacheOptions.UseOSMemoryFeatures` flag (and its backing AWE-based minidump
reader) has been removed.  The flag enabled an Address Windowing Extensions (AWE)
cache that required `SeLockMemoryPrivilege` — a privilege that is not granted by
default and required specialty machine setup on Windows.  Combined with being
under-maintained, the AWE path was removed in v4.

The property is still present and marked `[Obsolete]` for source compatibility,
but it has no effect.  Setting it in v4 will produce a compiler warning.

If you previously set this flag for performance reasons and do not need a thread
safe `IDataReader`, set `DataTargetOptions.UseLockFreeMemoryMapReader = true`
instead for a speed improvement on file-based dumps (see the section above for
details and trade-offs).

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
| `CacheOptions.UseOSMemoryFeatures` | _(removed — set `DataTargetOptions.UseLockFreeMemoryMapReader = true` for a speed boost if thread safety is not required)_ |
