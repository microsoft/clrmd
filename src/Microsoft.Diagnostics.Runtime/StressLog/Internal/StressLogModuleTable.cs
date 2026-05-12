// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Read-only view over the up-to-five module descriptors carried in a
    /// stress log. Used to translate the bit-packed <c>formatOffset</c>
    /// stored in each message back into a target-memory address that the
    /// reader can read to recover the format string bytes.
    /// </summary>
    /// <remarks>
    /// Both in-process and memory-mapped logs resolve offsets to addresses
    /// in the target's address space. For memory-mapped logs taken inside
    /// a process dump, the runtime keeps the file mapped at capture time
    /// so those addresses are still backed in the dump; for standalone
    /// <c>.stl</c> files (no surrounding process), the format strings are
    /// in the embedded <c>moduleImage</c> region and target-address
    /// resolution will fail. Consumers must read via the dump's
    /// <see cref="IMemoryReader"/>.
    /// </remarks>
    internal sealed class StressLogModuleTable
    {
        private readonly Module[] _modules;
        private readonly ulong _legacyModuleOffset;
        private readonly ulong _maxOffset;
        private readonly ulong _maxAddress;

        /// <summary>Number of module entries that passed validation.</summary>
        public int Count { get; }

        private StressLogModuleTable(Module[] modules,
                                     int count,
                                     ulong legacyModuleOffset,
                                     ulong maxOffset,
                                     ulong maxAddress)
        {
            _modules = modules;
            Count = count;
            _legacyModuleOffset = legacyModuleOffset;
            _maxOffset = maxOffset;
            _maxAddress = maxAddress;
        }

        /// <summary>
        /// Build a module table for an in-process layout. The module entries
        /// occupy <c>StressLogConstants.MaxModules</c> slots in
        /// <paramref name="entries"/>, each <c>2 * pointerSize</c> bytes wide.
        /// </summary>
        public static StressLogModuleTable BuildInProcess(StressLogLayout layout,
                                                          ReadOnlySpan<byte> entries,
                                                          ulong legacyModuleOffset,
                                                          bool hasModuleTable,
                                                          ulong maxOffset)
        {
            Module[] modules = ParseEntries(layout, entries, maxOffset, out int count);

            // Mirror the existing C++ heuristic: only use the module table
            // if the first entry's baseAddress matches legacyModuleOffset
            // and its size is in a sane range. Otherwise fall back to
            // single-module mode.
            if (!hasModuleTable
                || count == 0
                || modules[0].BaseAddress != legacyModuleOffset
                || modules[0].Size < 1024 * 1024
                || modules[0].Size >= maxOffset)
            {
                count = 0;
            }

            return new StressLogModuleTable(modules, count, legacyModuleOffset, maxOffset, layout.MaxAddress);
        }

        /// <summary>
        /// Build a module table for a memory-mapped layout. Memory-mapped
        /// stress logs are 64-bit only, so the entry size is fixed at 16.
        /// </summary>
        public static StressLogModuleTable BuildMemoryMapped(StressLogLayout layout, ReadOnlySpan<byte> entries, ulong maxOffset)
        {
            Module[] modules = ParseEntries(layout, entries, maxOffset, out int count);
            return new StressLogModuleTable(modules, count, legacyModuleOffset: 0, maxOffset, layout.MaxAddress);
        }

        private static Module[] ParseEntries(StressLogLayout layout, ReadOnlySpan<byte> entries, ulong maxOffset, out int count)
        {
            Module[] modules = new Module[StressLogConstants.MaxModules];
            count = 0;

            int entrySize = layout.ModuleEntrySize;
            int p = layout.PointerSize;
            ulong cumulative = 0;
            for (int i = 0; i < StressLogConstants.MaxModules; i++)
            {
                int o = i * entrySize;
                if (o + entrySize > entries.Length)
                    break;

                ulong baseAddress = layout.ReadTargetPointer(entries, o);
                ulong size = layout.ReadTargetPointer(entries, o + p);

                if (baseAddress == 0)
                    break;

                if (size == 0 || size >= maxOffset)
                    break;

                ulong nextCumulative = cumulative + size;
                if (nextCumulative < cumulative || nextCumulative >= maxOffset)
                    break;

                // Reject modules whose base+size cannot be represented in
                // the target's address space (matters on 32-bit, where a
                // malformed dump can otherwise produce addresses > 4GB).
                ulong endAddress = baseAddress + size;
                if (endAddress < baseAddress || endAddress - 1 > layout.MaxAddress)
                    break;

                modules[i] = new Module(baseAddress, size, cumulative);
                cumulative = nextCumulative;
                count++;
            }

            return modules;
        }

        /// <summary>
        /// Translate a bit-packed format offset into either a target address
        /// (for in-process logs) or an address inside the embedded module
        /// image (for memory-mapped logs). Returns false if the offset is
        /// out of range.
        /// </summary>
        public bool TryResolveFormatOffset(ulong formatOffset, out ulong address)
        {
            if (formatOffset == 0 || formatOffset >= _maxOffset)
            {
                address = 0;
                return false;
            }

            if (Count == 0)
            {
                // Legacy single-module mode (in-process only).
                if (_legacyModuleOffset == 0)
                {
                    address = 0;
                    return false;
                }

                address = _legacyModuleOffset + formatOffset;
                if (address < _legacyModuleOffset || address > _maxAddress)
                {
                    address = 0;
                    return false;
                }
                return true;
            }

            for (int i = 0; i < Count; i++)
            {
                Module m = _modules[i];
                if (formatOffset < m.CumulativeOffset + m.Size && formatOffset >= m.CumulativeOffset)
                {
                    ulong relative = formatOffset - m.CumulativeOffset;
                    address = m.BaseAddress + relative;
                    if (address < m.BaseAddress || address > _maxAddress)
                    {
                        address = 0;
                        return false;
                    }
                    return true;
                }
            }

            address = 0;
            return false;
        }

        private readonly struct Module
        {
            public Module(ulong baseAddress, ulong size, ulong cumulativeOffset)
            {
                BaseAddress = baseAddress;
                Size = size;
                CumulativeOffset = cumulativeOffset;
            }

            public ulong BaseAddress { get; }
            public ulong Size { get; }
            public ulong CumulativeOffset { get; }
        }
    }
}
