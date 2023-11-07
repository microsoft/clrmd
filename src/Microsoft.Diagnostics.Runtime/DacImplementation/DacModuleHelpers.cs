// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacModuleHelpers : IAbstractModuleHelpers
    {
        private const int mdtTypeDef = 0x02000000;
        private const int mdtTypeRef = 0x01000000;

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
                IsPEFile = data.IsPEFile != 0,
                ImageBase = data.ILBase,
                MetadataAddress = data.MetadataStart,
                MetadataSize = data.MetadataSize,
                IsDynamic = data.IsReflection != 0,
                ThunkHeap = data.ThunkHeap,
                LoaderAllocator = data.LoaderAllocator,
            };

            using ClrDataModule? dataModule = _sos.GetClrDataModule(moduleAddress);
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

        public MetadataImport? GetMetadataImport(ulong module) => _sos.GetMetadataImport(module);
    }
}