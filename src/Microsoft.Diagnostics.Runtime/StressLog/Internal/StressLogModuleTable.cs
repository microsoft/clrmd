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

        /// <summary>Number of module entries that passed validation.</summary>
        public int Count { get; }

        private StressLogModuleTable(Module[] modules,
                                     int count,
                                     ulong legacyModuleOffset,
                                     ulong maxOffset)
        {
            _modules = modules;
            Count = count;
            _legacyModuleOffset = legacyModuleOffset;
            _maxOffset = maxOffset;
        }

        /// <summary>
        /// Build a module table for an in-process layout. The module entries
        /// occupy <c>StressLogConstants.MaxModules</c> 16-byte slots in
        /// <paramref name="entries16"/>.
        /// </summary>
        public static StressLogModuleTable BuildInProcess(ReadOnlySpan<byte> entries16,
                                                          ulong legacyModuleOffset,
                                                          bool hasModuleTable,
                                                          ulong maxOffset)
        {
            Module[] modules = ParseEntries(entries16, maxOffset, out int count);

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

            return new StressLogModuleTable(modules, count, legacyModuleOffset, maxOffset);
        }

        /// <summary>
        /// Build a module table for a memory-mapped layout.
        /// </summary>
        public static StressLogModuleTable BuildMemoryMapped(ReadOnlySpan<byte> entries16, ulong maxOffset)
        {
            Module[] modules = ParseEntries(entries16, maxOffset, out int count);
            return new StressLogModuleTable(modules, count, legacyModuleOffset: 0, maxOffset);
        }

        private static Module[] ParseEntries(ReadOnlySpan<byte> entries16, ulong maxOffset, out int count)
        {
            Module[] modules = new Module[StressLogConstants.MaxModules];
            count = 0;

            ulong cumulative = 0;
            for (int i = 0; i < StressLogConstants.MaxModules; i++)
            {
                int o = i * StressLogLayout.InProc_ModuleEntrySize;
                if (o + StressLogLayout.InProc_ModuleEntrySize > entries16.Length)
                    break;

                ulong baseAddress = BinaryPrimitives.ReadUInt64LittleEndian(entries16.Slice(o));
                ulong size = BinaryPrimitives.ReadUInt64LittleEndian(entries16.Slice(o + 8));

                if (baseAddress == 0)
                    break;

                if (size == 0 || size >= maxOffset)
                    break;

                ulong nextCumulative = cumulative + size;
                if (nextCumulative < cumulative || nextCumulative >= maxOffset)
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
                return address >= _legacyModuleOffset; // overflow guard
            }

            for (int i = 0; i < Count; i++)
            {
                Module m = _modules[i];
                if (formatOffset < m.CumulativeOffset + m.Size && formatOffset >= m.CumulativeOffset)
                {
                    ulong relative = formatOffset - m.CumulativeOffset;
                    address = m.BaseAddress + relative;
                    return address >= m.BaseAddress; // overflow guard
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
