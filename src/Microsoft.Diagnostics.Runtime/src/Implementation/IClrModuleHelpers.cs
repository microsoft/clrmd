// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IClrModuleHelpers
    {
        IDataReader DataReader { get; }
        IClrNativeHeapHelpers GetNativeHeapHelpers();
        MetadataImport? GetMetadataImport(ClrModule module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefMap(ClrModule module);
        IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeRefMap(ClrModule module);
        ClrExtendedModuleData GetExtendedData(ClrModule module);
        string? GetAssemblyName(ClrModule module);
    }
}