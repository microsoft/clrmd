// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Linux;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal class CoreDumpReader : IDataReader2
    {
        private class CoreModuleInfo : ModuleInfo
        {
            internal readonly ElfFile _elfFile;
            private readonly PEImage _pe;

            public CoreModuleInfo(IDataReader reader, ElfLoadedImage image)
                : base(reader)
            {
                Debug.Assert(reader != null);
                _elfFile = image.Open();

                uint fileSize = (uint)image.Size;
                uint timeStamp = 0;

                if (_elfFile == null)
                {
                    PEImage pe = image.OpenAsPEImage();
                    if (pe.IsValid)
                    {
                        _pe = pe;
                        fileSize = (uint)pe.IndexFileSize;
                        timeStamp = (uint)pe.IndexTimeStamp;
                    }
                }
                else
                {
                    BuildId = _elfFile.BuildId;

                    // This keeps InitData from trying to open a Elf image as a PE image
                    _initialized = true;
                }

                FileName = image.Path;
                ImageBase = (ulong)image.BaseAddress;
                FileSize = fileSize;
                TimeStamp = timeStamp;
            }

            protected override void InitVersion(out VersionInfo version)
            {
                if (_elfFile != null)
                {
                    LinuxFunctions.GetVersionInfo(_dataReader, ImageBase, _elfFile, out version);
                }
                else
                {
                    version = default;

                    if (_pe != null)
                    {
                        // First try getting the version resource with the "file" module layout (isVirtual == false)
                        Utilities.FileVersionInfo fileVersion = _pe.GetFileVersionInfo();
                        if (fileVersion != null)
                        {
                            version = fileVersion.VersionInfo;
                        }
                        else
                        {
                            // If that fails, now try getting the version with the "loaded" layout (isVirtual == true)
                            PEImage image = GetPEImage();
                            if (image != null && image.IsValid)
                            {
                                fileVersion = image.GetFileVersionInfo();
                                if (fileVersion != null)
                                {
                                    version = fileVersion.VersionInfo;
                                }
                            }
                        }
                    }
                }
            }
        }

        private readonly string _source;
        private readonly Stream _stream;
        private readonly ElfCoreFile _core;
        private readonly int _pointerSize;
        private readonly Architecture _architecture;
        private Dictionary<uint, IElfPRStatus> _threads;
        private List<ModuleInfo> _modules;
        private readonly byte[] _buffer = new byte[512];

        public CoreDumpReader(string filename)
        {
            _source = filename;
            _stream = File.OpenRead(filename);
            _core = new ElfCoreFile(_stream);

            ElfMachine architecture = _core.ElfFile.Header.Architecture;
            switch (architecture)
            {
                case ElfMachine.EM_X86_64:
                    _pointerSize = 8;
                    _architecture = Architecture.Amd64;
                    break;

                case ElfMachine.EM_386:
                    _pointerSize = 4;
                    _architecture = Architecture.X86;
                    break;

                case ElfMachine.EM_AARCH64:
                    _pointerSize = 8;
                    _architecture = Architecture.Arm64;
                    break;

                case ElfMachine.EM_ARM:
                    _pointerSize = 4;
                    _architecture = Architecture.Arm;
                    break;

                default:
                    throw new NotImplementedException($"Support for {architecture} not yet implemented.");
            }
        }

        public bool IsMinidump => false; // TODO

        public void Close()
        {
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

        public IEnumerable<uint> EnumerateAllThreads()
        {
            InitThreads();
            return _threads.Keys;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            if (_modules == null)
            {
                // Need to filter out non-modules like the interpreter (named something 
                // like "ld-2.23") and anything that starts with /dev/ because their 
                // memory range overlaps with actual modules.
                ulong interpreter = _core.GetAuxvValue(ElfAuxvType.Base);

                _modules = new List<ModuleInfo>(_core.LoadedImages.Count);
                foreach (ElfLoadedImage image in _core.LoadedImages)
                    if ((ulong)image.BaseAddress != interpreter && !image.Path.StartsWith("/dev/"))
                        _modules.Add(new CoreModuleInfo(this, image));
            }

            return _modules;
        }

        public void Flush()
        {
            _threads = null;
            _modules = null;
        }

        public Architecture GetArchitecture()
        {
            return _architecture;
        }

        public uint GetPointerSize()
        {
            return (uint)_pointerSize;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            InitThreads();

            if (_threads.TryGetValue(threadID, out IElfPRStatus status))
            {
                return status.CopyContext(contextFlags, contextSize, context.ToPointer());
            }

            return false;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            InitThreads();

            if (_threads.TryGetValue(threadID, out IElfPRStatus status))
            {
                fixed (byte* ptr = context)
                {
                    return status.CopyContext(contextFlags, contextSize, ptr);
                }
            }

            return false;
        }

        public ulong GetThreadTeb(uint thread)
        {
            throw new NotImplementedException();
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            version = default;

            ModuleInfo moduleInfo = EnumerateModules().FirstOrDefault(module => module.ImageBase == baseAddress);
            if (moduleInfo != null)
            {
                version = moduleInfo.Version;
            }
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            int read = _core.ReadMemory((long)addr, _buffer, 4);
            if (read == 4)
                return BitConverter.ToUInt32(_buffer, 0);

            return 0;
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = _core.ReadMemory((long)address, buffer, bytesRequested);
            return bytesRead > 0;
        }

        public bool ReadMemory(ulong address, IntPtr ptr, int bytesRequested, out int bytesRead)
        {
            byte[] buffer = _buffer;
            if (bytesRequested > buffer.Length)
                buffer = new byte[bytesRequested];

            bool result = ReadMemory(address, buffer, bytesRequested, out bytesRead);
            if (result)
                Marshal.Copy(buffer, 0, ptr, bytesRead);

            return result;
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            int read = _core.ReadMemory((long)addr, _buffer, _pointerSize);
            if (read == _pointerSize)
            {
                if (_pointerSize == 8)
                    return BitConverter.ToUInt64(_buffer, 0);

                if (_pointerSize == 4)
                    return BitConverter.ToUInt32(_buffer, 0);
            }

            return 0;
        }

        public bool VirtualQuery(ulong address, out VirtualQueryData vq)
        {
            long addr = (long)address;
            foreach (ElfProgramHeader item in _core.ElfFile.ProgramHeaders)
            {
                long start = item.VirtualAddress;
                long end = start + item.VirtualSize;

                if (start <= addr && addr < end)
                {
                    vq = new VirtualQueryData((ulong)start, (ulong)item.VirtualSize);
                    return true;
                }
            }

            vq = new VirtualQueryData();
            return false;
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