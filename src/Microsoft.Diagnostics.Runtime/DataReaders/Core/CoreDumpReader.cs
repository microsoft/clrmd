// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class CoredumpReader : CommonMemoryReader, IDataReader, IDisposable, IThreadReader, IDumpInfoProvider, IDumpFileMemorySource, IThreadInfoReader, IProcessInfoProvider, IMemoryRegionReader, IModuleSegmentReader
    {
        private readonly ElfCoreFile _core;
        private readonly DataTargetLimits? _limits;
        private Dictionary<uint, IElfPRStatus>? _threads;
        private List<ModuleInfo>? _modules;
        private bool? _isCreatedByDotNetRuntime;

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => OSPlatform.Linux;

        public CoredumpReader(string path, Stream stream, bool leaveOpen, DataTargetLimits? limits = null)
        {
            DisplayName = path ?? throw new ArgumentNullException(nameof(path));
            _limits = limits;
            _core = new ElfCoreFile(stream ?? throw new ArgumentNullException(nameof(stream)), leaveOpen, limits);

            ElfMachine architecture = _core.ElfFile.Header.Architecture;
            (PointerSize, Architecture) = architecture switch
            {
                ElfMachine.EM_X86_64 => (8, Architecture.X64),
                ElfMachine.EM_386 => (4, Architecture.X86),
                ElfMachine.EM_AARCH64 => (8, Architecture.Arm64),
                ElfMachine.EM_ARM => (4, Architecture.Arm),
                ElfMachine.EM_RISCV => _core.ElfFile.Header.Is64Bit ?
                                            (8, (Architecture)9 /* Architecture.RiscV64 */) :
                                            throw new NotSupportedException($"RISC-V 32-bit is not supported."),
                ElfMachine.EM_LOONGARCH => _core.ElfFile.Header.Is64Bit ?
                                            (8, (Architecture)6 /* Architecture.LoongArch64 */) :
                                            throw new NotSupportedException($"LoongArch 32-bit is not supported."),
                _ => throw new NotSupportedException($"Architecture {architecture} is not supported."),
            };
        }

        public bool IsThreadSafe => false;

        public bool IsMiniOrTriage => false;

        public bool IsCreatedByDotNetRuntime
        {
            get
            {
                _isCreatedByDotNetRuntime ??= SpecialDiagInfo.TryReadSpecialDiagInfo(this, out _);
                return _isCreatedByDotNetRuntime.Value;
            }
        }

        public void Dispose()
        {
            _core.Dispose();
        }

        public int ProcessId
        {
            get
            {
                foreach (IElfPRStatus status in _core.EnumeratePRStatus())
                    return (int)status.ProcessId;

                return -1;
            }
        }

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_modules is null)
            {
                // Need to filter out non-modules like the interpreter (named something
                // like "ld-2.23") and anything that starts with /dev/ because their
                // memory range overlaps with actual modules.
                ulong interpreter = _core.GetAuxvValue(ElfAuxvType.Base);

                _modules = new List<ModuleInfo>();
                foreach (ElfLoadedImage image in _core.LoadedImages.Values)
                    if (image.BaseAddress != interpreter && !image.FileName.StartsWith("/dev", StringComparison.Ordinal))
                        _modules.Add(CreateModuleInfo(image));
            }

            return _modules;
        }

        private ModuleInfo CreateModuleInfo(ElfLoadedImage image)
        {
            using ElfFile? file = image.Open();

            // We suppress the warning because the function it wants us to use is not available on all ClrMD platforms

            // This substitution is for unloaded modules for which Linux appends " (deleted)" to the module name.
            string path = image.FileName.Replace(" (deleted)", "");
            if (file is not null)
            {
                long size = image.Size > long.MaxValue ? long.MaxValue : unchecked((long)image.Size);
                return new ElfModuleInfo(this, file, image.BaseAddress, size, path);
            }

            return new PEModuleInfo(this, image.BaseAddress, path, true, _limits);
        }

        public void FlushCachedData()
        {
            _threads = null;
            _modules = null;
        }

        public Architecture Architecture { get; }

        public override int PointerSize { get; }

        public IEnumerable<uint> EnumerateOSThreadIds()
        {
            foreach (IElfPRStatus status in _core.EnumeratePRStatus())
                yield return status.ThreadId;
        }

        public ulong GetThreadTeb(uint osThreadId) => 0;

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            Dictionary<uint, IElfPRStatus> threads = LoadThreads();

            if (threads.TryGetValue(threadID, out IElfPRStatus? status))
                return status.CopyRegistersAsContext(context);

            return false;
        }

        public override int Read(ulong address, Span<byte> buffer)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            return address > long.MaxValue ? 0 : _core.ReadMemory(address, buffer);
        }

        private Dictionary<uint, IElfPRStatus> LoadThreads()
        {
            Dictionary<uint, IElfPRStatus>? threads = _threads;

            if (threads is null)
            {
                threads = new Dictionary<uint, IElfPRStatus>();
                foreach (IElfPRStatus status in _core.EnumeratePRStatus())
                    threads.Add(status.ThreadId, status);

                _threads = threads;
            }

            return threads;
        }

        public IReadOnlyList<DumpMemorySegment> EnumerateMemorySegments()
        {
            // Mirror ElfVirtualAddressSpace's filter: only segments backed by file data.
            var headers = _core.ElfFile.ProgramHeaders;
            List<DumpMemorySegment> result = new(headers.Length);
            foreach (var header in headers)
            {
                if (header.FileSize == 0)
                    continue;
                result.Add(new DumpMemorySegment(header.VirtualAddress, header.FileOffset, header.FileSize));
            }

            result.Sort(static (a, b) => a.VirtualAddress.CompareTo(b.VirtualAddress));
            return result;
        }

        // -- IThreadInfoReader --

        public bool TryGetThreadInfo(uint osThreadId, out ThreadInfo info)
        {
            if (LoadThreads().TryGetValue(osThreadId, out IElfPRStatus? status))
            {
                info = BuildThreadInfo(status);
                return true;
            }

            info = default;
            return false;
        }

        public IEnumerable<ThreadInfo> EnumerateThreadInfo()
        {
            foreach (KeyValuePair<uint, IElfPRStatus> kvp in LoadThreads())
                yield return BuildThreadInfo(kvp.Value);
        }

        private static ThreadInfo BuildThreadInfo(IElfPRStatus status) => new()
        {
            OSThreadId = status.ThreadId,
            UserTime = status.UserTime,
            KernelTime = status.KernelTime,
            // The remaining fields aren't carried by Linux core dumps.
        };

        // -- IProcessInfoProvider --

        public ProcessInfo GetProcessInfo()
        {
            // Aggregate per-thread CPU times into a process-wide total. Skip
            // threads whose timeval could not be converted (overflow).
            TimeSpan userTime = TimeSpan.Zero;
            TimeSpan kernelTime = TimeSpan.Zero;
            bool anyTimes = false;
            foreach (KeyValuePair<uint, IElfPRStatus> kvp in LoadThreads())
            {
                if (kvp.Value.UserTime is TimeSpan ut)
                {
                    userTime += ut;
                    anyTimes = true;
                }
                if (kvp.Value.KernelTime is TimeSpan kt)
                {
                    kernelTime += kt;
                    anyTimes = true;
                }
            }

            // ImagePath: prefer the first non-interpreter loaded image whose path
            // matches AT_EXECFN's basename when available; otherwise fall back to
            // the first non-interpreter image.
            ulong interpreter = _core.GetAuxvValue(ElfAuxvType.Base);
            string? imagePath = null;
            foreach (ElfLoadedImage image in _core.LoadedImages.Values)
            {
                if (image.BaseAddress == interpreter)
                    continue;
                if (image.FileName.StartsWith("/dev", StringComparison.Ordinal))
                    continue;
                imagePath = image.FileName.Replace(" (deleted)", "");
                break;
            }

            return new ProcessInfo
            {
                ProcessId = (uint)ProcessId,
                ImagePath = imagePath,
                CommandLine = null,
                CreateTimeUtc = null,
                UserTime = anyTimes ? userTime : null,
                KernelTime = anyTimes ? kernelTime : null,
                Architecture = Architecture,
                TargetPlatform = TargetPlatform,
                OSVersion = null,
                OSBuildString = null,
                ProcessorCount = 0,
                DumpTimestampUtc = null,
            };
        }

        // -- IMemoryRegionReader --

        public IEnumerable<MemoryRegion> EnumerateMemoryRegions()
        {
            // Map every PT_LOAD program header into a MemoryRegion. PT_LOAD ranges
            // are the only mappings the kernel records into a core dump; reserved /
            // free pages are not represented so every region is reported as
            // committed.
            //
            // We classify a region as Image when its base address matches a known
            // module's base, Private otherwise.

            // Build a sorted list of (base, end) for image lookup.
            var imageRanges = _core.LoadedImages.Values
                .Select(img => (Base: img.BaseAddress, End: img.BaseAddress + img.Size))
                .OrderBy(r => r.Base)
                .ToArray();

            // IMemoryRegionReader contract requires regions yielded in ascending
            // base address order. Filter to PT_LOAD with non-zero size, then sort.
            ElfProgramHeader[] headers = _core.ElfFile.ProgramHeaders
                .Where(h => h.Type == ElfProgramHeaderType.Load && h.VirtualSize != 0)
                .OrderBy(h => h.VirtualAddress)
                .ToArray();

            foreach (ElfProgramHeader header in headers)
            {
                MemoryRegionProtect protect = MemoryRegionProtect.None;
                if (header.IsReadable) protect |= MemoryRegionProtect.Read;
                if (header.IsWritable) protect |= MemoryRegionProtect.Write;
                if (header.IsExecutable) protect |= MemoryRegionProtect.Execute;
                if (protect == MemoryRegionProtect.None) protect = MemoryRegionProtect.NoAccess;

                ulong va = header.VirtualAddress;
                ulong end = va + header.VirtualSize;

                ulong allocBase = va;
                MemoryRegionType regionType = MemoryRegionType.Private;
                foreach ((ulong imgBase, ulong imgEnd) in imageRanges)
                {
                    if (imgBase <= va && end <= imgEnd)
                    {
                        regionType = MemoryRegionType.Image;
                        allocBase = imgBase;
                        break;
                    }
                    if (imgBase > va)
                        break;
                }

                yield return new MemoryRegion
                {
                    BaseAddress = va,
                    Size = header.VirtualSize,
                    State = MemoryRegionState.Commit,
                    Type = regionType,
                    Protect = protect,
                    AllocationBase = allocBase,
                    AllocationProtect = protect,
                };
            }
        }

        // -- IModuleSegmentReader --

        public IEnumerable<ModuleSegment> EnumerateModuleSegments(ulong moduleBaseAddress)
        {
            // Find the loaded image at this base. LoadedImages is keyed by
            // filename, not address, so a small linear scan is needed —
            // module count is in the hundreds at worst.
            ElfLoadedImage? image = null;
            foreach (ElfLoadedImage candidate in _core.LoadedImages.Values)
            {
                if (candidate.BaseAddress == moduleBaseAddress)
                {
                    image = candidate;
                    break;
                }
            }
            if (image is null)
                yield break;

            using ElfFile? file = image.Open();
            if (file is null)
                yield break;

            // Compute load bias: ImageBase - p_vaddr of the lowest PT_LOAD.
            // For PIE / shared objects the lowest PT_LOAD has p_vaddr == 0,
            // so load bias == ImageBase. For position-dependent executables
            // load bias is typically 0.
            ulong lowestVaddr = ulong.MaxValue;
            foreach (ElfProgramHeader ph in file.ProgramHeaders)
            {
                if (ph.Type != ElfProgramHeaderType.Load || ph.VirtualSize == 0)
                    continue;
                if (ph.VirtualAddress < lowestVaddr)
                    lowestVaddr = ph.VirtualAddress;
            }
            if (lowestVaddr == ulong.MaxValue)
                yield break;
            ulong loadBias = moduleBaseAddress - lowestVaddr;

            foreach (ElfProgramHeader ph in file.ProgramHeaders)
            {
                if (ph.Type != ElfProgramHeaderType.Load || ph.VirtualSize == 0)
                    continue;
                yield return new ModuleSegment
                {
                    VirtualAddress = ph.VirtualAddress + loadBias,
                    VirtualSize = ph.VirtualSize,
                    IsReadable = ph.IsReadable,
                    IsWritable = ph.IsWritable,
                    IsExecutable = ph.IsExecutable,
                };
            }
        }
    }
}