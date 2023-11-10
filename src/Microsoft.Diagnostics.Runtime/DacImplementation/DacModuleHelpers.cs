// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacModuleHelpers : IAbstractModuleHelpers, IDisposable
    {
        private const int mdtTypeDef = 0x02000000;
        private const int mdtTypeRef = 0x01000000;
        private readonly Dictionary<ulong, DacMetadataReader?> _imports = new();
        private readonly Dictionary<ulong, ClrDataModule?> _modules = new();
        private readonly SOSDac _sos;

        public DacModuleHelpers(SOSDac sos)
        {
            _sos = sos;
        }

        public ClrModuleInfo GetModuleInfo(ulong moduleAddress)
        {
            _sos.GetModuleData(moduleAddress, out ModuleData data);

            ClrModuleInfo result = new()
            {
                Address = moduleAddress,
                Assembly = data.Assembly,
                AssemblyName = _sos.GetAssemblyName(data.Assembly),
                Id = data.ModuleID,
                Index = data.ModuleIndex,
                IsPEFile = data.IsPEFile != 0,
                ImageBase = data.ILBase,
                MetadataAddress = data.MetadataStart,
                MetadataSize = data.MetadataSize,
                IsDynamic = data.IsReflection != 0,
                ThunkHeap = data.ThunkHeap,
                LoaderAllocator = data.LoaderAllocator,
            };

            ClrDataModule? dataModule = GetClrDataModule(moduleAddress);
            if (dataModule is not null && dataModule.GetModuleData(out ExtendedModuleData extended))
            {
                result.Layout = extended.IsFlatLayout != 0 ? ModuleLayout.Flat : ModuleLayout.Mapped;
                result.IsDynamic |= extended.IsDynamic != 0;
                result.Size = extended.LoadedPESize;
                result.FileName = dataModule.GetFileName();
            }

            return result;
        }

        public IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefMap(ulong module) => GetModuleMap(module, SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable);

        public IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeRefMap(ulong module) => GetModuleMap(module, SOSDac.ModuleMapTraverseKind.TypeRefToMethodTable);

        private List<(ulong MethodTable, int Token)> GetModuleMap(ulong module, SOSDac.ModuleMapTraverseKind kind)
        {
            int tokenType = kind == SOSDac.ModuleMapTraverseKind.TypeDefToMethodTable ? mdtTypeDef : mdtTypeRef;
            List<(ulong MethodTable, int Token)> result = new();
            _sos.TraverseModuleMap(kind, module, (token, mt, _) => { result.Add((mt, token | tokenType)); });

            return result;
        }

        public IAbstractMetadataReader? GetMetadataReader(ulong moduleAddress)
        {
            lock (_imports)
                if (_imports.TryGetValue(moduleAddress, out DacMetadataReader? reader))
                    return reader;

            ClrDataModule? module = GetClrDataModule(moduleAddress);
            MetadataImport? import = null;

            if (module is not null)
            {
                nint qiResult = module.QueryInterface(MetadataImport.IID_IMetaDataImport);
                if (qiResult != 0)
                {
                    import = new(module.Library, qiResult);
                }
            }

            DacMetadataReader? result = import != null ? new(import) : null;
            lock (_imports)
            {
                try
                {
                    _imports.Add(moduleAddress, result);
                    import = null;  // don't dispose
                }
                catch (ArgumentException)
                {
                    result = _imports[moduleAddress];
                }
            }

            // It's ok if a really unexpected exception causes us not to call Dispose.  Eventually
            // this will be cleaned up by the finalizer, but all else being equal we want to
            // opportunistically release memory here.
            import?.Dispose();
            return result;
        }

        private ClrDataModule? GetClrDataModule(ulong moduleAddress)
        {
            lock (_modules)
                if (_modules.TryGetValue(moduleAddress, out ClrDataModule? dataModule))
                    return dataModule;

            ClrDataModule? result = _sos.GetClrDataModule(moduleAddress);

            lock (_modules)
            {
                try
                {
                    _modules.Add(moduleAddress, result);
                }
                catch (ArgumentException)
                {
                    result?.Dispose();
                    return _modules[moduleAddress];
                }
            }

            return result;
        }

        public void Dispose()
        {
            Flush();
        }

        public void Flush()
        {
            lock (_imports)
            {
                foreach (DacMetadataReader? import in _imports.Values)
                    import?.Dispose();

                _imports.Clear();
            }

            lock (_modules)
            {
                foreach (ClrDataModule? module in _modules.Values)
                    module?.Dispose();

                _modules.Clear();
            }
        }
    }
}