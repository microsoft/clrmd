using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.MacOS.Structs;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal sealed class MachOCoreReader : CommonMemoryReader, IDataReader, IThreadReader, IExportReader, IDisposable
    {
        private readonly MachOCoreDump _core;

        public string DisplayName { get; }

        public bool IsThreadSafe => true;

        public OSPlatform TargetPlatform => OSPlatform.OSX;

        public Architecture Architecture => _core.Architecture;

        public int ProcessId { get; }

        public unsafe MachOCoreReader(string displayName, Stream stream, bool leaveOpen)
        {
            DisplayName = displayName;
            _core = new MachOCoreDump(stream, leaveOpen, DisplayName);
        }

        public IEnumerable<ModuleInfo> EnumerateModules() => _core.EnumerateModules().Select(m => new ModuleInfo(this, m.BaseAddress, m.FileName, true, unchecked((int)m.ImageSize), 0, default));

        public void FlushCachedData()
        {
        }

        public ImmutableArray<byte> GetBuildId(ulong baseAddress)
        {
            MachOModule? module = _core.GetModuleByBaseAddress(baseAddress);
            if (module is not null)
            {
                return module.BuildId;
            }
            return ImmutableArray<byte>.Empty;
        }

        public IEnumerable<uint> EnumerateOSThreadIds() => _core.Threads.Keys;

        public ulong GetThreadTeb(uint _) => 0;

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context)
        {
            if (!_core.Threads.TryGetValue(threadID, out thread_state_t thread))
                return false;

            switch (Architecture)
            {
                case Architecture.Amd64:
                    thread.x64.CopyContext(context);
                    break;
                case Architecture.Arm64:
                    thread.arm.CopyContext(context);
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }
            return true;
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
            MachOModule? module = _core.GetModuleByBaseAddress(baseAddress);
            if (module is not null && module.TryLookupSymbol(name, out address))
            {
                return true;
            }
            address = 0;
            return false;
        }

        public bool GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            MachOModule? module = _core.GetModuleByBaseAddress(baseAddress);
            if (module is not null)
            {
                foreach (Segment64LoadCommand segment in module.EnumerateSegments())
                {
                    if (((segment.InitProt & Segment64LoadCommand.VmProtWrite) != 0) && !segment.Name.Equals("__LINKEDIT"))
                    {
                        if (this.GetVersionInfo(module.LoadBias + segment.VMAddr, segment.VMSize, out version))
                        {
                            return true;
                        }
                    }
                }
            }
            version = default;
            return false;
        }

        public override int Read(ulong address, Span<byte> buffer) => _core.ReadMemory(address, buffer);

        public void Dispose()
        {
            _core.Dispose();
        }
    }
}
