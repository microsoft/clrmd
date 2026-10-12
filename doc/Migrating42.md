# Migrating to ClrMD 4.2

ClrMD 4.2 updates automatic DAC discovery and the default symbol cache location.

## The default symbol cache is a per-user directory

If `DataTargetOptions.SymbolCachePath` is not set, ClrMD caches downloaded symbol
files under the current user's local application data directory:

- Windows: `%LOCALAPPDATA%\Microsoft\ClrMD\symbols`
- Linux: `$XDG_DATA_HOME/Microsoft.ClrMD/symbols`, or
  `~/.local/share/Microsoft.ClrMD/symbols` when `XDG_DATA_HOME` is not set.
- macOS: `~/Library/Application Support/Microsoft.ClrMD/symbols`.

On Unix, ClrMD creates its directories with mode `0700` and re-establishes that
mode on both `Microsoft.ClrMD` and `symbols` before reading or writing the default
cache. Those two directories must not be symbolic links. Existing application
data directories retain their permissions.

Applications that ran without a per-user profile and relied on the previous default
must now set `SymbolCachePath` to a directory that only the analyzing account can
modify:

```csharp
DataTargetOptions options = new() { SymbolCachePath = "/var/cache/my-analyzer/symbols" };
using DataTarget target = DataTarget.LoadDump("crash.dmp", options);
```

Explicitly configured caches, including caches named in a symbol path, are used as
given. ClrMD treats them as trusted caller configuration.

## DACs beside the runtime are no longer discovered automatically

ClrMD 4.2 does not probe the runtime module directory. Normal symbol-server and
file-locator resolution is unchanged. This may affect applications that deploy a DAC
beside the runtime and rely on automatic discovery.

Applications that intentionally deploy a trusted DAC beside the runtime must now pass
its path explicitly:

```csharp
using DataTarget target = DataTarget.LoadDump("crash.dmp");
ClrInfo info = target.ClrVersions[0];
using ClrRuntime runtime = info.CreateRuntime("/trusted/runtime/libmscordaccore.so");
```

The caller is responsible for authenticating explicitly supplied DACs on Linux and
macOS. On Windows, leave `DataTargetOptions.VerifyDacOnWindows` enabled unless the DAC
comes from a development build whose provenance you have independently established.
