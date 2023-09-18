// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrModuleHelpers
    {
        IDataReader DataReader { get; }
        IClrNativeHeapHelpers GetNativeHeapHelpers();
        MetadataImport? GetMetadataImport(ClrModule module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefMap(ClrModule module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeRefMap(ClrModule module);
        ExtendedModuleData GetExtendedData(ClrModule module);
        string? GetAssemblyName(ClrModule module);
    }
}