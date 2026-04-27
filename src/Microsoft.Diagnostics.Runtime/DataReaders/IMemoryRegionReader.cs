// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Implementation
{
    /// <summary>
    /// Allocation state of a memory region. Mirrors the Windows MEM_* values;
    /// on Linux every region in a core dump is reported as <see cref="Commit"/>.
    /// </summary>
    public enum MemoryRegionState
    {
        /// <summary>State is unknown / not reported by the source.</summary>
        Unknown = 0,
        /// <summary>Memory is committed (backed by physical storage).</summary>
        Commit = 0x1000,
        /// <summary>Memory is reserved but not committed.</summary>
        Reserve = 0x2000,
        /// <summary>Memory is free / not allocated.</summary>
        Free = 0x10000,
    }

    /// <summary>
    /// Backing-store classification for a memory region. Mirrors the Windows
    /// MEM_* values; Linux PT_LOAD segments are classified as
    /// <see cref="Image"/> when they fall inside a known module mapping and
    /// <see cref="Private"/> otherwise.
    /// </summary>
    public enum MemoryRegionType
    {
        /// <summary>Type is unknown / not reported by the source.</summary>
        Unknown = 0,
        /// <summary>Private (anonymous) memory.</summary>
        Private = 0x20000,
        /// <summary>Memory backed by a file mapping (non-image).</summary>
        Mapped = 0x40000,
        /// <summary>Memory backed by a loaded executable image.</summary>
        Image = 0x1000000,
    }

    /// <summary>
    /// Page-protection bits for a memory region. The R/W/X/NoAccess bits are
    /// cross-platform; <see cref="Guard"/>, <see cref="NoCache"/>, and
    /// <see cref="WriteCombine"/> are Windows-only and absent on Linux/macOS.
    /// </summary>
    [Flags]
    public enum MemoryRegionProtect
    {
        /// <summary>No protect bits set / unknown.</summary>
        None = 0,
        /// <summary>Page may not be accessed.</summary>
        NoAccess = 0x001,
        /// <summary>Page is readable.</summary>
        Read = 0x002,
        /// <summary>Page is writable.</summary>
        Write = 0x004,
        /// <summary>Page is copy-on-write.</summary>
        WriteCopy = 0x008,
        /// <summary>Page is executable.</summary>
        Execute = 0x010,
        /// <summary>Page raises STATUS_GUARD_PAGE_VIOLATION on first access (Windows).</summary>
        Guard = 0x100,
        /// <summary>Page is non-cacheable (Windows).</summary>
        NoCache = 0x200,
        /// <summary>Page uses write-combining caching (Windows).</summary>
        WriteCombine = 0x400,
    }

    /// <summary>
    /// One contiguous memory region in the target's virtual address space, as
    /// reported by the data reader. Modeled after MEMORY_BASIC_INFORMATION;
    /// Linux PT_LOAD segments are mapped into this shape with state always
    /// <see cref="MemoryRegionState.Commit"/> and protect derived from the
    /// PT_LOAD <c>p_flags</c> bits.
    /// </summary>
    public readonly struct MemoryRegion
    {
        /// <summary>Lowest virtual address in the region.</summary>
        public ulong BaseAddress { get; init; }

        /// <summary>Size of the region in bytes.</summary>
        public ulong Size { get; init; }

        /// <summary>Allocation state (committed/reserved/free).</summary>
        public MemoryRegionState State { get; init; }

        /// <summary>Backing store classification.</summary>
        public MemoryRegionType Type { get; init; }

        /// <summary>Current page protection.</summary>
        public MemoryRegionProtect Protect { get; init; }

        /// <summary>Base address of the original allocation that contains this
        /// region. On Linux this is the lowest PT_LOAD of the containing image
        /// when classifiable, otherwise <see cref="BaseAddress"/>.</summary>
        public ulong AllocationBase { get; init; }

        /// <summary>Page protection at the time of original allocation. Same
        /// as <see cref="Protect"/> when not separately reported.</summary>
        public MemoryRegionProtect AllocationProtect { get; init; }
    }

    /// <summary>
    /// Provides the full virtual-address-space memory map of the target,
    /// including regions that may not have been written into the dump file
    /// (committed/reserved bookkeeping only).
    ///
    /// This interface is not used by the ClrMD library itself, but is here to
    /// expose data that <see cref="IDataReader"/> implementations already parse
    /// (Windows MINIDUMP_MEMORY_INFO, Linux PT_LOAD program headers, dbgeng
    /// <c>QueryVirtual</c>, /proc/$pid/maps).
    ///
    /// This interface must always be requested and not assumed to be there:
    ///
    ///     IDataReader reader = ...;
    ///
    ///     if (reader is IMemoryRegionReader regions)
    ///         ...
    /// </summary>
    public interface IMemoryRegionReader
    {
        /// <summary>
        /// Enumerates every memory region known to the data reader, ordered by
        /// ascending base address. Implementations should yield regions of all
        /// states (commit/reserve/free) when the source carries that
        /// information; sources that only know about backed memory yield
        /// <see cref="MemoryRegionState.Commit"/> regions only.
        /// </summary>
        IEnumerable<MemoryRegion> EnumerateMemoryRegions();
    }
}
