// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class MinidumpReader : IDataReader
    {
        private readonly Minidump _minidump;
        private IMemoryReader? _readerCached;

        public IMemoryReader MemoryReader => _readerCached ??= _minidump.MemoryReader;

        public MinidumpReader(string crashDump)
        {
            if (crashDump is null)
                throw new ArgumentNullException(nameof(crashDump));

            _minidump = new Minidump(crashDump);

            Architecture = _minidump.Architecture switch
            {
                MinidumpProcessorArchitecture.Amd64 => Architecture.Amd64,
                MinidumpProcessorArchitecture.Arm => Architecture.Arm,
                MinidumpProcessorArchitecture.Intel => Architecture.X86,
                _ => throw new NotImplementedException($"No support for platform {_minidump.Architecture}"),
            };

            PointerSize = _minidump.PointerSize;
        }

        public bool IsThreadSafe => true;

        public Architecture Architecture { get; }

        public uint ProcessId => 0;

        public bool IsFullMemoryAvailable => throw new NotImplementedException();

        public int PointerSize { get; }

        public void Dispose()
        {
            _minidump.Dispose();
        }

        public IEnumerable<uint> EnumerateAllThreads() => _minidump.ContextData.Select(c => c.ThreadId);

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            return from module in _minidump.EnumerateModuleInfo()
                   select new ModuleInfo(this, module.BaseOfImage, module.SizeOfImage, module.DateTimeStamp, module.ModuleName, buildId: default, version: module.VersionInfo.AsVersionInfo());
        }

        public void FlushCachedData()
        {
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            throw new NotImplementedException();
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            throw new NotImplementedException();
        }

        public bool QueryMemory(ulong address, out MemoryRegionInfo info)
        {
            throw new NotImplementedException();
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead)
        {
            throw new NotImplementedException();
        }

        public bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public T Read<T>(ulong address) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public bool ReadPointer(ulong address, out ulong value)
        {
            throw new NotImplementedException();
        }

        public ulong ReadPointer(ulong address)
        {
            throw new NotImplementedException();
        }
    }
}
