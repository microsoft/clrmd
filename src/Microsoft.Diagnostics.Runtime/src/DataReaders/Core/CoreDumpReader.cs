// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Linux;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal class CoredumpReader : IDataReader, IDisposable
    {
        private readonly Stream _stream;
        private readonly ElfCoreFile _core;
        private Dictionary<uint, IElfPRStatus>? _threads;
        private List<ModuleInfo>? _modules;

        public string DisplayName { get; }
        public OSPlatform TargetPlatform => OSPlatform.Linux;

        public CoredumpReader(string path, Stream stream)
        {
            DisplayName = path ?? throw new ArgumentNullException(nameof(path));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _core = new ElfCoreFile(_stream);

            ElfMachine architecture = _core.ElfFile.Header.Architecture;
            switch (architecture)
            {
                case ElfMachine.EM_X86_64:
                    PointerSize = 8;
                    Architecture = Architecture.Amd64;
                    break;

                case ElfMachine.EM_386:
                    PointerSize = 4;
                    Architecture = Architecture.X86;
                    break;

                case ElfMachine.EM_AARCH64:
                    PointerSize = 8;
                    Architecture = Architecture.Arm64;
                    break;

                case ElfMachine.EM_ARM:
                    PointerSize = 4;
                    Architecture = Architecture.Arm;
                    break;

                default:
                    throw new NotImplementedException($"Support for {architecture} not yet implemented.");
            }
        }

        public bool IsThreadSafe => false;

        public void Dispose()
        {
            _stream.Dispose();
        }

        public uint ProcessId
        {
            get
            {
                foreach (IElfPRStatus status in _core.EnumeratePRStatus())
                    return status.ProcessId;

                return uint.MaxValue;
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

                _modules = new List<ModuleInfo>(_core.LoadedImages.Length);
                foreach (ElfLoadedImage image in _core.LoadedImages)
                    if ((ulong)image.BaseAddress != interpreter && !image.Path.StartsWith("/dev"))
                        _modules.Add(CreateModuleInfo(image));
            }

            return _modules;
        }

        private ModuleInfo CreateModuleInfo(ElfLoadedImage image)
        {
            ElfFile? file = image.Open();

            int filesize = 0;
            int timestamp = 0;

            if (file is null)
            {
                using PEImage pe = image.OpenAsPEImage();
                filesize = pe.IndexFileSize;
                timestamp = pe.IndexTimeStamp;
            }

            // We set buildId to "default" which means we will later lazily evaluate the buildId on demand.
            return new ModuleInfo((ulong)image.BaseAddress, image.Path, image._containsExecutable, filesize, timestamp, buildId: default);
        }

        public void FlushCachedData()
        {
            _threads = null;
            _modules = null;
        }

        public Architecture Architecture { get; }

        public int PointerSize { get; }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            InitThreads();

            if (_threads!.TryGetValue(threadID, out IElfPRStatus? status))
                return status.CopyContext(contextFlags, context);

            return false;
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress)
        {
            return GetElfFile(baseAddress)?.BuildId ?? ImmutableArray<byte>.Empty;
        }

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            ElfFile? file = GetElfFile(baseAddress);
            if (file is null)
            {
                version = default;
                return false;
            }

            return LinuxFunctions.GetVersionInfo(this, baseAddress, file, out version);
        }

        private ElfFile? GetElfFile(ulong baseAddress)
        {
            return _core.LoadedImages.First(image => (ulong)image.BaseAddress == baseAddress).Open();
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            DebugOnly.Assert(!buffer.IsEmpty);
            if (address > long.MaxValue)
            {
                bytesRead = 0;
                return false;
            }

            bytesRead = _core.ReadMemory((long)address, buffer);
            return bytesRead > 0;
        }

        public ulong ReadPointer(ulong address)
        {
            ReadPointer(address, out ulong value);
            return value;
        }

        public unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (Read(address, buffer, out int size) && size == sizeof(T))
            {
                value = Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
                return true;
            }

            value = default;
            return false;
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            Read(address, out T value);
            return value;
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (Read(address, buffer, out int size) && size == IntPtr.Size)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        internal IEnumerable<string> GetModulesFullPath()
        {
            return EnumerateModules().Where(module => !string.IsNullOrEmpty(module.FileName)).Select(module => module.FileName!);
        }

        private void InitThreads()
        {
            if (_threads != null)
                return;

            _threads = new Dictionary<uint, IElfPRStatus>();
            foreach (IElfPRStatus status in _core.EnumeratePRStatus())
                _threads.Add(status.ThreadId, status);
        }
    }
}