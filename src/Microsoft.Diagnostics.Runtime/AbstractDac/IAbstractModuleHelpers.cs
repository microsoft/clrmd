// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Used to construct ClrModules.
    ///
    /// This interface is required.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    internal interface IAbstractModuleProvider
    {
        ClrModuleInfo GetModuleInfo(ulong module);
        MetadataImport? GetMetadataImport(ulong module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefMap(ulong module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeRefMap(ulong module);
    }

    internal struct ClrModuleInfo
    {
        public ulong Address { get; set; }
        public ulong ImageBase { get; set; }
        public ulong Id { get; set; }
        public ulong Assembly { get; set; }

        public bool IsPEFile { get; set; }
        public bool IsDynamic { get; set; }

        public ulong MetadataAddress { get; set; }
        public ulong MetadataSize { get; set; }

        public ulong ThunkHeap { get; set; }
        public ulong LoaderAllocator { get; set; }

        public string? AssemblyName { get; set; }
        public string? FileName { get; set; }
        public ModuleLayout Layout { get; set; }
        public ulong Size { get; set; }
    }
}