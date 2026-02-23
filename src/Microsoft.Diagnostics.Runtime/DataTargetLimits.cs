// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime;

/// <summary>
/// Defines upper bounds for parsing and enumeration operations. These limits prevent excessive
/// memory allocation and processing time when reading untrusted or corrupted dump files.
/// All properties use <c>get; init;</c> so they cannot be changed after construction.
/// </summary>
public sealed class DataTargetLimits
{
    // ──────────────────────────────────────────────
    //  Minidump limits
    // ──────────────────────────────────────────────

    /// <summary>Maximum number of streams allowed in a Minidump header.</summary>
    public int MaxMinidumpStreams { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum number of memory ranges (MemoryListStream / Memory64ListStream) in a Minidump.</summary>
    public int MaxMinidumpMemoryRanges { get => field; init => field = ValidatePositive(value); } = 10_000_000;

    // ──────────────────────────────────────────────
    //  Mach-O limits
    // ──────────────────────────────────────────────

    /// <summary>Maximum number of load commands in a Mach-O header.</summary>
    public int MaxMachOLoadCommands { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum number of symbols in a Mach-O symbol table.</summary>
    public int MaxMachOSymbols { get => field; init => field = ValidatePositive(value); } = 10_000_000;

    /// <summary>Maximum length of an ASCII string read from a Mach-O core dump.</summary>
    public int MaxMachOAsciiLength { get => field; init => field = ValidatePositive(value); } = 4_096;

    // ──────────────────────────────────────────────
    //  ELF limits
    // ──────────────────────────────────────────────

    /// <summary>Maximum number of program headers in an ELF file.</summary>
    public int MaxElfProgramHeaders { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum number of file table entries in an ELF core dump.</summary>
    public int MaxElfFileTableEntries { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum number of auxiliary vector entries in an ELF core dump.</summary>
    public int MaxElfAuxvEntries { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum chain walk length in an ELF GNU hash table.</summary>
    public int MaxElfGnuHashChainLength { get => field; init => field = ValidatePositive(value); } = 100_000;

    // ──────────────────────────────────────────────
    //  PE image limits
    // ──────────────────────────────────────────────

    /// <summary>Maximum number of sections in a PE image.</summary>
    public int MaxPESections { get => field; init => field = ValidatePositive(value); } = 10_000;

    /// <summary>Maximum number of relocations in a PE image.</summary>
    public int MaxPERelocations { get => field; init => field = ValidatePositive(value); } = 10_000_000;

    /// <summary>Maximum number of export names in a PE image.</summary>
    public int MaxPEExportNames { get => field; init => field = ValidatePositive(value); } = 1_000_000;

    /// <summary>Maximum number of debug directories in a PE image.</summary>
    public int MaxPEDebugDirectories { get => field; init => field = ValidatePositive(value); } = 10_000;

    // ──────────────────────────────────────────────
    //  CLR runtime enumeration limits
    // ──────────────────────────────────────────────

    /// <summary>Maximum number of threads to enumerate from the runtime.</summary>
    public int MaxThreads { get => field; init => field = ValidatePositive(value); } = 20_000;

    /// <summary>Maximum number of stack frames to enumerate per thread.</summary>
    public int MaxStackFrames { get => field; init => field = ValidatePositive(value); } = 8_096;

    /// <summary>Maximum number of modules to enumerate across the runtime.</summary>
    public int MaxModules { get => field; init => field = ValidatePositive(value); } = 100_000;

    /// <summary>Maximum number of app domains to enumerate from the runtime.</summary>
    public int MaxAppDomains { get => field; init => field = ValidatePositive(value); } = 10_000;

    private static int ValidatePositive(int value) =>
        value > 0 ? value : throw new System.ArgumentOutOfRangeException(nameof(value), "Limit must be a positive integer.");
}
