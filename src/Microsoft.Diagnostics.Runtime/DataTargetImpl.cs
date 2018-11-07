// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DataTargetImpl : DataTarget
    {
        private IDataReader _dataReader;
        private IDebugClient _client;
        private Architecture _architecture;
        private Lazy<ClrInfo[]> _versions;
        private Lazy<ModuleInfo[]> _modules;
        
        public DataTargetImpl(IDataReader dataReader, IDebugClient client)
        {
            _dataReader = dataReader ?? throw new ArgumentNullException("dataReader");
            _client = client;
            _architecture = _dataReader.GetArchitecture();
            _modules = new Lazy<ModuleInfo[]>(InitModules);
            _versions = new Lazy<ClrInfo[]>(InitVersions);
        }

        public override IDataReader DataReader
        {
            get
            {
                return _dataReader;
            }
        }

        public override bool IsMinidump
        {
            get { return _dataReader.IsMinidump; }
        }

        public override Architecture Architecture
        {
            get { return _architecture; }
        }

        public override uint PointerSize
        {
            get { return _dataReader.GetPointerSize(); }
        }

        public override IList<ClrInfo> ClrVersions
        {
            get { return _versions.Value; }
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public override IDebugClient DebuggerInterface
        {
            get { return _client; }
        }

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

        private static Regex s_invalidChars = new Regex($"[{Regex.Escape(new string(System.IO.Path.GetInvalidPathChars()))}]");

        private ModuleInfo[] InitModules()
        {
            var sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules().Where(m => !s_invalidChars.IsMatch(m.FileName)));
            sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
            return sortedModules.ToArray();
        }

        private ClrInfo[] InitVersions()
        {

            List<ClrInfo> versions = new List<ClrInfo>();
            foreach (ModuleInfo module in EnumerateModules())
            {
                string clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

                if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr" && clrName != "mrt100_app" && clrName != "libcoreclr")
                    continue;

                ClrFlavor flavor;
                switch (clrName)
                {
                    case "mrt100_app":
                        flavor = ClrFlavor.Native;
                        break;

                    case "libcoreclr":
                    case "coreclr":
                        flavor = ClrFlavor.Core;
                        break;

                    default:
                        flavor = ClrFlavor.Desktop;
                        break;
                }

                bool isLinux = clrName == "libcoreclr";

                string dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), DacInfo.GetDacFileName(flavor, Architecture));

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

                VersionInfo version = module.Version;
                string dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
                string dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

                DacInfo dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture)
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
        }
    }
}
