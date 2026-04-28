// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Core;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime;

public class DataTargetOptions
{
    /// <summary>
    ///  An optional set of cache options.  Returning null from this property will use ClrMD's default
    ///  cache options.
    /// </summary>
    public CacheOptions CacheOptions
    {
        get => field ??= new CacheOptions();
        init;
    }

    /// <summary>
    /// Overrides ClrMD's file locator.  This file locator will be used to find any missing binaries (Dac, PE images,
    /// etc) needed to analyze the target process.
    /// </summary>
    public IFileLocator FileLocator
    {
        init;
        get
        {
            if (field is null)
            {
                Directory.CreateDirectory(SymbolCachePath);
                FileSymbolCache cache = new(SymbolCachePath);
                IEnumerable<SymbolServer> servers = SymbolPaths.Select(r =>
                                                        new SymbolServer(cache, r, TraceSymbolRequests, SymbolTokenCredential));
                field = new SymbolGroup(servers);
                return field;
            }

            return field;
        }
    }

    /// <summary>
    /// Gets or sets the directory path used to cache downloaded symbol files.
    /// </summary>
    /// <remarks>If not explicitly set, the property defaults to a temporary directory named "symbols" within
    /// the system's temporary path. This path is used to store symbol files retrieved during symbol
    /// resolution.</remarks>
    public string SymbolCachePath
    {
        init;
        get => field ??= Path.Combine(Path.GetTempPath(), "symbols");
    }

    /// <summary>
    /// The symbol path used by the default file locator if <see cref="FileLocator"/> is null.  If
    /// <see cref="FileLocator"/> is non-null, this property has no effect.
    /// </summary>
    public string[] SymbolPaths { get; init; } = ["https://msdl.microsoft.com/download/symbols"];

    /// <summary>
    /// If true, all runtimes in the target process will be enumerated.  This enables us to find single-file runtimes
    /// that may not be discovered through the default enumeration process.  Enabling this option is significantly
    /// more expensive, so it is not enabled by default.
    /// </summary>
    public bool ForceCompleteRuntimeEnumeration { get; set; }

    /// <summary>
    /// The TokenCredential to use for any Azure based symbol servers (set to null if not using one).
    /// </summary>
    public TokenCredential? SymbolTokenCredential { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether detailed information about symbol requests is traced during execution.
    /// </summary>
    /// <remarks>When enabled, this property allows the tracing of all symbol resolution requests, which can
    /// be useful for debugging or analyzing symbol loading behavior. Tracing may produce a large amount of output and
    /// could impact performance.</remarks>
    public bool TraceSymbolRequests { get; init; }

    /// <summary>
    /// Safety limits for parsing and enumeration operations. These limits prevent excessive memory
    /// allocation and processing when reading untrusted or corrupted dump files.
    /// </summary>
    public DataTargetLimits Limits
    {
        get => field ??= new DataTargetLimits();
        init;
    }

    /// <summary>
    /// On Windows, this property determines if we verify the signature of the dac before downloading/loading it.  This is enabled
    /// by default, but can be disabled to allow loading of a DAC that doesn't have a signature (such as building CLR
    /// from source).  This only affects dacs with a signature.
    /// Note that disabling this option can lead to security risks, so it should only be disabled if you understand the implications.
    /// </summary>
    public bool VerifyDacOnWindows { get; init; } = true;

    /// <summary>
    /// When true, <see cref="DataTarget.LoadDump(string, DataTargetOptions?)"/> will wrap the underlying
    /// dump reader with a single-threaded, lock-free, memory-mapped data reader. This trades thread safety
    /// for significantly faster sequential read patterns (heap walks, GC root traversal).
    ///
    /// <para>
    /// Only takes effect when loading a dump from a file path (Minidump, ELF coredump, or Mach-O coredump).
    /// Has no effect for stream-based dump loads or for live process targets.
    /// </para>
    ///
    /// <para>
    /// Default is <c>false</c>. Setting this to <c>true</c> means the resulting
    /// <see cref="IDataReader"/> is NOT thread safe; callers must serialize all access to the
    /// <see cref="DataTarget"/>, <see cref="ClrRuntime"/>, and <see cref="ClrHeap"/>.
    /// </para>
    ///
    /// <para>
    /// <b>32-bit warning:</b> This option memory-maps the entire dump file. On a 32-bit process
    /// the user-mode address space is ~2 GB, so dumps approaching or exceeding that size will fail
    /// to map and produce an <see cref="System.OutOfMemoryException"/>. Disable this option (or run
    /// as a 64-bit process) when loading large dumps from a 32-bit host.
    /// </para>
    /// </summary>
    public bool UseLockFreeMemoryMapReader { get; init; }
}
