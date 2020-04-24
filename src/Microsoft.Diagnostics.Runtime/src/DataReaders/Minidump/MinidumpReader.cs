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
                MinidumpProcessorArchitecture.Arm64 => Architecture.Arm64,
                MinidumpProcessorArchitecture.Intel => Architecture.X86,
                _ => throw new NotImplementedException($"No support for platform {_minidump.Architecture}"),
            };

            PointerSize = _minidump.PointerSize;
        }

        public bool IsThreadSafe => true;

        public Architecture Architecture { get; }

        public uint ProcessId => 0;

        public bool IsFullMemoryAvailable => true; // todo remove me

        public int PointerSize { get; }

        public void Dispose()
        {
            _minidump.Dispose();
        }

        public IEnumerable<uint> EnumerateAllThreads() => _minidump.ContextData.Select(c => c.ThreadId);

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            return from module in _minidump.EnumerateModuleInfo()
                   select new ModuleInfo(module.BaseOfImage, module.SizeOfImage, module.DateTimeStamp, module.ModuleName, isVirtual: true, buildId: default, version: module.VersionInfo.AsVersionInfo());
        }

        public void FlushCachedData()
        {
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            int index = _minidump.ContextData.Search(threadID, (x, y) => x.ThreadId.CompareTo(y));
            if (index < 0)
                return false;

            MinidumpContextData ctx = _minidump.ContextData[index];
            if (ctx.ContextRva == 0 || ctx.ContextBytes == 0)
                return false;

            return _minidump.MemoryReader.ReadFromRVA(ctx.ContextRva, context) == context.Length;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            version = _minidump.EnumerateModuleInfo().FirstOrDefault(m => m.BaseOfImage == baseAddress)?.VersionInfo.AsVersionInfo() ?? default;
        }

        public bool QueryMemory(ulong address, out MemoryRegionInfo info)
        {
            //todo
            info = default;
            return false;
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead) => MemoryReader.Read(address, buffer, out bytesRead);
        public bool Read<T>(ulong address, out T value) where T : unmanaged => MemoryReader.Read(address, out value);
        public T Read<T>(ulong address) where T : unmanaged => MemoryReader.Read<T>(address);
        public bool ReadPointer(ulong address, out ulong value) => MemoryReader.ReadPointer(address, out value);
        public ulong ReadPointer(ulong address) => MemoryReader.ReadPointer(address);
    }
}
