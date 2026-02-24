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
}
