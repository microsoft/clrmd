// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Implementation
{
    /// <summary>
    /// One loadable segment of a module — an ELF PT_LOAD entry on Linux,
    /// or one PE section header (.text / .data / .rdata / ...) on Windows.
    ///
    /// <see cref="VirtualAddress"/> is an absolute address in the target's
    /// address space (load bias already applied for PIE / shared objects).
    /// </summary>
    public readonly struct ModuleSegment
    {
        /// <summary>Absolute base address of the segment in the target.</summary>
        public ulong VirtualAddress { get; init; }

        /// <summary>In-memory size of the segment in bytes (includes BSS).</summary>
        public ulong VirtualSize { get; init; }

        /// <summary>Whether the segment is mapped readable.</summary>
        public bool IsReadable { get; init; }

        /// <summary>Whether the segment is mapped writable.</summary>
        public bool IsWritable { get; init; }

        /// <summary>Whether the segment is mapped executable.</summary>
        public bool IsExecutable { get; init; }
    }

    /// <summary>
    /// Provides per-module loadable-segment layout (ELF PT_LOAD program
    /// headers, PE section table) so callers can attribute a memory region
    /// back to a specific segment of the owning module.
    ///
    /// This interface is not used by the ClrMD library itself, but is here
    /// to expose data that <see cref="IDataReader"/> implementations already
    /// parse — the same ELF program-header walk used to build the module
    /// list and the <see cref="IMemoryRegionReader"/> output.
    ///
    /// This interface must always be requested and not assumed to be there:
    ///
    ///     IDataReader reader = ...;
    ///
    ///     if (reader is IModuleSegmentReader segments)
    ///         ...
    /// </summary>
    public interface IModuleSegmentReader
    {
        /// <summary>
        /// Enumerates the loadable segments of the module whose
        /// <see cref="ModuleInfo.ImageBase"/> equals
        /// <paramref name="moduleBaseAddress"/>. Each returned segment's
        /// <see cref="ModuleSegment.VirtualAddress"/> is absolute (load bias
        /// already applied). Returns an empty sequence when the module is
        /// unknown or its headers cannot be parsed.
        /// </summary>
        IEnumerable<ModuleSegment> EnumerateModuleSegments(ulong moduleBaseAddress);
    }
}
