// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Linux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal class CoreDumpReader : IDataReader
    {
        private readonly string _source;
        private readonly Stream _stream;
        private readonly ElfCoreFile _core;
        private readonly int _pointerSize;
        private readonly Architecture _architecture;
        private Dictionary<uint, ELFPRStatus> _threads;
        private readonly byte[] _buffer = new byte[512];

        public CoreDumpReader(string filename)
        {
            _source = filename;
            _stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            _core = new ElfCoreFile(_stream);


            switch (_core.Architecture)
            {
                case ElfMachine.EM_X86_64:
                    _pointerSize = 8;
                    _architecture = Architecture.Amd64;
                    break;

                default:
                    throw new NotImplementedException($"Support for {_core.Architecture} not yet implemented.");
            }
        }

        public bool IsMinidump => false; // TODO

        public void Close()
        {
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            InitThreads();
            return _threads.Keys;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            return _core.LoadedImages.Select(img => CreateModuleInfo(img)).ToArray();
        }

        private ModuleInfo CreateModuleInfo(ElfLoadedImage img)
        {
            string filename = Path.GetFileName(img.Path);

            return new ModuleInfo()
            {
                FileName = img.Path,
                FileSize = (uint)img.Size,
                ImageBase = (ulong)img.BaseAddress,
                IsRuntime = filename.Equals("libcoreclr.so", StringComparison.OrdinalIgnoreCase)
            };
        }

        public void Flush()
        {
            _threads = null;
        }

        public Architecture GetArchitecture() => _architecture;

        public uint GetPointerSize() => (uint)_pointerSize;


        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            if (contextSize != AMD64Context.Size)
                return false;

            InitThreads();

            AMD64Context* ctx = (AMD64Context*)context.ToPointer();
            ctx->ContextFlags = (int)contextFlags;
            if (_threads.TryGetValue(threadID, out ELFPRStatus status))
            {
                CopyContext(ctx, ref status.RegisterSet);
                return true;
            }
            
            return false;
        }

        private unsafe void CopyContext(AMD64Context* ctx, ref RegSetX64 registerSet)
        {
            ctx->R15 = registerSet.R15;
            ctx->R14 = registerSet.R14;
            ctx->R13 = registerSet.R13;
            ctx->R12 = registerSet.R12;
            ctx->Rbp = registerSet.Rbp;
            ctx->Rbx = registerSet.Rbx;
            ctx->R11 = registerSet.R11;
            ctx->R10 = registerSet.R10;
            ctx->R9 = registerSet.R9;
            ctx->R8 = registerSet.R8;
            ctx->Rax = registerSet.Rax;
            ctx->Rcx = registerSet.Rcx;
            ctx->Rdx = registerSet.Rdx;
            ctx->Rsi = registerSet.Rsi;
            ctx->Rdi = registerSet.Rdi;
            ctx->Rip = registerSet.Rip;
            ctx->Rsp = registerSet.Rsp;
        }

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            if (contextSize != AMD64Context.Size)
                return false;

            InitThreads();

            if (_threads.TryGetValue(threadID, out ELFPRStatus status))
            {
                fixed (byte* ptr = context)
                {
                    AMD64Context* ctx = (AMD64Context*)ptr;
                    ctx->ContextFlags = (int)contextFlags;
                    CopyContext(ctx, ref status.RegisterSet);
                    return true;
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
            // TODO
            System.Diagnostics.Debug.WriteLine($"GetVersionInfo not yet implemented: addr={baseAddress:x}");
            version = new VersionInfo();
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
                long start = item.RefHeader.VirtualAddress;
                long end = start + item.RefHeader.VirtualSize;

                if (start <= addr && addr < end)
                {
                    vq = new VirtualQueryData((ulong)start, (ulong)item.RefHeader.VirtualSize);
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
            
            _threads = new Dictionary<uint, ELFPRStatus>();
            foreach (ELFPRStatus status in _core.EnumeratePRStatus())
                _threads.Add(status.Pid, status);
        }
    }
}