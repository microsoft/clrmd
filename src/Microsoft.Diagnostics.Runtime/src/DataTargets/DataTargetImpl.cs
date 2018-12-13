// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        private readonly Lazy<ModuleInfo[]> _modules;
        private readonly List<DacLibrary> _dacLibraries = new List<DacLibrary>(2);

        public DataTargetImpl(IDataReader dataReader, IDebugClient client)
        {
            _dataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
            DebuggerInterface = client;
            Architecture = _dataReader.GetArchitecture();
            _modules = new Lazy<ModuleInfo[]>(InitModules);
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

        private static readonly Regex s_invalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

        private ModuleInfo[] InitModules()
        {
            List<ModuleInfo> sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules().Where(m => !s_invalidChars.IsMatch(m.FileName)));
            sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
            return sortedModules.ToArray();
        }

        private ClrInfo[] InitVersions()
        {
            List<ClrInfo> versions = new List<ClrInfo>();
            foreach (ModuleInfo module in EnumerateModules())
            {
                if (!ClrInfoProvider.IsSupportedRuntime(module, out var flavor, out var platform))
                    continue;

                module.IsRuntime = true; //strange logic here (originally from the ClrInfo constructor)
                versions.Add(new ClrInfo(this, module, flavor, platform));
            }

            ClrInfo[] result = versions.ToArray();
            Array.Sort(result);
            return result;
        }

        public override void Dispose()
        {
            _dataReader.Close();
            foreach (DacLibrary library in _dacLibraries)
                library.Dispose();
        }

        internal override void AddDacLibrary(DacLibrary dacLibrary)
        {
            _dacLibraries.Add(dacLibrary);
        }
    }
}