// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DataTargetImpl : DataTarget
    {
        private readonly IDataReader _dataReader;
        private ClrInfo[] _versions;
        private ModuleInfo _native;

        private readonly Lazy<ModuleInfo[]> _modules;
        private readonly List<DacLibrary> _dacLibraries = new List<DacLibrary>(2);

        public DataTargetImpl(IDataReader dataReader, IDebugClient client)
        {
            _dataReader = dataReader ?? throw new ArgumentNullException("dataReader");
            DebuggerInterface = client;
            Architecture = _dataReader.GetArchitecture();
            _modules = new Lazy<ModuleInfo[]>(InitModules);
        }

        internal ModuleInfo NativeRuntime
        {
            get
            {
                if (_versions == null)
                    _versions = InitVersions();

                return _native;
            }
        }

        public override IDataReader DataReader => _dataReader;

        public override bool IsMinidump => _dataReader.IsMinidump;

        public override Architecture Architecture { get; }

        public override uint PointerSize => _dataReader.GetPointerSize();

        public override IList<ClrInfo> ClrVersions
        {
            get
            {
                if (_versions == null)
                    _versions = InitVersions();

                return _versions;
            }
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public override IDebugClient DebuggerInterface { get; }

        public override IEnumerable<ModuleInfo> EnumerateModules()
        {
            return _modules.Value;
        }

        private ModuleInfo FindModule(ulong addr)
        {
            // TODO: Make binary search.
            foreach (var module in _modules.Value)
                if (module.ImageBase <= addr && addr < module.ImageBase + module.FileSize)
                    return module;

            return null;
        }

        private static readonly Regex s_invalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

        private ModuleInfo[] InitModules()
        {
            var sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules().Where(m => !s_invalidChars.IsMatch(m.FileName)));
            sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
            return sortedModules.ToArray();
        }

#pragma warning disable 0618
        private ClrInfo[] InitVersions()
        {
            var versions = new List<ClrInfo>();
            foreach (var module in EnumerateModules())
            {
                var clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

                if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr" && clrName != "mrt100_app" && clrName != "libcoreclr")
                    continue;

                ClrFlavor flavor;
                switch (clrName)
                {
                    case "mrt100_app":
                        _native = module;
                        continue;

                    case "libcoreclr":
                    case "coreclr":
                        flavor = ClrFlavor.Core;
                        break;

                    default:
                        flavor = ClrFlavor.Desktop;
                        break;
                }

                var isLinux = clrName == "libcoreclr";

                var dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), DacInfo.GetDacFileName(flavor, Architecture));

                if (isLinux)
                    dacLocation = Path.ChangeExtension(dacLocation, ".so");

                if (isLinux)
                {
                    if (!File.Exists(dacLocation))
                        dacLocation = Path.GetFileName(dacLocation);
                }
                else if (!File.Exists(dacLocation) || !PlatformFunctions.IsEqualFileVersion(dacLocation, module.Version))
                {
                    dacLocation = null;
                }

                var version = module.Version;
                var dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
                var dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

                var dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture)
                {
                    FileSize = module.FileSize,
                    TimeStamp = module.TimeStamp,
                    FileName = dacFileName,
                    Version = module.Version
                };

                versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
            }

            var result = versions.ToArray();
            Array.Sort(result);
            return result;
        }

#pragma warning restore 0618

        public override void Dispose()
        {
            _dataReader.Close();
            foreach (var library in _dacLibraries)
                library.Dispose();
        }

        internal override void AddDacLibrary(DacLibrary dacLibrary)
        {
            _dacLibraries.Add(dacLibrary);
        }
    }
}