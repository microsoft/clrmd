using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal sealed class MachOCoreReader : CommonMemoryReader, IDataReader, IDisposable
    {

        public string DisplayName { get; }

        private readonly MachOCoreDump _core;

        public bool IsThreadSafe => true;

        public OSPlatform TargetPlatform => OSPlatform.OSX;

        public Architecture Architecture { get; }

        public int ProcessId { get; }

        public unsafe MachOCoreReader(string displayName, Stream stream, bool leaveOpen)
        {
            DisplayName = displayName;
            _core = new MachOCoreDump(stream, leaveOpen, DisplayName);
        }

        public IEnumerable<ModuleInfo> EnumerateModules()
        {
            return _core.Modules.Select(m => new ModuleInfo(this, m.BaseAddress, m.FileName, true, 0, 0, default));
        }

        public void FlushCachedData()
        {
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress) => default;

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            throw new NotImplementedException();
        }

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version) { version = default; return false; }

        public override int Read(ulong address, Span<byte> buffer) => _core.ReadMemory(address, buffer);

        public void Dispose()
        {
            _core.Dispose();
        }
    }
}
