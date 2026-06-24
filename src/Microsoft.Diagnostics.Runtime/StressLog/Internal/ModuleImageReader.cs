// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// An <see cref="IMemoryReader"/> that satisfies reads from dump memory
    /// first and, when the dump does not back the requested range, falls back
    /// to the on-disk image of the module that contains the address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ELF coredumps (and Windows minidumps) routinely omit file-backed,
    /// read-only pages such as a module's <c>.rodata</c> section, since those
    /// bytes can be recovered from the binary on disk. Stress log format
    /// strings live in <c>.rodata</c>, so without this fallback every message
    /// renders as <c>&lt;unresolved-format&gt;</c> on a typical Linux dump. SOS
    /// gets this for free because dbgeng's <c>ReadVirtual</c> transparently
    /// maps loaded module images into the target's address space.
    /// </para>
    /// <para>
    /// The fallback is read-only and engages only when the dump read does not
    /// fully satisfy the request and the address falls inside a known module's
    /// image. Structural reads (chunk buffers, thread headers, heap-resident
    /// argument strings) are backed by the dump and never reach the fallback,
    /// so this cannot mask a genuinely missing region of the log. Today only
    /// ELF modules are resolved; other module kinds fall through to the
    /// primary reader's result.
    /// </para>
    /// </remarks>
    internal sealed class ModuleImageReader : CommonMemoryReader, IDisposable
    {
        private readonly IMemoryReader _primary;
        private readonly DataTarget _dataTarget;
        private readonly OSPlatform _platform;
        private readonly object _gate = new();

        private ModuleImage[]? _modules;
        private bool _disposed;

        public ModuleImageReader(IMemoryReader primary, DataTarget dataTarget)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _dataTarget = dataTarget ?? throw new ArgumentNullException(nameof(dataTarget));
            _platform = dataTarget.DataReader.TargetPlatform;
        }

        public override int PointerSize => _primary.PointerSize;

        public override int Read(ulong address, Span<byte> buffer)
        {
            int got = _primary.Read(address, buffer);
            if (got == buffer.Length || buffer.Length == 0)
                return got;

            // The dump did not fully back this range. If the address lies
            // inside a module's image, recover the bytes from the binary on
            // disk; otherwise keep whatever the dump returned.
            int fromImage = TryReadFromModuleImage(address, buffer);
            return fromImage > got ? fromImage : got;
        }

        private int TryReadFromModuleImage(ulong address, Span<byte> buffer)
        {
            ModuleImage? module = FindModule(address);
            return module is null ? 0 : module.Read(address, buffer);
        }

        private ModuleImage? FindModule(ulong address)
        {
            ModuleImage[] modules = EnsureModules();

            // Module counts are small (tens), so a linear scan is fine and
            // avoids the bookkeeping of keeping the list sorted.
            foreach (ModuleImage module in modules)
            {
                if (address >= module.ImageBase && address < module.ImageEnd)
                    return module;
            }

            return null;
        }

        private ModuleImage[] EnsureModules()
        {
            ModuleImage[]? modules = _modules;
            if (modules is not null)
                return modules;

            lock (_gate)
            {
                if (_modules is not null)
                    return _modules;

                List<ModuleImage> list = new();
                foreach (ModuleInfo info in _dataTarget.EnumerateModules())
                {
                    // Resolve ELF module images. Some data readers report a
                    // real ModuleKind; others (notably the dotnet/diagnostics
                    // IDataReader bridge used by dotnet-dump and SOS) report
                    // every module as ModuleKind.Unknown. On a Linux target,
                    // admit Unknown modules too and let ModuleImage validate
                    // them by attempting an ELF parse: a non-ELF file simply
                    // yields no bytes. Without this, the on-disk .rodata
                    // fallback never engages under dotnet-dump and every
                    // message renders as <unresolved-format>.
                    bool elfCandidate = info.Kind == ModuleKind.Elf
                        || (info.Kind == ModuleKind.Unknown && _platform == OSPlatform.Linux);
                    if (!elfCandidate)
                        continue;
                    if (info.ImageBase == 0 || info.ImageSize <= 0)
                        continue;

                    list.Add(new ModuleImage(info, _dataTarget, _platform));
                }

                _modules = list.ToArray();
                return _modules;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            ModuleImage[]? modules = _modules;
            if (modules is not null)
            {
                foreach (ModuleImage module in modules)
                    module.Dispose();
            }
        }

        /// <summary>
        /// A single module whose on-disk image can be read by virtual address.
        /// The backing <see cref="ElfFile"/> is opened lazily on first use and
        /// reused for subsequent reads.
        /// </summary>
        private sealed class ModuleImage : IDisposable
        {
            private readonly ModuleInfo _info;
            private readonly DataTarget _dataTarget;
            private readonly OSPlatform _platform;
            private readonly object _gate = new();

            private ElfFile? _elf;
            private bool _resolved;

            public ModuleImage(ModuleInfo info, DataTarget dataTarget, OSPlatform platform)
            {
                _info = info;
                _dataTarget = dataTarget;
                _platform = platform;

                // Saturate rather than wrap: a corrupt reader can report an
                // ImageSize that pushes ImageBase + ImageSize past ulong.MaxValue,
                // which would make ImageEnd < ImageBase and silently hide the module.
                ulong size = (ulong)info.ImageSize;
                ImageEnd = info.ImageBase > ulong.MaxValue - size ? ulong.MaxValue : info.ImageBase + size;
            }

            public ulong ImageBase => _info.ImageBase;

            public ulong ImageEnd { get; }

            public int Read(ulong address, Span<byte> buffer)
            {
                ElfFile? elf = EnsureElf();
                if (elf is null)
                    return 0;

                // The module is loaded at ImageBase. A runtime address maps to
                // the file's own virtual-address space by subtracting the load
                // base; for shared objects the lowest p_vaddr is 0, so this is
                // simply (address - ImageBase).
                //
                // We translate that file virtual address to a file offset by
                // walking the PT_LOAD program headers ourselves rather than
                // going through ElfFile.VirtualAddressReader. The shared
                // ElfVirtualAddressSpace locates segments with a binary search
                // that assumes non-overlapping, vaddr-sorted segments; a module
                // image nests smaller headers (PT_PHDR, PT_NOTE, ...) inside the
                // first large PT_LOAD, so that search can step past the
                // enclosing segment and fail to find it. Coredumps never hit
                // this because their PT_LOADs do not overlap.
                ulong fileVaddr = address - _info.ImageBase;

                try
                {
                    foreach (ElfProgramHeader ph in elf.ProgramHeaders)
                    {
                        if (ph.Type != ElfProgramHeaderType.Load || ph.FileSize == 0)
                            continue;

                        // Guard against ulong wrap from a corrupt/hostile header
                        // before using the segment's vaddr range.
                        if (ph.FileSize > ulong.MaxValue - ph.VirtualAddress)
                            continue;
                        if (fileVaddr < ph.VirtualAddress || fileVaddr >= ph.VirtualAddress + ph.FileSize)
                            continue;

                        ulong segmentOffset = fileVaddr - ph.VirtualAddress;

                        // Guard against ulong wrap when translating to a file offset.
                        if (segmentOffset > ulong.MaxValue - ph.FileOffset)
                            return 0;
                        ulong fileOffset = ph.FileOffset + segmentOffset;

                        // Do not read past this segment's file-backed bytes.
                        int toRead = (int)Math.Min((ulong)buffer.Length, ph.FileSize - segmentOffset);
                        if (toRead <= 0)
                            return 0;

                        return elf.Reader.ReadBytes(fileOffset, buffer.Slice(0, toRead));
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or OverflowException or ArgumentException)
                {
                    // A corrupt or hostile module image (e.g. lazy program-header
                    // parsing throws InvalidDataException, or out-of-range offsets)
                    // must degrade to <unresolved-format>, never crash enumeration.
                    return 0;
                }

                return 0;
            }

            private ElfFile? EnsureElf()
            {
                if (_resolved)
                    return _elf;

                lock (_gate)
                {
                    if (_resolved)
                        return _elf;

                    _elf = TryOpenElf();
                    _resolved = true;
                    return _elf;
                }
            }

            private ElfFile? TryOpenElf()
            {
                string? path = LocateFile();
                if (path is null)
                    return null;

                try
                {
                    return new ElfFile(path);
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (InvalidDataException)
                {
                    return null;
                }
            }

            private string? LocateFile()
            {
                // The module path recorded in the dump is untrusted input: a
                // crafted dump could point it at a UNC share (triggering an
                // outbound SMB authentication / NTLM-hash leak when probed) or at
                // an arbitrary local file. We therefore never open the recorded
                // path directly. Instead, locate the binary by its build id
                // through the configured file locator (symbol server / local
                // cache), which keys on the file's base name plus build id rather
                // than the dump-supplied path.
                IFileLocator? locator = _dataTarget.FileLocator;
                if (locator is null || _info.BuildId.IsDefaultOrEmpty)
                    return null;

                string? found = locator.FindPEImage(_info.FileName, SymbolProperties.Self, _info.BuildId, _platform, checkProperties: false);
                return !string.IsNullOrEmpty(found) && File.Exists(found) ? found : null;
            }

            public void Dispose() => _elf?.Dispose();
        }
    }
}
