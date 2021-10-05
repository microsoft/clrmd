// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal class CoredumpReader : CommonMemoryReader, IDataReader, IDisposable, IThreadReader, IExportReader
    {
        private readonly ElfCoreFile _core;
        private Dictionary<uint, IElfPRStatus>? _threads;
        private List<ModuleInfo>? _modules;

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => OSPlatform.Linux;

        public CoredumpReader(string path, Stream stream, bool leaveOpen)
        {
            DisplayName = path ?? throw new ArgumentNullException(nameof(path));
            _core = new ElfCoreFile(stream ?? throw new ArgumentNullException(nameof(stream)), leaveOpen);

            ElfMachine architecture = _core.ElfFile.Header.Architecture;
            (PointerSize, Architecture) = architecture switch
            {
                ElfMachine.EM_X86_64  => (8, Architecture.Amd64),
                ElfMachine.EM_386     => (4, Architecture.X86),
                ElfMachine.EM_AARCH64 => (8, Architecture.Arm64),
                ElfMachine.EM_ARM     => (4, Architecture.Arm),
                _ => throw new NotImplementedException($"Support for {architecture} not yet implemented."),
            };
        }

        public bool IsThreadSafe => false;

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
                    if ((ulong)image.BaseAddress != interpreter && !image.FileName.StartsWith("/dev", StringComparison.Ordinal))
                        _modules.Add(CreateModuleInfo(image));
            }

            return _modules;
        }

        private ModuleInfo CreateModuleInfo(ElfLoadedImage image)
        {
            using ElfFile? file = image.Open();

            int filesize = 0;
            int timestamp = 0;

            if (file is null)
            {
                using Stream stream = image.AsStream();
                PEImage.ReadIndexProperties(stream, out timestamp, out filesize);
            }

            // It's true that we are setting "IndexFileSize" to be the raw size on linux for Linux modules,
            // but this unblocks some SOS scenarios.
            if (filesize == 0)
            {
                filesize = unchecked((int)image.Size);
            }

            // We suppress the warning because the function it wants us to use is not available on all ClrMD platforms
#pragma warning disable CA1307 // Specify StringComparison

            // This substitution is for unloaded modules for which Linux appends " (deleted)" to the module name.
            string path = image.FileName.Replace(" (deleted)", "");

#pragma warning restore CA1307 // Specify StringComparison

            // We set buildId to "default" which means we will later lazily evaluate the buildId on demand.
            return new ModuleInfo(this, (ulong)image.BaseAddress, path, true, filesize, timestamp, buildId: default);
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

        public ImmutableArray<byte> GetBuildId(ulong baseAddress)
        {
            using ElfFile? elfFile = GetElfFile(baseAddress);
            return elfFile?.BuildId ?? ImmutableArray<byte>.Empty;
        }

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            using ElfFile? file = GetElfFile(baseAddress);
            if (file is not null)
            {
                return this.GetVersionInfo(baseAddress, file, out version);
            }

            using PEImage? pe = GetPEImage(baseAddress);
            if (pe is not null)
            {
                FileVersionInfo? fileVersionInfo = pe.GetFileVersionInfo();
                if (fileVersionInfo is not null)
                {
                    version = fileVersionInfo.VersionInfo;
                    return true;
                }
            }

            version = default;
            return false;
        }

        /// <summary>
        /// Returns the address of a module export symbol if found
        /// </summary>
        /// <param name="baseAddress">module base address</param>
        /// <param name="name">symbol name (without the module name prepended)</param>
        /// <param name="address">address returned</param>
        /// <returns>true if found</returns>
        bool IExportReader.TryGetSymbolAddress(ulong baseAddress, string name, out ulong address)
        {
            using ElfFile? elfFile = GetElfFile(baseAddress);
            if (elfFile is not null && elfFile.TryGetExportSymbol(name, out ulong offset))
            {
                address = baseAddress + offset;
                return true;
            }
            address = 0;
            return false;
        }

        private ElfFile? GetElfFile(ulong baseAddress)
        {
            return _core.LoadedImages.TryGetValue(baseAddress, out ElfLoadedImage? image) ? image?.Open() : null;
        }

        private PEImage? GetPEImage(ulong baseAddress)
        {
            return EnumerateModules().First(mod => mod.ImageBase == baseAddress).GetPEImage();
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
    }
}