// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A helper class to read linux coredumps.
    /// </summary>
    internal sealed class ElfCoreFile : IDisposable
    {
        private readonly DataTargetLimits _limits;

        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly Reader _reader;
        private ImmutableDictionary<ulong, ElfLoadedImage>? _loadedImages;
        // _auxvEntries is mutated only under _auxvLock; _auxvLoaded is the publication
        // flag readers check first to avoid taking the lock on the steady-state path.
        private readonly Dictionary<ulong, ulong> _auxvEntries = new();
        private readonly object _auxvLock = new();
        private volatile bool _auxvLoaded;
        private ElfVirtualAddressSpace? _virtualAddressSpace;

        /// <summary>
        /// All coredumps are themselves ELF files.  This property returns the ElfFile that represents this coredump.
        /// </summary>
        public ElfFile ElfFile { get; }

        /// <summary>
        /// Enumerates all prstatus notes contained within this coredump.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IElfPRStatus> EnumeratePRStatus()
        {
            ElfMachine architecture = ElfFile.Header.Architecture;

            return GetNotes(ElfNoteType.PrpsStatus).Select<ElfNote, IElfPRStatus>(r => {
                return architecture switch
                {
                    ElfMachine.EM_X86_64 => r.ReadContents<ElfPRStatusX64>(0),
                    ElfMachine.EM_ARM => r.ReadContents<ElfPRStatusArm>(0),
                    ElfMachine.EM_AARCH64 => r.ReadContents<ElfPRStatusArm64>(0),
                    ElfMachine.EM_386 => r.ReadContents<ElfPRStatusX86>(0),
                    ElfMachine.EM_RISCV => ElfFile.Header.Is64Bit ?
                                                r.ReadContents<ElfPRStatusRiscV64>(0) :
                                                throw new NotSupportedException($"Invalid architecture RISC-V 32bit"),
                    ElfMachine.EM_LOONGARCH => ElfFile.Header.Is64Bit ?
                                                r.ReadContents<ElfPRStatusLoongArch64>(0) :
                                                throw new NotSupportedException($"Invalid architecture LoongArch 32bit"),
                    _ => throw new NotSupportedException($"Invalid architecture {architecture}"),
                };
            });
        }

        /// <summary>
        /// Returns the Auxv value of the given type.
        /// </summary>
        public ulong GetAuxvValue(ElfAuxvType type)
        {
            LoadAuxvTable();
            _auxvEntries.TryGetValue((ulong)type, out ulong value);
            return value;
        }

        /// <summary>
        /// A mapping of all loaded images in the process.  The key is the base address that the module is loaded at.
        /// </summary>
        public ImmutableDictionary<ulong, ElfLoadedImage> LoadedImages
        {
            get
            {
                ImmutableDictionary<ulong, ElfLoadedImage>? cached = Volatile.Read(ref _loadedImages);
                if (cached is not null)
                    return cached;

                ImmutableDictionary<ulong, ElfLoadedImage> fresh = LoadFileTable();
                return Interlocked.CompareExchange(ref _loadedImages, fresh, null) ?? fresh;
            }
        }

        /// <summary>
        /// Creates an ElfCoreFile from a file on disk.
        /// </summary>
        /// <param name="coredump">A full path to a coredump on disk.</param>
        /// <exception cref="InvalidDataException">Throws <see cref="InvalidDataException"/> if the file is not an Elf coredump.</exception>
        public ElfCoreFile(string coredump)
            : this(File.OpenRead(coredump))
        {
        }

        /// <summary>
        /// Creates an ElfCoreFile from a file on disk.
        /// </summary>
        /// <param name="stream">The Elf stream to read the coredump from.</param>
        /// <param name="leaveOpen">Whether to leave the given stream open after this class is disposed.</param>
        /// <param name="limits">Optional safety limits for parsing.</param>
        /// <exception cref="InvalidDataException">Throws <see cref="InvalidDataException"/> if the file is not an Elf coredump.</exception>
        public ElfCoreFile(Stream stream, bool leaveOpen = false, DataTargetLimits? limits = null)
        {
            _limits = limits ?? new DataTargetLimits();
            _stream = stream;
            _leaveOpen = leaveOpen;

            _reader = new Reader(new StreamAddressSpace(stream));
            ElfFile = new ElfFile(_reader, limits: _limits);

            if (ElfFile.Header.Type != ElfHeaderType.Core)
                throw new InvalidDataException($"{stream.GetFilename() ?? "The given stream"} is not a coredump");

#if DEBUG
            _loadedImages = LoadFileTable();
#endif
        }

        /// <summary>
        /// Reads memory from the given coredump's virtual address space.
        /// </summary>
        /// <param name="address">An address in the target program's virtual address space.</param>
        /// <param name="buffer">The buffer to fill.</param>
        /// <returns>The number of bytes written into the buffer.</returns>
        public int ReadMemory(ulong address, Span<byte> buffer)
        {
            ElfVirtualAddressSpace? vas = Volatile.Read(ref _virtualAddressSpace);
            if (vas is null)
            {
                ElfVirtualAddressSpace fresh = new(ElfFile.ProgramHeaders, _reader.DataSource);
                vas = Interlocked.CompareExchange(ref _virtualAddressSpace, fresh, null) ?? fresh;
            }
            return vas.Read(address, buffer);
        }

        private IEnumerable<ElfNote> GetNotes(ElfNoteType type)
        {
            return ElfFile.Notes.Where(n => n.Type == type);
        }

        private void LoadAuxvTable()
        {
            if (_auxvLoaded)
                return;

            // Hold the lock for the full read+populate so concurrent first callers do
            // not corrupt _auxvEntries via interleaved Add calls.
            lock (_auxvLock)
            {
                if (_auxvLoaded)
                    return;

                // Defensive: if a previous attempt threw mid-population (e.g., hit
                // MaxElfAuxvEntries), start fresh so duplicate-key Adds don't fire.
                _auxvEntries.Clear();

                ElfNote auxvNote = GetNotes(ElfNoteType.Aux).SingleOrDefault() ?? throw new BadImageFormatException($"No auxv entries in coredump");
                ulong position = 0;
                int count = 0;
                while (true)
                {
                    if (count++ > _limits.MaxElfAuxvEntries)
                        throw new InvalidDataException($"ELF coredump contains more than {_limits.MaxElfAuxvEntries} auxv entries, which exceeds the maximum allowed.");

                    ulong type;
                    ulong value;
                    if (ElfFile.Header.Is64Bit)
                    {
                        ElfAuxv64 elfauxv64 = auxvNote.ReadContents<ElfAuxv64>(ref position);
                        type = elfauxv64.Type;
                        value = elfauxv64.Value;
                    }
                    else
                    {
                        ElfAuxv32 elfauxv32 = auxvNote.ReadContents<ElfAuxv32>(ref position);
                        type = elfauxv32.Type;
                        value = elfauxv32.Value;
                    }

                    if (type == (ulong)ElfAuxvType.Null)
                    {
                        break;
                    }

                    _auxvEntries.Add(type, value);
                }

                // _auxvLoaded is volatile: the write here happens-after all _auxvEntries
                // mutations and is observed by readers using the fast-path check above.
                _auxvLoaded = true;
            }
        }

        private ImmutableDictionary<ulong, ElfLoadedImage> LoadFileTable()
        {
            ElfNote fileNote = GetNotes(ElfNoteType.File).Single();

            ulong position = 0;
            ulong entryCount;
            if (ElfFile.Header.Is64Bit)
            {
                ElfFileTableHeader64 header = fileNote.ReadContents<ElfFileTableHeader64>(ref position);
                entryCount = header.EntryCount;
            }
            else
            {
                ElfFileTableHeader32 header = fileNote.ReadContents<ElfFileTableHeader32>(ref position);
                entryCount = header.EntryCount;
            }

            if (entryCount > (ulong)_limits.MaxElfFileTableEntries)
                throw new InvalidDataException($"ELF coredump file table reports {entryCount} entries, which exceeds the maximum of {_limits.MaxElfFileTableEntries}.");

            ElfFileTableEntryPointers64[] fileTable = new ElfFileTableEntryPointers64[entryCount];
            Dictionary<string, List<ElfFileTableEntryPointers64>> entriesByPath = new();

            for (int i = 0; i < fileTable.Length; i++)
            {
                if (ElfFile.Header.Is64Bit)
                {
                    fileTable[i] = fileNote.ReadContents<ElfFileTableEntryPointers64>(ref position);
                }
                else
                {
                    ElfFileTableEntryPointers32 entry = fileNote.ReadContents<ElfFileTableEntryPointers32>(ref position);
                    fileTable[i].Start = entry.Start;
                    fileTable[i].Stop = entry.Stop;
                    fileTable[i].PageOffset = entry.PageOffset;
                }
            }

            int size = (int)(fileNote.Header.ContentSize - position);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                int read = fileNote.ReadContents(position, bytes);
                int start = 0;
                for (int i = 0; i < fileTable.Length; i++)
                {
                    int end = start;
                    while (end < read && bytes[end] != 0)
                        end++;

                    if (end >= read)
                        throw new InvalidDataException("ELF coredump file table contains a file path without a null terminator.");

                    string path = Encoding.UTF8.GetString(bytes, start, end - start);
                    start = end + 1;

                    if (!entriesByPath.TryGetValue(path, out List<ElfFileTableEntryPointers64>? pathEntries))
                        pathEntries = entriesByPath[path] = new List<ElfFileTableEntryPointers64>();

                    pathEntries.Add(fileTable[i]);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }

            ImmutableDictionary<ulong, ElfLoadedImage>.Builder result = ImmutableDictionary.CreateBuilder<ulong, ElfLoadedImage>();
            foreach (KeyValuePair<string, List<ElfFileTableEntryPointers64>> kvp in entriesByPath)
            {
                foreach (ElfLoadedImage image in BuildImagesForPath(kvp.Key, kvp.Value))
                {
                    // Distinct images (whether split from one path or belonging to different paths) should
                    // have distinct base addresses. If a malformed dump violates that, the last one wins
                    // (matching the historical single-image behavior) rather than throwing; assert in debug
                    // builds so we notice the corruption during testing.
                    Debug.Assert(!result.ContainsKey(image.BaseAddress), $"Duplicate ELF image base address 0x{image.BaseAddress:x} for '{image.FileName}'.");
                    result[image.BaseAddress] = image;
                }
            }

            return result.ToImmutable();
        }

        // A single file can be present in the NT_FILE table under several file-backed VMAs.
        // Normally those are the segments of one mapping (e.g. the PT_LOAD sections of a .so, or
        // the PE sections of a managed assembly's "loaded" layout) and belong to one module.
        // However, the runtime maps a managed ReadyToRun assembly TWICE on Linux/macOS: a "flat"
        // file layout and a "loaded"/converted layout, both file-backed by the same path but at
        // disjoint (often multi-GB apart) address ranges. Grouping purely by path collapses those
        // into one module whose size spans the entire gap, which is wrong and produces bogus,
        // mutually-overlapping module ranges. Split a path's entries into contiguity clusters so
        // each distinct mapping becomes its own module.
        private List<ElfLoadedImage> BuildImagesForPath(string path, List<ElfFileTableEntryPointers64> entries)
        {
            int n = entries.Count;

            // Order entries by start address to detect clusters, but keep original file order when
            // populating each image so the base-address selection ("first PageOffset == 0 entry")
            // matches the historical single-image behavior for entries that stay grouped together.
            int[] order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => entries[a].Start.CompareTo(entries[b].Start));

            int[] clusterOf = new int[n];
            int clusterCount = 0;
            ulong clusterHeaderBase = 0; // address probed for the PE header / SizeOfImage
            bool clusterHasHeader = false; // whether clusterHeaderBase is a PageOffset == 0 (header) entry
            ulong clusterEnd = 0;
            long clusterSizeOfImage = -1; // -1 = not read yet, 0 = unreadable / not a PE
            for (int k = 0; k < n; k++)
            {
                ElfFileTableEntryPointers64 entry = entries[order[k]];
                if (k == 0 || !IsSameImage(clusterHeaderBase, clusterEnd, ref clusterSizeOfImage, entry.Start))
                {
                    clusterCount++;
                    clusterHeaderBase = entry.Start;
                    clusterHasHeader = entry.PageOffset == 0;
                    clusterEnd = entry.Stop;
                    clusterSizeOfImage = -1;
                }
                else
                {
                    if (entry.Stop > clusterEnd)
                        clusterEnd = entry.Stop;

                    // The image header (and thus its PE SizeOfImage) lives at the first PageOffset == 0 entry,
                    // which is not necessarily the lowest-address entry (e.g. .NET single-file modules). Once
                    // such an entry joins, retarget the probe at it and re-read SizeOfImage.
                    if (!clusterHasHeader && entry.PageOffset == 0)
                    {
                        clusterHeaderBase = entry.Start;
                        clusterHasHeader = true;
                        clusterSizeOfImage = -1;
                    }
                }

                clusterOf[order[k]] = clusterCount - 1;
            }

            ElfLoadedImage[] images = new ElfLoadedImage[clusterCount];
            for (int c = 0; c < clusterCount; c++)
                images[c] = new ElfLoadedImage(ElfFile.VirtualAddressReader, ElfFile.Header.Is64Bit, path);

            for (int i = 0; i < n; i++)
                images[clusterOf[i]].AddTableEntryPointers(entries[i]);

            return new List<ElfLoadedImage>(images);
        }

        // Decides whether an entry starting at nextStart belongs to the current contiguity cluster.
        // The image's declared PE SizeOfImage (read from the header at headerBase) authoritatively
        // bounds the mapping when available: an entry that starts at or beyond the end of the image
        // is a distinct mapping of the same file (e.g. the flat vs loaded layouts of a managed
        // ReadyToRun assembly). When SizeOfImage is unavailable -- a native ELF image with no PE
        // header, or an unreadable header -- fall back to a gap heuristic: mappings that are far
        // apart are distinct, otherwise they are treated as one image (the historical behavior).
        private bool IsSameImage(ulong headerBase, ulong clusterEnd, ref long cachedSizeOfImage, ulong nextStart)
        {
            if (cachedSizeOfImage < 0)
                cachedSizeOfImage = TryReadPESizeOfImage(headerBase);

            if (cachedSizeOfImage > 0)
            {
                // nextStart >= headerBase because entries are visited in ascending start order and
                // headerBase is a start of an entry already in the cluster; compute the offset by
                // subtraction to avoid overflowing headerBase + SizeOfImage near the top of the range.
                ulong offset = nextStart >= headerBase ? nextStart - headerBase : 0;
                return offset < (ulong)cachedSizeOfImage;
            }

            ulong gap = nextStart > clusterEnd ? nextStart - clusterEnd : 0;
            return gap < LargeGapThreshold;
        }

        // Reads the PE OptionalHeader.SizeOfImage for the image mapped at imageBase, or 0 if the
        // bytes are not present or the image is not a PE. SizeOfImage lives at offset 56 within the
        // optional header for both PE32 and PE32+, so no bitness detection is required.
        private long TryReadPESizeOfImage(ulong imageBase)
        {
            Reader reader = ElfFile.VirtualAddressReader;

            if (reader.TryRead<ushort>(imageBase) is not 0x5A4D) // 'MZ'
                return 0;

            uint? lfanew = reader.TryRead<uint>(imageBase + 0x3C);
            if (lfanew is null || lfanew.Value > 0x10000)
                return 0;

            ulong ntHeaders = imageBase + lfanew.Value;
            if (reader.TryRead<uint>(ntHeaders) is not 0x00004550) // 'PE\0\0'
                return 0;

            // 4-byte PE signature + 20-byte COFF file header, then SizeOfImage at optional offset 56.
            uint? sizeOfImage = reader.TryRead<uint>(ntHeaders + 4 + 20 + 56);
            return sizeOfImage ?? 0;
        }

        // Fallback ceiling used only when a same-path mapping has no readable PE SizeOfImage (i.e. a
        // native ELF image). It is far larger than the internal padding/alignment gaps within a
        // single native mapping, but far smaller than the multi-GB separation seen between distinct
        // mappings of the same file, so it distinguishes the two without a header to consult.
        private const ulong LargeGapThreshold = 0x10000000; // 256 MB

        public void Dispose()
        {
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }
}