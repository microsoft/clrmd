// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class ModuleBuilder : IModuleData
    {
        private ModuleData _moduleData;
        private readonly SOSDac _sos;

        public ModuleBuilder(IModuleHelpers helpers, SOSDac sos)
        {
            _sos = sos;
            Helpers = helpers;
        }

        public IModuleHelpers Helpers { get; }

        public ulong Address { get; private set; }

        public bool IsPEFile => _moduleData.IsPEFile != 0;
        public ulong PEImageBase => _moduleData.PEFile;
        public ulong ILImageBase => _moduleData.ILBase;
        public ulong MetadataStart => _moduleData.MetadataStart;
        public ulong MetadataLength => _moduleData.MetadataSize;

        public ulong Size { get; private set; }

        public string? Name
        {
            get
            {
                if (_moduleData.PEFile != 0)
                    return _sos.GetPEFileName(_moduleData.PEFile);

                return null;
            }
        }

        public string? AssemblyName
        {
            get
            {
                if (_moduleData.Assembly != 0)
                    return _sos.GetAssemblyName(_moduleData.Assembly);

                return null;
            }
        }

        public bool IsReflection => _moduleData.IsReflection != 0;

        public ulong AssemblyAddress => _moduleData.Assembly;

        public bool Init(ulong address)
        {
            Address = address;
            if (!_sos.GetModuleData(Address, out _moduleData))
                return false;

            using ClrDataModule? dataModule = _sos.GetClrDataModule(address);
            Size = dataModule != null && dataModule.GetModuleData(out ExtendedModuleData data) ? data.LoadedPESize : 0;
            return true;
        }
    }
}