// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IMethodHelpers
    {
        IDataReader DataReader { get; }

        bool GetSignature(ulong methodDesc, out string? signature);
        ImmutableArray<ILToNativeMap> GetILMap(ulong nativeCode, in HotColdRegions hotColdInfo);
        ulong GetILForModule(ulong address, uint rva);
    }
}