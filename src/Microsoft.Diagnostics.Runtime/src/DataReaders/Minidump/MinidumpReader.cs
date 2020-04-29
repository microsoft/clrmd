// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class MinidumpReader : IDataReader, IDisposable
    {
        private readonly Minidump _minidump;
        private IMemoryReader? _readerCached;

        public OSPlatform TargetPlatform => OSPlatform.Windows;

        public string DisplayName { get; }

        public IMemoryReader MemoryReader => _readerCached ??= _minidump.MemoryReader;

        public MinidumpReader(string crashDump)
            : this(crashDump, File.OpenRead(crashDump ?? throw new ArgumentNullException(nameof(crashDump))))
        {
        }

        public MinidumpReader(string crashDump, Stream stream)
        {
            if (crashDump is null)
                throw new ArgumentNullException(nameof(crashDump));

            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            DisplayName = crashDump;

            _minidump = new Minidump(crashDump, stream);

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

        public int PointerSize { get; }

        public void Dispose()
        {
            _minidump.Dispose();
        }


        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            // We set buildId to "Empty" since only PEImages exist where minidumps are created, and we do not
            // want to try to lazily evaluate the buildId later
            return from module in _minidump.EnumerateModuleInfo()
                   select new ModuleInfo(module.BaseOfImage, module.ModuleName, true, module.SizeOfImage,
                                         module.DateTimeStamp, ImmutableArray<byte>.Empty);
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

        public ImmutableArray<byte> GetBuildId(ulong baseAddress) => ImmutableArray<byte>.Empty;

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {

            MinidumpModuleInfo module = _minidump.EnumerateModuleInfo().FirstOrDefault(m => m.BaseOfImage == baseAddress);
            if (module == null)
            {
                version = default;
                return false;
            }

            version = module.VersionInfo.AsVersionInfo();
            return true;
        }

        public bool Read(ulong address, Span<byte> buffer, out int bytesRead) => MemoryReader.Read(address, buffer, out bytesRead);
        public bool Read<T>(ulong address, out T value) where T : unmanaged => MemoryReader.Read(address, out value);
        public T Read<T>(ulong address) where T : unmanaged => MemoryReader.Read<T>(address);
        public bool ReadPointer(ulong address, out ulong value) => MemoryReader.ReadPointer(address, out value);
        public ulong ReadPointer(ulong address) => MemoryReader.ReadPointer(address);
    }
}
