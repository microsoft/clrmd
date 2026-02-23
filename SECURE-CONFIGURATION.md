# Secure Configuration Guidelines: Microsoft.Diagnostics.Runtime (ClrMD)

This document describes how to configure ClrMD securely, the security implications
of each configuration option, and best practices for production deployments.

## Overview

ClrMD is a .NET library for inspecting crash dumps and live processes.  It loads
and executes a native DAC library, contacts symbol servers over the network, and
parses untrusted binary file formats.  These operations have security implications
that you should understand before deploying tools built on ClrMD.

By default, ClrMD is configured for secure operation:

- DAC binaries are Authenticode-verified on Windows before loading
- DAC binaries are not downloaded on non-Windows platforms
- Symbol server downloads are bounded in size and time
- All file parsers enforce upper bounds on structure counts
- The default symbol server is Microsoft's public HTTPS endpoint

You only need to change defaults if your environment has specific requirements.

---

## DAC Signature Verification

**What it does:** Before loading a DAC binary (`mscordaccore.dll`), ClrMD verifies
its Authenticode signature, including the Microsoft root CA chain and a DAC-specific
Enhanced Key Usage OID.  This prevents loading tampered or attacker-supplied native
code.

**Default:** Enabled (`DataTargetOptions.VerifyDacSignature = true`).

**When to disable:** Only when debugging with a locally-built .NET runtime whose
DAC is not signed.  This is a development-only scenario.

**Risk of disabling:** ClrMD will load and execute any native DLL as the DAC without
integrity verification.  A malicious DAC could execute arbitrary code in your process.
**Never disable signature verification in production tooling or services that process
untrusted dumps.**

```csharp
// DEVELOPMENT ONLY — never do this in production
var options = new DataTargetOptions { VerifyDacSignature = false };
```

**Non-Windows platforms:** Authenticode verification is not available on Linux or
macOS.  ClrMD does not download DAC binaries on these platforms at all.  If you provide
a DAC path directly via `ClrInfo.CreateRuntime(dacPath)`, you are responsible for
verifying the binary through your own mechanism before providing it.

---

## Symbol Server Configuration

ClrMD contacts symbol servers to download DAC binaries (Windows only) and IL images
(all platforms, read-only data — never executed).

### Network Endpoints

**Default:** `https://msdl.microsoft.com/download/symbols` (HTTPS).

**Configuring custom servers:**
```csharp
var options = new DataTargetOptions
{
    SymbolPaths = new[] { "https://msdl.microsoft.com/download/symbols" },
};
```

**Security implications:**
- ClrMD will contact every URL in `SymbolPaths`.  Only configure endpoints you trust.
- If you configure an HTTP (non-TLS) endpoint, downloads are not encrypted and are
  subject to man-in-the-middle attacks.  **Always use HTTPS.**
- A malicious symbol server could serve a tampered DAC, but on Windows the Authenticode
  check (enabled by default) will reject it.  IL images downloaded from symbol servers
  are used as read-only data sources and are never loaded as executable code.

### Authenticated Servers (Symweb)

For Azure-hosted symbol servers like Symweb, provide a `TokenCredential`:

```csharp
var options = new DataTargetOptions
{
    SymbolPaths = new[] { "https://symweb.azurefd.net/" },
    SymbolTokenCredential = new InteractiveBrowserCredential(),
};
```

If no credential is provided and a Symweb URL is configured, ClrMD defaults to
`InteractiveBrowserCredential`, which will prompt for interactive login.  In headless
or service environments, provide a `ManagedIdentityCredential`,
`ClientSecretCredential`, or other appropriate credential type.

### Download Limits

**Timeout:** All HTTP operations have a default timeout of **180 seconds**
(`DataTargetLimits.SymbolTimeout`).  If a server does not respond or a transfer
stalls, the operation is cancelled.

**File size:** Downloads are capped at **16 MB** by default
(`DataTargetLimits.MaxFileDownloadSize`).  Content exceeding the limit is truncated
and saved as a `.partial` file in the cache.  This prevents a malicious or
misconfigured server from filling your disk.

**Adjusting limits:**
```csharp
var options = new DataTargetOptions
{
    Limits = new DataTargetLimits
    {
        SymbolTimeout = TimeSpan.FromSeconds(60),
        MaxFileDownloadSize = 50 * 1024 * 1024, // 50 MB
    },
};
```

**Risk of increasing limits:** Larger download limits increase exposure to disk
exhaustion attacks from malicious servers.  Longer timeouts increase exposure to
slowloris-style denial of service.

### Symbol Cache Directory

**Default:** `%TEMP%\symbols` (`DataTargetOptions.SymbolCachePath`).

Downloaded files are stored here.  ClrMD sanitizes cache keys using
`Path.GetFileName` to prevent directory traversal.  Ensure this directory has
appropriate filesystem permissions — files cached here include DAC binaries that
will be loaded as executable code on Windows.

---

## Parsing Limits

ClrMD parses untrusted binary formats: Minidump, ELF coredump, Mach-O coredump,
and PE images.  All parsing loops enforce configurable upper bounds via
`DataTargetLimits`.

**Default limits** are chosen to handle all known legitimate dumps with margin while
preventing resource exhaustion from crafted inputs:

