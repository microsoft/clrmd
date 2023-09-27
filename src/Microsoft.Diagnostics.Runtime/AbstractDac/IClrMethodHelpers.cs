// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrMethodHelpers
    {
        IDataReader DataReader { get; }

        string? GetSignature(ulong methodDesc);
        ImmutableArray<ILToNativeMap> GetILMap(ClrMethod method);
        ulong GetILForModule(ulong address, uint rva);
    }
}