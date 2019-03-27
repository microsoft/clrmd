// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DataTargetImpl : DataTarget
    {
        private readonly IDataReader _dataReader;
        private uint? _pid;
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

        public override uint ProcessId
        {
            get
            {
                if (!_pid.HasValue)
                {
                    if (_dataReader is IDataReader2 reader2)
                        _pid = reader2.ProcessId;
                    else
                        _pid = uint.MaxValue;
                }

                return _pid.Value;
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

                string dacFileName = ClrInfoProvider.GetDacFileName(flavor, platform);
                string dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), dacFileName);

                if (platform == Platform.Linux)
                {
                    if (File.Exists(dacLocation))
                    {
                        // Works around issue https://github.com/dotnet/coreclr/issues/20205
                        int processId = Process.GetCurrentProcess().Id;
                        string tempDirectory = Path.Combine(Path.GetTempPath(), "clrmd" + processId);
                        Directory.CreateDirectory(tempDirectory);

                        string symlink = Path.Combine(tempDirectory, dacFileName);
                        if (LinuxFunctions.symlink(dacLocation, symlink) == 0)
                        {
                            dacLocation = symlink;
                        }
                    }
                    else
                    {
                        dacLocation = dacFileName;
                    }
                }
                else if (!File.Exists(dacLocation) || !PlatformFunctions.IsEqualFileVersion(dacLocation, module.Version))
                {
                    dacLocation = null;
                }

                VersionInfo version = module.Version;
                string dacAgnosticName = ClrInfoProvider.GetDacRequestFileName(flavor, Architecture, Architecture, version, platform);
                string dacRegularName = ClrInfoProvider.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version, platform);

                DacInfo dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture)
                {
                    FileSize = module.FileSize,
                    TimeStamp = module.TimeStamp,
                    FileName = dacRegularName,
                    Version = module.Version
                };

                module.IsRuntime = true; //strange logic here (originally from the ClrInfo constructor)

                versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
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

        protected internal override void AddDacLibrary(DacLibrary dacLibrary)
        {
            _dacLibraries.Add(dacLibrary);
        }
    }
}