| Category | Limit | Default |
|----------|-------|---------|
| Minidump streams | `MaxMinidumpStreams` | 10,000 |
| Minidump memory ranges | `MaxMinidumpMemoryRanges` | 10,000,000 |
| ELF program headers | `MaxElfProgramHeaders` | 10,000 |
| ELF file table entries | `MaxElfFileTableEntries` | 10,000 |
| ELF auxiliary vector entries | `MaxElfAuxvEntries` | 10,000 |
| ELF GNU hash chain length | `MaxElfGnuHashChainLength` | 100,000 |
| Mach-O load commands | `MaxMachOLoadCommands` | 10,000 |
| Mach-O symbols | `MaxMachOSymbols` | 10,000,000 |
| PE sections | `MaxPESections` | 10,000 |
| PE relocations | `MaxPERelocations` | 10,000,000 |
| PE export names | `MaxPEExportNames` | 1,000,000 |
| PE debug directories | `MaxPEDebugDirectories` | 10,000 |
| Threads | `MaxThreads` | 20,000 |
| Stack frames per thread | `MaxStackFrames` | 8,096 |
| Modules | `MaxModules` | 100,000 |
| App domains | `MaxAppDomains` | 10,000 |

Exceeding any limit throws `InvalidDataException`.

**Hardening for untrusted inputs:** If you process dumps from untrusted sources,
consider lowering limits to match your expected workload:

```csharp
var options = new DataTargetOptions
{
    Limits = new DataTargetLimits
    {
        MaxThreads = 5_000,
        MaxModules = 10_000,
    },
};
```

---

## Process Attachment Privileges

ClrMD attaches to live processes using OS-level APIs:

| Platform | API | Required Privilege |
|----------|-----|-------------------|
| Windows | `OpenProcess` + `ReadProcessMemory` | `PROCESS_VM_READ` (or debug privilege for elevated processes) |
| Linux | `process_vm_readv` / `ptrace` | Same UID or `CAP_SYS_PTRACE` |
| macOS | `mach_vm_read` | Task port access |

ClrMD never elevates privileges.  It reads memory in a read-only fashion and does
not inject code or write to process memory.  If process suspension is requested,
ClrMD suspends the target during attachment and resumes it on `DataTarget.Dispose`.

**Best practices:**
- Run ClrMD-based tools with the minimum privileges needed.  Do not run as root or
  administrator unless required to attach to the target process.
- On Linux, prefer `CAP_SYS_PTRACE` over running as root.
- Always dispose `DataTarget` to ensure suspended processes are resumed.

---

## Processing Untrusted Dumps

The DAC (loaded by ClrMD) may enter infinite loops, crash, or exhibit undefined
behavior when processing corrupted or malicious data.  ClrMD cannot cancel a DAC
call that does not return.

**Recommendations for services that process untrusted dumps:**

1. **Impose an external timeout** — wrap ClrMD processing in a timeout mechanism
   (e.g. `CancellationToken`, process-level watchdog).  Azure Watson uses a 5-minute
   timeout.
2. **Run in a sandbox** — use a container, AppContainer, or other isolation mechanism
   with no outbound network access.
3. **Limit resources** — constrain memory and CPU using OS resource limits (cgroups,
   Job objects).
4. **Lower parsing limits** — tighten `DataTargetLimits` to match your expected
   workload.

---

## Dump File Confidentiality

Crash dumps and live process memory may contain credentials, cryptographic keys,
PII, and any other data present in memory at capture time.  ClrMD makes it easy to
enumerate and inspect this data — that is its purpose.

**Best practices:**
- Restrict access to dump files using filesystem permissions and encryption at rest.
- Do not store dumps in publicly accessible locations.
- Treat ClrMD output (type names, field values, exception messages) as potentially
  containing sensitive data.
- Comply with your organization's data handling policies for HBI/customer data.

---

## Auditing

ClrMD does not produce audit logs.  If you need to track dump analysis operations,
implement logging in your tool around ClrMD calls.

Enable `DataTargetOptions.TraceSymbolRequests = true` to trace symbol server requests
to `System.Diagnostics.Trace`.  This is useful for debugging symbol resolution but
may produce large output.

---

## Backward Compatibility

ClrMD 4.0 changed several security-relevant defaults from prior versions:

| Setting | v3 (less secure) | v4 (secure default) |
|---------|------------------|---------------------|
| DAC signature verification | Opt-in (`verifySignature: false` by default) | Opt-out (`VerifyDacSignature = true` by default) |
| Non-Windows DAC download | Allowed via `FindElfImage`/`FindMachOImage` | Removed from API entirely |
| Download size limit | None | 16 MB default |
| Download timeout | Implicit HttpClient default (100s) | Explicit 180s default |
| Parser loop bounds | Partial (some hardcoded) | Comprehensive, configurable via `DataTargetLimits` |

If you are upgrading from v3, see the [v4 migration guide](doc/Migrating4.md) for
details on all breaking changes.

---

## Summary: Recommended Production Configuration

```csharp
// Secure defaults — no changes needed for most scenarios
using DataTarget dt = DataTarget.LoadDump("crash.dmp");

// Explicit secure configuration for production services
using DataTarget dt = DataTarget.LoadDump("crash.dmp", new DataTargetOptions
{
    // Use HTTPS symbol servers only
    SymbolPaths = new[] { "https://msdl.microsoft.com/download/symbols" },

    // DAC signature verification is on by default — do not disable
    VerifyDacSignature = true,

    // Tighten limits for untrusted input
    Limits = new DataTargetLimits
    {
        MaxFileDownloadSize = 16 * 1024 * 1024,
        SymbolTimeout = TimeSpan.FromSeconds(180),
    },
});

ClrRuntime runtime = dt.ClrVersions[0].CreateRuntime();
```
